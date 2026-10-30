using System;
using System.IO;
using System.Text.Json;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;

namespace ZEarth.App.Services;

public class AppSettingsModel
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "en";
    public string SceneMode { get; set; } = "3D";
    public string BasemapId { get; set; } = ImageryRegistry.ProviderEsriHybrid;
    public string TerrainProviderId { get; set; } = "demTerrain";
    public string? LocalTerrainRasterPath { get; set; } = null;
    // Numeric elevation rasters are normalized to metres before Cesium sees them.
    // Keep the explicit unit instead of guessing from a filename or provider label.
    public string LocalTerrainVerticalUnit { get; set; } = "Meters";
    public string ScreenshotDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ZEarth");
    // Session credentials must never be serialized to plaintext settings.json.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CesiumIonToken { get; set; } = null;
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CopernicusAccessToken { get; set; } = null;
    public string? CustomTerrainUrl { get; set; } = null;
    // Procedural visual clouds are optional and offline, not live meteorological data.
    public bool ShowClouds { get; set; } = false;
    public bool ShowGraticules { get; set; } = false;
    // Retained for settings-file compatibility. The Map Grid UI no longer exposes
    // a general label switch: labels follow each enabled grid component.
    public bool ShowGraticuleLabels { get; set; } = true;
    public string GridLabelFormat { get; set; } = GridSettingsDefaults.GridLabelFormat;
    public bool ShowKeyParallels { get; set; } = GridSettingsDefaults.ShowKeyParallels;
    public bool ShowLongitudeLines { get; set; } = GridSettingsDefaults.ShowLongitudeLines;
    public bool ShowLatitudeLines { get; set; } = GridSettingsDefaults.ShowLatitudeLines;
    public double LongitudeIntervalDegrees { get; set; } = GridSettingsDefaults.LongitudeIntervalDegrees;
    public double LatitudeIntervalDegrees { get; set; } = GridSettingsDefaults.LatitudeIntervalDegrees;
    public bool ShowUtmZones { get; set; } = GridSettingsDefaults.ShowUtmZones;
    public string UtmLabelMode { get; set; } = GridSettingsDefaults.UtmLabelMode;
    public string UtmLabelPlacement { get; set; } = GridSettingsDefaults.UtmLabelPlacement;
    public string GridLabelSize { get; set; } = GridSettingsDefaults.GridLabelSize;
    public string GridMajorColor { get; set; } = GridSettingsDefaults.GridMajorColor;
    public string GridLongitudeColor { get; set; } = GridSettingsDefaults.GridLongitudeColor;
    public string GridLatitudeColor { get; set; } = GridSettingsDefaults.GridLatitudeColor;
    public string GridLabelColor { get; set; } = GridSettingsDefaults.GridLabelColor;
    public string GridUtmColor { get; set; } = GridSettingsDefaults.GridUtmColor;
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
        // Automated/native test hosts may opt into a process-scoped settings directory.
        // Production keeps the established LocalApplicationData location. This prevents
        // test runs from reading or overwriting a real user's preferences.
        var configuredDir = Environment.GetEnvironmentVariable("ZEARTH_SETTINGS_DIRECTORY");
        var dir = string.IsNullOrWhiteSpace(configuredDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth")
            : Path.GetFullPath(configuredDir);
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
                    model.CopernicusAccessToken = Environment.GetEnvironmentVariable("ZEARTH_COPERNICUS_ACCESS_TOKEN");
                    model.EsriApiKey = Environment.GetEnvironmentVariable("ZEARTH_ESRI_API_KEY");
                    NormalizeBasemap(model);
                    GridSettingsDefaults.Normalize(model);
                    return model;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogWarning($"Failed to read settings from {_settingsPath}: {ex.Message}");
        }
        var defaults = new AppSettingsModel
        {
            CesiumIonToken = Environment.GetEnvironmentVariable("ZEARTH_CESIUM_ION_TOKEN"),
            CopernicusAccessToken = Environment.GetEnvironmentVariable("ZEARTH_COPERNICUS_ACCESS_TOKEN"),
            EsriApiKey = Environment.GetEnvironmentVariable("ZEARTH_ESRI_API_KEY")
        };
        NormalizeBasemap(defaults);
        return defaults;
    }

    private static void NormalizeBasemap(AppSettingsModel model)
    {
        var configured = model.BasemapId?.Trim();
        model.BasemapId = ImageryRegistry.Providers.Any(provider =>
                string.Equals(provider.Id, configured, StringComparison.OrdinalIgnoreCase))
            ? configured!
            : ImageryRegistry.ProviderEsriHybrid;
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
