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
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth");
                Directory.CreateDirectory(appData);
                _sessionFilePath = Path.Combine(appData, "temporary_session.json");
            }
        }

        public string SessionFilePath => _sessionFilePath;

        public void SaveSession(IEnumerable<PlaceItem> items)
        {
            try
            {
                var list = new List<PlaceItem>(items);
                var json = JsonSerializer.Serialize(list, _jsonOptions);
                File.WriteAllText(_sessionFilePath, json);
                LoggerService.Instance.LogInfo($"Temporary places session saved ({list.Count} root items) to {_sessionFilePath}");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Failed to save temporary places session: {ex.Message}");
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
