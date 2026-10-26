using System;
using System.IO;
using System.Text.Json;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;

namespace ZEarth.App.Services;

public class AppSettingsModel
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "en";
    public string SceneMode { get; set; } = "3D";
    public string BasemapId { get; set; } = "offline-vector";
    public string TerrainProviderId { get; set; } = "demTerrain";
    public string ScreenshotDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ZEarth");
    // Session credentials must never be serialized to plaintext settings.json.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CesiumIonToken { get; set; } = null;
    public string? CustomTerrainUrl { get; set; } = null;
    // Procedural visual clouds are optional and offline, not live meteorological data.
    public bool ShowClouds { get; set; } = false;
    public bool ShowGraticules { get; set; } = false;
    public bool ShowGraticuleLabels { get; set; } = true;
    public string GridLabelFormat { get; set; } = "DMS";
    public bool ShowKeyParallels { get; set; } = true;
    public bool ShowLongitudeLines { get; set; } = true;
    public bool ShowLatitudeLines { get; set; } = true;
    public double LongitudeIntervalDegrees { get; set; } = 0.0;
    public double LatitudeIntervalDegrees { get; set; } = 0.0;
    public bool ShowUtmZones { get; set; } = false;
    // Offline-first: enabling Esri hosted borders is an explicit user choice.
    public bool ShowBorders { get; set; } = false;

    // Coordinate & Status Bar Readouts (Addendum 02)
    public bool ShowCenterReadout { get; set; } = true;
    public bool ShowCursorReadout { get; set; } = true;
    public string CoordinateFormat { get; set; } = "DecimalDegrees";
    public int CoordinatePrecision { get; set; } = 4;
    public string EyeAltitudeUnit { get; set; } = "AutoMetric";
    public bool ShowCenterCrosshair { get; set; } = false;

    // Scenes & Credits (Addendum 02)
    public bool ShowSceneLocationsOnMap { get; set; } = false;
    public string CreditsDisplayMode { get; set; } = "Compact";
    public bool ShowOptionalProviderLogos { get; set; } = true;
    public double ZoomSensitivity { get; set; } = 1.0;

    // Units
    public string UnitProfile { get; set; } = "Metric";
    public string DistanceUnit { get; set; } = "AutoMetric";
    public string AreaUnit { get; set; } = "AutoMetric";
    public string ElevationUnit { get; set; } = "Meters";

    // Snapping
    public bool SnappingEnabled { get; set; } = true;
    public string SnappingMode { get; set; } = "GroundDistance";
    public double SnapTolerancePixels { get; set; } = 12.0;
    public double SnapGroundToleranceMeters { get; set; } = 5.0;
    public bool SnapVertices { get; set; } = true;
    public bool SnapMidpoints { get; set; } = true;
    public bool SnapEdges { get; set; } = true;
    public bool SnapIntersections { get; set; } = true;

    // CRS & Navigation settings (Addendum 07 & RC2)
    public int MapCrsEpsg { get; set; } = 4326;
    public List<int> FavoriteCrsList { get; set; } = new() { 4326, 3857, 32636, 22992 };
    public string CompassMode { get; set; } = "cardinal";

    // Credentials / Auth
    [System.Text.Json.Serialization.JsonIgnore]
    public string? EsriApiKey { get; set; } = null;
    public string? CustomTileServerUrl { get; set; } = null;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DefaultBasemap { get => BasemapId; set => BasemapId = value; }

    [System.Text.Json.Serialization.JsonIgnore]
    public double SnappingTolerancePixels { get => SnapTolerancePixels; set => SnapTolerancePixels = value; }

    [System.Text.Json.Serialization.JsonIgnore]
    public double GroundToleranceMeters { get => SnapGroundToleranceMeters; set => SnapGroundToleranceMeters = value; }
}

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;
    public static AppSettingsModel Current => _instance.Value.Settings;

    private readonly string _settingsPath;
    public AppSettingsModel Settings { get; private set; }

    public void Save() => SaveSettings();

    private SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Settings = LoadSettings();
        ApplyToDomain();
    }

    private AppSettingsModel LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var model = JsonSerializer.Deserialize<AppSettingsModel>(json);
                if (model != null)
                {
                    // Ignore insecure tokens left by older settings versions.
                    model.CesiumIonToken = Environment.GetEnvironmentVariable("ZEARTH_CESIUM_ION_TOKEN");
                    model.EsriApiKey = Environment.GetEnvironmentVariable("ZEARTH_ESRI_API_KEY");
                    return model;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogWarning($"Failed to read settings from {_settingsPath}: {ex.Message}");
        }
        return new AppSettingsModel
        {
            CesiumIonToken = Environment.GetEnvironmentVariable("ZEARTH_CESIUM_ION_TOKEN"),
            EsriApiKey = Environment.GetEnvironmentVariable("ZEARTH_ESRI_API_KEY")
        };
    }

    public void SaveSettings()
    {
        try
        {
            // Settings is the authoritative edited model. Implicit domain synchronization used
            // to overwrite freshly selected Options values with stale singleton state.
            // Domain-origin changes must explicitly synchronize into the model at their source.
            AppSettingsStore.Save(_settingsPath, Settings);
            LoggerService.Instance.LogInfo($"Saved user settings to {_settingsPath}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Failed to save settings to {_settingsPath}", ex);
        }
    }

    public void ApplyToDomain()
    {
        // Apply Units
        if (Enum.TryParse<UnitProfile>(Settings.UnitProfile, out var prof)) UnitService.Instance.ActiveProfile = prof;
        if (Enum.TryParse<DistanceUnit>(Settings.DistanceUnit, out var dUnit)) UnitService.Instance.SelectedDistanceUnit = dUnit;
        if (Enum.TryParse<AreaUnit>(Settings.AreaUnit, out var aUnit)) UnitService.Instance.SelectedAreaUnit = aUnit;
        if (Enum.TryParse<ElevationUnit>(Settings.ElevationUnit, out var eUnit)) UnitService.Instance.SelectedElevationUnit = eUnit;

        // Apply Snapping
        SnappingSettings.Instance.IsEnabled = Settings.SnappingEnabled;
        // Z EARTH uses a real-world geodetic tolerance. Keep the legacy pixel
        // field only for backwards-compatible settings-file deserialization.
        SnappingSettings.Instance.Mode = SnappingMode.GroundDistance;
        SnappingSettings.Instance.TolerancePixels = Math.Clamp(Settings.SnapTolerancePixels, 4.0, 40.0);
        SnappingSettings.Instance.GroundToleranceMeters = Math.Max(0.1, Settings.SnapGroundToleranceMeters);
        SnappingSettings.Instance.SnapToVertices = Settings.SnapVertices;
        SnappingSettings.Instance.SnapToMidpoints = Settings.SnapMidpoints;
        SnappingSettings.Instance.SnapToEdges = Settings.SnapEdges;
        SnappingSettings.Instance.SnapToIntersections = Settings.SnapIntersections;
    }

    public void SyncFromDomain()
    {
        Settings.UnitProfile = UnitService.Instance.ActiveProfile.ToString();
        Settings.DistanceUnit = UnitService.Instance.SelectedDistanceUnit.ToString();
        Settings.AreaUnit = UnitService.Instance.SelectedAreaUnit.ToString();
        Settings.ElevationUnit = UnitService.Instance.SelectedElevationUnit.ToString();

        Settings.SnappingEnabled = SnappingSettings.Instance.IsEnabled;
        Settings.SnappingMode = SnappingSettings.Instance.Mode.ToString();
        Settings.SnapTolerancePixels = SnappingSettings.Instance.TolerancePixels;
        Settings.SnapGroundToleranceMeters = SnappingSettings.Instance.GroundToleranceMeters;
        Settings.SnapVertices = SnappingSettings.Instance.SnapToVertices;
        Settings.SnapMidpoints = SnappingSettings.Instance.SnapToMidpoints;
        Settings.SnapEdges = SnappingSettings.Instance.SnapToEdges;
        Settings.SnapIntersections = SnappingSettings.Instance.SnapToIntersections;
    }
}

/// <summary>
/// Explicit, testable persistence boundary: only the supplied settings model
/// is serialized, never unrelated singleton/domain state.
/// </summary>
public static class AppSettingsStore
{
    public static void Save(string path, AppSettingsModel settings)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Settings path required", nameof(path));
        ArgumentNullException.ThrowIfNull(settings);
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))
                     ?? throw new IOException("No settings directory");
        Directory.CreateDirectory(folder);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        // Same-directory replacement retains the last valid file if writing fails.
        File.Move(temp, path, overwrite: true);
    }
}
