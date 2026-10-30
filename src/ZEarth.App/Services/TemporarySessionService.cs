using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ZEarth.Domain;

namespace ZEarth.App.Services
{
    /// <summary>
    /// Manages durable session persistence of Temporary Places across app restarts (SIDEBAR-01 Scope, Section 3).
    /// Keeps session store isolated from .zearth project files to prevent accidental modification or promotion.
    /// </summary>
    public class TemporarySessionService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _sessionFilePath;

        public TemporarySessionService(string? customSessionFilePath = null)
        {
            if (!string.IsNullOrEmpty(customSessionFilePath))
            {
                _sessionFilePath = customSessionFilePath;
                var dir = Path.GetDirectoryName(_sessionFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            else
            {
                var overrideDirectory = Environment.GetEnvironmentVariable("ZEARTH_PROFILE_DIRECTORY");
                var appData = string.IsNullOrWhiteSpace(overrideDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth")
                    : Path.GetFullPath(overrideDirectory);
                Directory.CreateDirectory(appData);
                _sessionFilePath = Path.Combine(appData, "temporary_session.json");
            }
        }

        public string SessionFilePath => _sessionFilePath;

        public void SaveSession(IEnumerable<PlaceItem> items) => TrySaveSession(items);

        /// <summary>Write a complete replacement before publishing it, retaining the old session on failure.</summary>
        public bool TrySaveSession(IEnumerable<PlaceItem> items)
        {
            var tempPath = _sessionFilePath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                var list = new List<PlaceItem>(items);
                var json = JsonSerializer.Serialize(list, _jsonOptions);
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                    writer.Write(json);
                File.Move(tempPath, _sessionFilePath, overwrite: true);
                LoggerService.Instance.LogInfo($"Temporary places session saved ({list.Count} root items).");
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Failed to save temporary places session: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        public List<PlaceItem> LoadSession()
        {
            if (!File.Exists(_sessionFilePath)) return new List<PlaceItem>();

            try
            {
                var json = File.ReadAllText(_sessionFilePath);
                var items = JsonSerializer.Deserialize<List<PlaceItem>>(json, _jsonOptions);
                LoggerService.Instance.LogInfo($"Recovered temporary places session from {_sessionFilePath}");
                return items ?? new List<PlaceItem>();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Failed to read temporary places session file: {ex.Message}");
                return new List<PlaceItem>();
            }
        }

        public void ClearSession()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                    LoggerService.Instance.LogInfo("Temporary places session cache cleared.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Failed to clear temporary places session cache: {ex.Message}");
            }
        }
    }
}
