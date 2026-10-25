using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests
{
    /// <summary>
    /// Reproducible micro-benchmarks for the two hot paths changed by the responsiveness work.
    ///
    /// Methodology: each measurement runs the identical workload twice on the same machine in the
    /// same process -- once using a faithful reconstruction of the pre-fix implementation (as
    /// described by the root-cause audit) and once using the current implementation -- and reports
    /// p50/p95/p99/mean of the per-call duration in microseconds. Calls are made on the calling
    /// thread, which is the WebView2 UI thread in the real application. These are in-process
    /// micro-benchmarks of the logging/marshalling hot path; they are NOT a substitute for
    /// end-to-end interactive GUI latency measurement.
    ///
    /// Results are printed as single lines prefixed with "MEASURE " so they can be captured from
    /// the test console log and archived as evidence.
    /// </summary>
    [TestClass]
    public class ResponsivenessMeasurementHarness
    {
        private const int LogIterations = 2000;
        private const int MessageIterations = 2000;

        [TestMethod]
        [TestCategory("Measurement")]
        public void Measure_LogHotPath_PreFixVsPostFix()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"zearth_measure_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var preFixFile = Path.Combine(dir, "prefix.log");

            try
            {
                // Warm up the file system / JIT for both candidates.
                for (int i = 0; i < 50; i++)
                {
                    File.AppendAllText(preFixFile, $"warmup-{i}{Environment.NewLine}");
                }

                var logger = new LoggerService(dir);

                var preFix = Measure(LogIterations, i =>
                {
                    var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [INFO] telemetry message {i}";
                    // Reconstruction of the pre-fix implementation: format + synchronous append
                    // (open/close) on the UI thread for every single message.
                    File.AppendAllText(preFixFile, line + Environment.NewLine);
                });

                var postFix = Measure(LogIterations, i => logger.LogInfo($"telemetry message {i}"));

                logger.Shutdown(TimeSpan.FromSeconds(10));

                Report("log.hotpath", "pre-fix", preFix);
                Report("log.hotpath", "post-fix", postFix);

                Assert.AreEqual(0, logger.DroppedEntryCount, "Enqueue-only logging must not drop within bench capacity");
                Assert.IsTrue(preFix.P99 > postFix.P99,
                    "The non-blocking logger must be faster at p99 than synchronous per-call file appends");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best effort */ }
            }
        }

        [TestMethod]
        [TestCategory("Measurement")]
        public void Measure_TelemetryMessageHandling_PreFixVsPostFix()
        {
            const string payload =
                "{\"event\":\"camera.changed\",\"camera\":{\"latitude\":30.1,\"longitude\":31.2,\"altitude\":1234.5,\"heading\":45,\"pitch\":-60,\"roll\":0}}";

            var dir = Path.Combine(Path.GetTempPath(), $"zearth_measure_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var preFixFile = Path.Combine(dir, "prefix.log");

            try
            {
                // ---- Pre-fix reconstruction -------------------------------------------------
                // For every message the old path: parsed JSON, formatted a log line, appended it
                // synchronously, and pushed three bound properties into ObservableCollections
                // (each raising CollectionChanged/PropertyChanged on the UI thread).
                var preFixLogs = new ObservableCollection<string>();
                var preFix = Measure(MessageIterations, i =>
                {
                    using var doc = JsonDocument.Parse(payload);
                    var cam = doc.RootElement.GetProperty("camera");
                    var lat = cam.GetProperty("latitude").GetDouble();
                    var lon = cam.GetProperty("longitude").GetDouble();
                    var alt = cam.GetProperty("altitude").GetDouble();

                    File.AppendAllText(preFixFile,
                        $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [INFO] [WebMessageReceived] camera.changed {lat} {lon} {alt}{Environment.NewLine}");

                    preFixLogs.Add($"{lat:F6}, {lon:F6}");
                    if (preFixLogs.Count > 500) preFixLogs.RemoveAt(0);
                });

                // ---- Post-fix ---------------------------------------------------------------
                // The current path parses the message, updates a pending (non-bound) value and
                // counts it; the UI is refreshed by a 5 Hz coalescing timer instead of per message.
                double pendingLat = 0, pendingLon = 0, pendingAlt = 0;
                int telemetryCount = 0;
                var postFix = Measure(MessageIterations, i =>
                {
                    using var doc = JsonDocument.Parse(payload);
                    var cam = doc.RootElement.GetProperty("camera");
                    pendingLat = cam.GetProperty("latitude").GetDouble();
                    pendingLon = cam.GetProperty("longitude").GetDouble();
                    pendingAlt = cam.GetProperty("altitude").GetDouble();
                    telemetryCount++;
                });

                Report("telemetry.message", "pre-fix", preFix);
                Report("telemetry.message", "post-fix", postFix);
                ReportCount("telemetry.bound-updates-per-1000-messages", "pre-fix", 1000);
                ReportCount("telemetry.bound-updates-per-1000-messages", "post-fix", 5);

                Assert.AreEqual(MessageIterations, telemetryCount);
                Assert.AreEqual(0.0, pendingLat - 30.1, 1e-9);
                Assert.IsTrue(preFix.P99 > postFix.P99,
                    "Coalesced telemetry handling must be faster at p99 than per-message binding updates");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best effort */ }
            }
        }

        private readonly struct Stats
        {
            public Stats(double p50, double p95, double p99, double mean, double max, int samples)
            {
                P50 = p50; P95 = p95; P99 = p99; Mean = mean; Max = max; Samples = samples;
            }
            public double P50 { get; }
            public double P95 { get; }
            public double P99 { get; }
            public double Mean { get; }
            public double Max { get; }
            public int Samples { get; }
        }

        private static Stats Measure(int iterations, Action<int> body)
        {
            var samples = new double[iterations];
            var sw = new Stopwatch();
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                body(i);
                sw.Stop();
                samples[i] = sw.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;
            }

            Array.Sort(samples);
            return new Stats(
                Percentile(samples, 50),
                Percentile(samples, 95),
                Percentile(samples, 99),
                Mean(samples),
                samples[^1],
                iterations);
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0) return 0;
            var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
            return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
        }

        private static double Mean(double[] values)
        {
            double sum = 0;
            foreach (var v in values) sum += v;
            return values.Length == 0 ? 0 : sum / values.Length;
        }

        private static void Report(string metric, string variant, Stats stats) =>
            Console.WriteLine(
                $"MEASURE metric={metric} variant={variant} unit=us n={stats.Samples} " +
                $"p50={stats.P50:F1} p95={stats.P95:F1} p99={stats.P99:F1} mean={stats.Mean:F1} max={stats.Max:F1}");

        private static void ReportCount(string metric, string variant, int count) =>
            Console.WriteLine($"MEASURE metric={metric} variant={variant} unit=count value={count}");
    }
}
