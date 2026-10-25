using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ZEarth.App.Services
{
    public interface ILoggerService
    {
        ObservableCollection<string> MemoryLogs { get; }
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
    }

    /// <summary>
    /// Non-blocking logger. Callers only enqueue a formatted line onto a bounded channel; a single
    /// background writer batches disk appends. This removes all synchronous file I/O from hot paths
    /// such as the WebView2 <c>WebMessageReceived</c> callback (RC-1).
    /// </summary>
    public sealed class LoggerService : ILoggerService
    {
        private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
        public static LoggerService Instance => _instance.Value;

        private const int QueueCapacity = 8192;
        private const long MaxFileBytes = 8L * 1024 * 1024; // rotate at 8 MB
        private const int MemoryLogCapacity = 500;
        private const int MaxBatch = 256;
        private const int FlushIntervalMs = 400;

        private readonly string _logFilePath;
        private readonly Channel<string> _channel;
        private readonly Task _writerTask;
        private readonly CancellationTokenSource _shutdownCts = new();
        private long _droppedEntries;
        private int _shutdownStarted;

        public ObservableCollection<string> MemoryLogs { get; } = new ObservableCollection<string>();

        /// <summary>
        /// When false (default) entries are only written to disk. The in-memory collection is leached
        /// lazily by the diagnostics view, which avoids one Dispatcher callback per message.
        /// </summary>
        public bool MemoryLoggingEnabled { get; set; }

        public long DroppedEntryCount => Interlocked.Read(ref _droppedEntries);

        public LoggerService() : this(null)
        {
        }

        /// <summary>
        /// Creates a logger writing into <paramref name="logDirectory"/>. A null value uses the
        /// per-user application log directory. Tests and measurement harnesses pass an isolated
        /// directory so they never pollute the application log.
        /// </summary>
        public LoggerService(string? logDirectory)
        {
            var logDir = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth", "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "zearth.log");

            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });

            _writerTask = Task.Run(WriterLoopAsync);
        }

        public void LogInfo(string message) => Log("INFO", message);
        public void LogWarning(string message) => Log("WARN", message);
        public void LogError(string message, Exception? ex = null)
        {
            var fullMsg = ex != null
                ? $"{message} | Exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}"
                : message;
            Log("ERROR", fullMsg);
        }

        private void Log(string level, string message)
        {
            if (Volatile.Read(ref _shutdownStarted) == 1) return;

            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            if (!_channel.Writer.TryWrite(entry))
            {
                Interlocked.Increment(ref _droppedEntries);
                // Critical entries must not be lost to back-pressure: append once, synchronously,
                // but only as a last resort (never on the routine path).
                if (level == "ERROR")
                {
                    TryAppendFallback(entry);
                }
            }
        }

        private void TryAppendFallback(string entry)
        {
            try
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
            catch
            {
                // Never crash on log failure
            }
        }

        private async Task WriterLoopAsync()
        {
            var reader = _channel.Reader;
            var pending = new List<string>(MaxBatch);
            try
            {
                while (await reader.WaitToReadAsync(_shutdownCts.Token).ConfigureAwait(false))
                {
                    pending.Clear();
                    while (pending.Count < MaxBatch && reader.TryRead(out var entry))
                    {
                        pending.Add(entry);
                    }

                    if (pending.Count == 0) continue;

                    WriteBatch(pending);
                    PublishToMemory(pending);

                    // Drain any additional bursts that arrived while writing, then flush.
                    await Task.Delay(FlushIntervalMs, _shutdownCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception)
            {
                // The writer must never fault the process
            }
            finally
            {
                // Drain whatever is left after completion. `pending` was already flushed by the loop.
                var remaining = new List<string>(MaxBatch);
                while (reader.TryRead(out var entry))
                {
                    remaining.Add(entry);
                }
                if (remaining.Count > 0)
                {
                    WriteBatch(remaining);
                    PublishToMemory(remaining);
                }
            }
        }

        private void WriteBatch(List<string> entries)
        {
            try
            {
                RotateIfNeeded();
                using var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                foreach (var entry in entries)
                {
                    writer.WriteLine(entry);
                }
                writer.Flush();
            }
            catch
            {
                // Ignore transient log I/O failures
            }
        }

        private void RotateIfNeeded()
        {
            try
            {
                var info = new FileInfo(_logFilePath);
                if (!info.Exists || info.Length < MaxFileBytes) return;

                var backup = _logFilePath + ".1";
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
                File.Move(_logFilePath, backup);
            }
            catch
            {
                // Rotation is best-effort
            }
        }

        private void PublishToMemory(List<string> entries)
        {
            if (!MemoryLoggingEnabled) return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            // A single marshaled callback per batch instead of one per entry.
            dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var entry in entries)
                {
                    MemoryLogs.Add(entry);
                    if (MemoryLogs.Count > MemoryLogCapacity)
                    {
                        MemoryLogs.RemoveAt(0);
                    }
                }
            }));
        }

        /// <summary>
        /// Stops accepting new entries, drains the queue and flushes pending lines.
        /// Safe to call more than once.
        /// </summary>
        public void Shutdown(TimeSpan? drainTimeout = null)
        {
            if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1) return;

            _channel.Writer.TryComplete();

            try
            {
                var timeout = drainTimeout ?? TimeSpan.FromSeconds(3);
                if (!_writerTask.Wait(timeout))
                {
                    _shutdownCts.Cancel();
                }
            }
            catch
            {
                // Never block shutdown on logging
            }
        }
    }
}
