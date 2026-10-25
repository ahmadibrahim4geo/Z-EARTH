using System;

namespace ZEarth.Domain.Settings;

public enum SnappingMode
{
    ScreenPixels,
    GroundDistance
}

/// <summary>
/// Domain model and options for snapping precision, mode, and target types (SN-01).
/// </summary>
public sealed class SnappingSettings
{
    private static readonly Lazy<SnappingSettings> _instance = new(() => new SnappingSettings());
    public static SnappingSettings Instance => _instance.Value;

    public bool IsEnabled { get; set; } = true;
    public SnappingMode Mode { get; set; } = SnappingMode.GroundDistance;

    private double _tolerancePixels = 12.0;
    private double _groundToleranceMeters = 5.0;

    /// <summary>
    /// Screen cursor tolerance in pixels (default 12 px, range 4 to 40 px).
    /// </summary>
    public double TolerancePixels
    {
        get => _tolerancePixels;
        set => _tolerancePixels = Math.Clamp(value, 4.0, 40.0);
    }

    public double ScreenTolerancePixels
    {
        get => TolerancePixels;
        set => TolerancePixels = value;
    }

    /// <summary>
    /// Ground tolerance in meters (e.g. 5.0m, range 1 to 50 meters).
    /// </summary>
    public double GroundToleranceMeters
    {
        get => _groundToleranceMeters;
        set => _groundToleranceMeters = Math.Clamp(value, 1.0, 50.0);
    }

    public bool SnapToVertices { get; set; } = true;
    public bool SnapToMidpoints { get; set; } = true;
    public bool SnapToEdges { get; set; } = true;
    public bool SnapToIntersections { get; set; } = true;

    public event EventHandler? SettingsChanged;

    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
