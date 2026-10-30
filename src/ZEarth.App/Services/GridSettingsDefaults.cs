using System;
using System.Globalization;
using System.Linq;

namespace ZEarth.App.Services;

/// <summary>
/// One authoritative definition of the Map Grid defaults. A fresh installation
/// and the explicit "Reset Map Grid to defaults" action must produce identical
/// values; the settings dialog and the Cesium engine both read from here.
/// </summary>
public static class GridSettingsDefaults
{
    public const bool ShowKeyParallels = true;
    public const bool ShowLongitudeLines = true;
    public const bool ShowLatitudeLines = true;
    public const bool ShowUtmZones = false;
    public const string GridLabelFormat = "DD";
    public const double LongitudeIntervalDegrees = 0.0;
    public const double LatitudeIntervalDegrees = 0.0;
    public const string UtmLabelMode = "GridZone";
    public const string UtmLabelPlacement = "Center";
    public const string GridLabelSize = "Large";
    public const string GridMajorColor = "#FF5252";
    public const string GridLongitudeColor = "#3BA9FF";
    public const string GridLatitudeColor = "#3AD17B";
    public const string GridLabelColor = "#E8E8E8";
    public const string GridUtmColor = "#FFC107";

    public static readonly string[] LabelSizes = { "Small", "Medium", "Large", "ExtraLarge" };
    public static readonly string[] LabelFormats = { "Degrees", "DM", "DMS", "DD" };
    public static readonly string[] UtmLabelModes = { "GridZone", "Hemisphere", "Both" };

    public static void Apply(AppSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ShowKeyParallels = ShowKeyParallels;
        settings.ShowLongitudeLines = ShowLongitudeLines;
        settings.ShowLatitudeLines = ShowLatitudeLines;
        settings.ShowUtmZones = ShowUtmZones;
        settings.GridLabelFormat = GridLabelFormat;
        settings.LongitudeIntervalDegrees = LongitudeIntervalDegrees;
        settings.LatitudeIntervalDegrees = LatitudeIntervalDegrees;
        settings.UtmLabelMode = UtmLabelMode;
        settings.UtmLabelPlacement = UtmLabelPlacement;
        settings.GridLabelSize = GridLabelSize;
        settings.GridMajorColor = GridMajorColor;
        settings.GridLongitudeColor = GridLongitudeColor;
        settings.GridLatitudeColor = GridLatitudeColor;
        settings.GridLabelColor = GridLabelColor;
        settings.GridUtmColor = GridUtmColor;
    }

    /// <summary>
    /// Repairs missing/invalid values from older or hand-edited settings files
    /// without replacing valid user choices. Called only after deserialization.
    /// </summary>
    public static void Normalize(AppSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!LabelFormats.Contains(settings.GridLabelFormat)) settings.GridLabelFormat = GridLabelFormat;
        if (!LabelSizes.Contains(settings.GridLabelSize)) settings.GridLabelSize = GridLabelSize;
        if (!UtmLabelModes.Contains(settings.UtmLabelMode)) settings.UtmLabelMode = UtmLabelMode;
        if (!string.Equals(settings.UtmLabelPlacement, UtmLabelPlacement, StringComparison.OrdinalIgnoreCase))
            settings.UtmLabelPlacement = UtmLabelPlacement;
        settings.LongitudeIntervalDegrees = NormalizeInterval(settings.LongitudeIntervalDegrees, LongitudeIntervalDegrees);
        settings.LatitudeIntervalDegrees = NormalizeInterval(settings.LatitudeIntervalDegrees, LatitudeIntervalDegrees);
        settings.GridMajorColor = GridColor.NormalizeOrFallback(settings.GridMajorColor, GridMajorColor);
        settings.GridLongitudeColor = GridColor.NormalizeOrFallback(settings.GridLongitudeColor, GridLongitudeColor);
        settings.GridLatitudeColor = GridColor.NormalizeOrFallback(settings.GridLatitudeColor, GridLatitudeColor);
        settings.GridLabelColor = GridColor.NormalizeOrFallback(settings.GridLabelColor, GridLabelColor);
        settings.GridUtmColor = GridColor.NormalizeOrFallback(settings.GridUtmColor, GridUtmColor);
    }

    public static double NormalizeInterval(double value, double fallback)
    {
        if (!double.IsFinite(value)) return fallback;
        if (value == 0) return 0; // automatic spacing
        return value is >= 0.05 and <= 60 ? value : fallback;
    }
}

/// <summary>
/// Validation and conversion for the five user-selectable grid colours.
/// Stored values are always opaque #RRGGBB so persistence is stable; Cesium
/// receives an explicit rgba() string with the renderer's own alpha.
/// </summary>
public static class GridColor
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        if (text.Length is not (7 or 9) || text[0] != '#') return false;
        if (!text.Skip(1).All(Uri.IsHexDigit)) return false;
        // Accept #AARRGGBB as well as #RRGGBB and persist the opaque RGB form.
        normalized = text.Length == 9
            ? "#" + text.Substring(3).ToUpperInvariant()
            : text.ToUpperInvariant();
        return true;
    }

    public static string NormalizeOrFallback(string? value, string fallback) =>
        TryNormalize(value, out var normalized) ? normalized : fallback;

    /// <summary>Convert #RRGGBB to a Cesium-compatible rgba() string.</summary>
    public static string ToCssRgba(string? value, double alpha, string fallback = "#FFFFFF")
    {
        var hex = NormalizeOrFallback(value, NormalizeOrFallback(fallback, "#FFFFFF"));
        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var safeAlpha = double.IsFinite(alpha) ? Math.Clamp(alpha, 0.0, 1.0) : 1.0;
        return FormattableString.Invariant($"rgba({r}, {g}, {b}, {safeAlpha:0.##})");
    }
}
