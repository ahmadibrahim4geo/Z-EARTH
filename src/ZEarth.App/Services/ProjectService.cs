using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using ZEarth.Domain;
using ZEarth.Domain.Persistence;

namespace ZEarth.App.Services
{
    public interface IProjectService
    {
        ProjectDocument CurrentProject { get; set; }
        string? CurrentFilePath { get; set; }
        bool IsModified { get; set; }
        bool HasPendingCrashRecovery { get; }
        string AutoSavePath { get; }

        event EventHandler? ProjectLoaded;
        event EventHandler? ProjectSaved;
        event EventHandler? ModifiedChanged;

        ProjectDocument CreateNew();
        Task<bool> SaveAsync(string? filePath = null);
        Task<ProjectDocument?> LoadAsync(string filePath);
        Task<bool> AutoSaveAsync();
        Task<ProjectDocument?> TryRecoverAutoSaveAsync();
        void ClearActiveSession();
    }

    public sealed class ProjectService : IProjectService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _recoveryDir;
        private readonly string _autoSavePath;
        private readonly string _sessionActivePath;
        private bool _isModified = false;

        // Serializes all writes so scheduled autosave and a manual save can never race on the same
        // bundle / SQLite database and produce "file is being used by another process" (RC-7).
        private readonly System.Threading.SemaphoreSlim _saveGate = new(1, 1);

        public ProjectDocument CurrentProject { get; set; } = ProjectDocument.CreateDefault();
        public string? CurrentFilePath { get; set; }

        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified != value)
                {
                    _isModified = value;
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool HasPendingCrashRecovery { get; private set; }
        public string AutoSavePath => _autoSavePath;

        public event EventHandler? ProjectLoaded;
        public event EventHandler? ProjectSaved;
        public event EventHandler? ModifiedChanged;

        public ProjectService()
        {
            _recoveryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth", "recovery");
            Directory.CreateDirectory(_recoveryDir);
            _autoSavePath = Path.Combine(_recoveryDir, "autosave.zearth");
            _sessionActivePath = Path.Combine(_recoveryDir, "session.active");

            // Check for previous abnormal termination
            if (File.Exists(_sessionActivePath) && File.Exists(_autoSavePath))
            {
                HasPendingCrashRecovery = true;
            }

            // Mark current session as active
            try
            {
                File.WriteAllText(_sessionActivePath, $"{DateTime.UtcNow:O}|PID:{Environment.ProcessId}");
            }
            catch { }
        }

        public ProjectDocument CreateNew()
        {
            CurrentProject = ProjectDocument.CreateDefault();
            CurrentFilePath = null;
            IsModified = false;
            ProjectLoaded?.Invoke(this, EventArgs.Empty);
            return CurrentProject;
        }

        /// <summary>
        /// Saves project atomically to a versioned .zearth ZIP bundle containing
        /// an authoritative SQLite project.db database, manifest.json, and relative assets (ADR-004, ADR-012).
        /// </summary>
        public async Task<bool> SaveAsync(string? filePath = null)
        {
            await _saveGate.WaitAsync();
            try
            {
                return await SaveCoreAsync(filePath);
            }
            finally
            {
                _saveGate.Release();
            }
        }

        private static async Task WithIoRetryAsync(Func<Task> operation, int attempts = 4, int delayMs = 120)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (IOException) when (attempt < attempts)
                {
                    await Task.Delay(delayMs * attempt);
                }
            }
        }

        private static void WithIoRetry(Action operation, int attempts = 4, int delayMs = 120)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    operation();
                    return;
                }
                catch (IOException) when (attempt < attempts)
                {
                    System.Threading.Thread.Sleep(delayMs * attempt);
                }
            }
        }

        private static void MoveReplacing(string source, string destination)
        {
            WithIoRetry(() =>
            {
                if (File.Exists(destination))
                {
                    File.Move(source, destination, overwrite: true);
                }
                else
                {
                    File.Move(source, destination);
                }
            });
        }

        private async Task<bool> SaveCoreAsync(string? filePath)
        {
            var targetPath = filePath ?? CurrentFilePath;
            if (string.IsNullOrEmpty(targetPath)) return false;

            var tempZipPath = targetPath + ".tmp_" + Guid.NewGuid().ToString("N");
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"zearth_{Guid.NewGuid():N}.sqlite");

            try
            {
                CurrentProject.ModifiedDate = DateTime.UtcNow;

                // 1. Generate SQLite project database
                WithIoRetry(() => SqliteProjectStorage.SaveToDatabase(CurrentProject, tempDbPath));

                var manifest = new
                {
                    format = "zearth",
                    formatVersion = "1.0.0",
                    application = "Z EARTH Desktop GIS",
                    author = "Ahmad Ibrahim",
                    createdDate = CurrentProject.CreatedDate.ToString("O"),
                    modifiedDate = CurrentProject.ModifiedDate.ToString("O"),
                    entryPoint = "project.db",
                    schemaType = "sqlite",
                    crs = CurrentProject.Crs,
                    sceneMode = CurrentProject.SceneMode.ToString()
                };

                var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
                var projectJson = JsonSerializer.Serialize(CurrentProject, _jsonOptions);

                // Write to temp ZIP archive
                using (var zipStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    // 1. Add manifest.json
                    var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(manifestEntry.Open()))
                    {
                        await writer.WriteAsync(manifestJson);
                    }

                    // 2. Add authoritative project.db (SQLite database)
                    var dbEntry = archive.CreateEntry("project.db", CompressionLevel.Optimal);
                    using (var dbFs = File.OpenRead(tempDbPath))
                    using (var dbEntryStream = dbEntry.Open())
                    {
                        await dbFs.CopyToAsync(dbEntryStream);
                    }

                    // 3. Add project.json for export/interoperability
                    var projectEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(projectEntry.Open()))
                    {
                        await writer.WriteAsync(projectJson);
                    }

                    // 4. Ensure assets directory entry exists
                    archive.CreateEntry("assets/");
                }

                // Atomic replacement: Move tempZipPath to targetPath
                MoveReplacing(tempZipPath, targetPath);

                CurrentFilePath = targetPath;
                IsModified = false;
                ProjectSaved?.Invoke(this, EventArgs.Empty);
                LoggerService.Instance.LogInfo($"Project atomically saved to .zearth bundle with SQLite database: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to save project to {targetPath}", ex);
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }
                return false;
            }
            finally
            {
                if (File.Exists(tempDbPath))
                {
                    try { File.Delete(tempDbPath); } catch { }
                }
            }
        }

        /// <summary>
        /// Loads project supporting SQLite project.db in .zearth ZIP archives,
        /// fallback project.json entries, and legacy prototype JSON files (ADR-004, ADR-012).
        /// </summary>
        public async Task<ProjectDocument?> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                byte[] header = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fs.ReadExactlyAsync(header.AsMemory(0, 4));
                }

                ProjectDocument? doc = null;

                // Check magic bytes: 0x50 0x4B 0x03 0x04 ("PK..") -> ZIP Bundle
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    using var zipStream = File.OpenRead(filePath);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    // Check for authoritative SQLite database entry
                    var dbEntry = archive.GetEntry("project.db") ?? archive.GetEntry("project.sqlite");
                    if (dbEntry != null)
                    {
                        var tempExtractDb = Path.Combine(Path.GetTempPath(), $"zearth_load_{Guid.NewGuid():N}.sqlite");
                        try
                        {
                            using (var entryStream = dbEntry.Open())
                            using (var tempFs = new FileStream(tempExtractDb, FileMode.Create, FileAccess.Write))
                            {
                                await entryStream.CopyToAsync(tempFs);
                            }

                            doc = SqliteProjectStorage.LoadFromDatabase(tempExtractDb);
                            LoggerService.Instance.LogInfo($"Loaded project from SQLite project.db inside: {filePath}");
                        }
                        finally
                        {
                            if (File.Exists(tempExtractDb))
                            {
                                try { File.Delete(tempExtractDb); } catch { }
                            }
                        }
                    }
                    else
                    {
                        // Fallback to project.json inside ZIP if project.db not present
                        var projectEntry = archive.GetEntry("project.json");
                        if (projectEntry != null)
                        {
                            using var reader = new StreamReader(projectEntry.Open());
                            var json = await reader.ReadToEndAsync();
                            doc = JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions);
                            LoggerService.Instance.LogInfo($"Loaded project from legacy project.json inside: {filePath}");
                        }
                    }
                }
                // Check if legacy prototype JSON format (starts with '{' 0x7B)
                else if (header[0] == 0x7B)
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    doc = JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions);
                    LoggerService.Instance.LogInfo($"Migrated legacy prototype JSON project to memory: {filePath}");
                }

                if (doc != null)
                {
                    CurrentProject = doc;
                    CurrentFilePath = filePath;
                    IsModified = false;
                    ProjectLoaded?.Invoke(this, EventArgs.Empty);
                    LoggerService.Instance.LogInfo($"Project successfully loaded from {filePath}");
                    return doc;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to load project from {filePath}", ex);
            }

            return null;
        }

        public async Task<bool> AutoSaveAsync()
        {
            await _saveGate.WaitAsync();
            try
            {
                return await AutoSaveCoreAsync();
            }
            finally
            {
                _saveGate.Release();
            }
        }

        private async Task<bool> AutoSaveCoreAsync()
        {
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"zearth_auto_{Guid.NewGuid():N}.sqlite");
            try
            {
                WithIoRetry(() => SqliteProjectStorage.SaveToDatabase(CurrentProject, tempDbPath));

                var manifest = new
                {
                    format = "zearth",
                    formatVersion = "1.0.0",
                    application = "Z EARTH Desktop GIS",
                    author = "Ahmad Ibrahim",
                    createdDate = CurrentProject.CreatedDate.ToString("O"),
                    modifiedDate = DateTime.UtcNow.ToString("O"),
                    entryPoint = "project.db",
                    schemaType = "sqlite",
                    isAutoSave = true
                };

                var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
                var projectJson = JsonSerializer.Serialize(CurrentProject, _jsonOptions);

                await WithIoRetryAsync(async () =>
                {
                    using var zipStream = new FileStream(_autoSavePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

                    var mEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
                    using (var w = new StreamWriter(mEntry.Open())) await w.WriteAsync(manifestJson);

                    var dbEntry = archive.CreateEntry("project.db", CompressionLevel.Fastest);
                    using (var dbFs = File.OpenRead(tempDbPath))
                    using (var dbEntryStream = dbEntry.Open())
                    {
                        await dbFs.CopyToAsync(dbEntryStream);
                    }

                    var pEntry = archive.CreateEntry("project.json", CompressionLevel.Fastest);
                    using (var w = new StreamWriter(pEntry.Open())) await w.WriteAsync(projectJson);
                });

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Auto-save failed: {ex.Message}");
                return false;
            }
            finally
            {
                if (File.Exists(tempDbPath))
                {
                    try { File.Delete(tempDbPath); } catch { }
                }
            }
        }

        public async Task<ProjectDocument?> TryRecoverAutoSaveAsync()
        {
            if (!File.Exists(_autoSavePath)) return null;

            try
            {
                return await LoadAsync(_autoSavePath);
            }
            catch
            {
                return null;
            }
        }

        public void ClearActiveSession()
        {
            try
            {
                if (File.Exists(_sessionActivePath)) File.Delete(_sessionActivePath);
                if (File.Exists(_autoSavePath)) File.Delete(_autoSavePath);
            }
            catch { }
        }
    }
}
