using System;
using System.Collections.Generic;

namespace ZEarth.Domain.Units;

public enum UnitProfile
{
    Metric,
    Imperial,
    Custom
}

public enum DistanceUnit
{
    AutoMetric,
    AutoImperial,
    Meters,
    Kilometers,
    Centimeters,
    Millimeters,
    Feet,
    Inches,
    Yards,
    Miles,
    NauticalMiles
}

public enum AreaUnit
{
    AutoMetric,
    AutoImperial,
    SquareMeters,
    SquareKilometers,
    Hectares,
    EgyptianFeddan,
    SquareCentimeters,
    SquareMillimeters,
    SquareFeet,
    SquareInches,
    SquareYards,
    Acres,
    SquareMiles
}

public enum ElevationUnit
{
    Meters,
    Kilometers,
    Centimeters,
    Millimeters,
    Feet,
    Inches
}

/// <summary>
/// Domain-wide, dimensionally sound Unit of Measurement service (U-01).
/// All calculations are canonically stored in SI units (meters and square meters) on the WGS84 ellipsoid.
/// Conversions are performed strictly for display, telemetry, and export.
/// </summary>
public sealed class UnitService
{
    private static readonly Lazy<UnitService> _instance = new(() => new UnitService());
    public static UnitService Instance => _instance.Value;

    // Exact SI conversion constants
    public const double MetersPerInch = 0.0254;
    public const double MetersPerFoot = 0.3048;
    public const double MetersPerYard = 0.9144;
    public const double MetersPerMile = 1609.344;
    public const double MetersPerNauticalMile = 1852.0;

    public const double SqMetersPerHectare = 10000.0;
    public const double SqMetersPerAcre = 4046.8564224; // 43,560 sq ft * (0.3048)^2
    /// <summary>
    /// Egyptian Feddan (official Survey of Egypt standard: 24 Qirat = 4200.8335 m²).
    /// </summary>
    public const double SqMetersPerEgyptianFeddan = 4200.8335;

    public UnitProfile ActiveProfile { get; set; } = UnitProfile.Metric;
    public DistanceUnit SelectedDistanceUnit { get; set; } = DistanceUnit.AutoMetric;
    public AreaUnit SelectedAreaUnit { get; set; } = AreaUnit.AutoMetric;
    public ElevationUnit SelectedElevationUnit { get; set; } = ElevationUnit.Meters;

    public DistanceUnit ActiveDistanceUnit { get => SelectedDistanceUnit; set => SelectedDistanceUnit = value; }
    public AreaUnit ActiveAreaUnit { get => SelectedAreaUnit; set => SelectedAreaUnit = value; }
    public ElevationUnit ActiveElevationUnit { get => SelectedElevationUnit; set => SelectedElevationUnit = value; }

    public event EventHandler? UnitsChanged;

    public void NotifyUnitsChanged()
    {
        UnitsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyProfile(UnitProfile profile)
    {
        ActiveProfile = profile;
        if (profile == UnitProfile.Metric)
        {
            SelectedDistanceUnit = DistanceUnit.AutoMetric;
            SelectedAreaUnit = AreaUnit.AutoMetric;
            SelectedElevationUnit = ElevationUnit.Meters;
        }
        else if (profile == UnitProfile.Imperial)
        {
            SelectedDistanceUnit = DistanceUnit.AutoImperial;
            SelectedAreaUnit = AreaUnit.AutoImperial;
            SelectedElevationUnit = ElevationUnit.Feet;
        }
        NotifyUnitsChanged();
    }

    #region Distance Conversions

    public double ConvertDistance(double meters, DistanceUnit targetUnit)
    {
        return targetUnit switch
        {
            DistanceUnit.Millimeters => meters * 1000.0,
            DistanceUnit.Centimeters => meters * 100.0,
            DistanceUnit.Meters => meters,
            DistanceUnit.Kilometers => meters / 1000.0,
            DistanceUnit.Inches => meters / MetersPerInch,
            DistanceUnit.Feet => meters / MetersPerFoot,
            DistanceUnit.Yards => meters / MetersPerYard,
            DistanceUnit.Miles => meters / MetersPerMile,
            DistanceUnit.NauticalMiles => meters / MetersPerNauticalMile,
            DistanceUnit.AutoMetric => meters >= 1000.0 ? meters / 1000.0 : meters,
            DistanceUnit.AutoImperial => meters >= MetersPerMile ? meters / MetersPerMile : meters / MetersPerFoot,
            _ => meters
        };
    }

    public string GetDistanceUnitSymbol(DistanceUnit unit, double meters = 0)
    {
        return unit switch
        {
            DistanceUnit.Millimeters => "mm",
            DistanceUnit.Centimeters => "cm",
            DistanceUnit.Meters => "m",
            DistanceUnit.Kilometers => "km",
            DistanceUnit.Inches => "in",
            DistanceUnit.Feet => "ft",
            DistanceUnit.Yards => "yd",
            DistanceUnit.Miles => "mi",
            DistanceUnit.NauticalMiles => "nmi",
            DistanceUnit.AutoMetric => meters >= 1000.0 ? "km" : "m",
            DistanceUnit.AutoImperial => meters >= MetersPerMile ? "mi" : "ft",
            _ => "m"
        };
    }

    public string FormatDistance(double meters, DistanceUnit? unit = null, int decimals = 2)
    {
        var targetUnit = unit ?? SelectedDistanceUnit;
        if (double.IsNaN(meters) || double.IsInfinity(meters)) return "0.00 m";

        if (targetUnit == DistanceUnit.AutoMetric)
        {
            if (meters >= 1000.0)
                return $"{meters / 1000.0:F3} km ({meters:F1} m)";
            if (meters < 1.0)
                return $"{meters * 100.0:F1} cm";
            return $"{meters:F2} m";
        }

        if (targetUnit == DistanceUnit.AutoImperial)
        {
            if (meters >= MetersPerMile)
            {
                double miles = meters / MetersPerMile;
                double feet = meters / MetersPerFoot;
                return $"{miles:F3} mi ({feet:F0} ft)";
            }
            double ft = meters / MetersPerFoot;
            return $"{ft:F2} ft";
        }

        double converted = ConvertDistance(meters, targetUnit);
        string symbol = GetDistanceUnitSymbol(targetUnit, meters);
        return $"{converted.ToString($"F{decimals}")} {symbol}";
    }

    #endregion

    #region Area Conversions

    public double ConvertArea(double sqMeters, AreaUnit targetUnit)
    {
        return targetUnit switch
        {
            AreaUnit.SquareMillimeters => sqMeters * 1_000_000.0,
            AreaUnit.SquareCentimeters => sqMeters * 10_000.0,
            AreaUnit.SquareMeters => sqMeters,
            AreaUnit.SquareKilometers => sqMeters / 1_000_000.0,
            AreaUnit.Hectares => sqMeters / SqMetersPerHectare,
            AreaUnit.EgyptianFeddan => sqMeters / SqMetersPerEgyptianFeddan,
            AreaUnit.SquareInches => sqMeters / (MetersPerInch * MetersPerInch),
            AreaUnit.SquareFeet => sqMeters / (MetersPerFoot * MetersPerFoot),
            AreaUnit.SquareYards => sqMeters / (MetersPerYard * MetersPerYard),
            AreaUnit.Acres => sqMeters / SqMetersPerAcre,
            AreaUnit.SquareMiles => sqMeters / (MetersPerMile * MetersPerMile),
            AreaUnit.AutoMetric => sqMeters >= 1_000_000.0 ? sqMeters / 1_000_000.0 : (sqMeters >= 10_000.0 ? sqMeters / SqMetersPerHectare : sqMeters),
            AreaUnit.AutoImperial => sqMeters >= (MetersPerMile * MetersPerMile) ? sqMeters / (MetersPerMile * MetersPerMile) : (sqMeters >= SqMetersPerAcre ? sqMeters / SqMetersPerAcre : sqMeters / (MetersPerFoot * MetersPerFoot)),
            _ => sqMeters
        };
    }

    public string GetAreaUnitSymbol(AreaUnit unit, double sqMeters = 0)
    {
        return unit switch
        {
            AreaUnit.SquareMillimeters => "mm²",
            AreaUnit.SquareCentimeters => "cm²",
            AreaUnit.SquareMeters => "m²",
            AreaUnit.SquareKilometers => "km²",
            AreaUnit.Hectares => "ha",
            AreaUnit.EgyptianFeddan => "fed",
            AreaUnit.SquareInches => "in²",
            AreaUnit.SquareFeet => "ft²",
            AreaUnit.SquareYards => "yd²",
            AreaUnit.Acres => "ac",
            AreaUnit.SquareMiles => "mi²",
            AreaUnit.AutoMetric => sqMeters >= 1_000_000.0 ? "km²" : (sqMeters >= 10_000.0 ? "ha" : "m²"),
            AreaUnit.AutoImperial => sqMeters >= (MetersPerMile * MetersPerMile) ? "mi²" : (sqMeters >= SqMetersPerAcre ? "ac" : "ft²"),
            _ => "m²"
        };
    }

    public string FormatArea(double sqMeters, AreaUnit? unit = null, int decimals = 2)
    {
        var targetUnit = unit ?? SelectedAreaUnit;
        if (double.IsNaN(sqMeters) || double.IsInfinity(sqMeters)) return "0.00 m²";

        if (targetUnit == AreaUnit.AutoMetric)
        {
            if (sqMeters >= 1_000_000.0)
            {
                double kmSq = sqMeters / 1_000_000.0;
                double ha = sqMeters / SqMetersPerHectare;
                return $"{kmSq:F3} km² ({ha:F1} ha)";
            }
            if (sqMeters >= 10_000.0)
            {
                double ha = sqMeters / SqMetersPerHectare;
                double fed = sqMeters / SqMetersPerEgyptianFeddan;
                return $"{sqMeters:F1} m² ({ha:F2} ha / {fed:F2} fed)";
            }
            return $"{sqMeters:F2} m²";
        }

        if (targetUnit == AreaUnit.AutoImperial)
        {
            if (sqMeters >= (MetersPerMile * MetersPerMile))
            {
                double miSq = sqMeters / (MetersPerMile * MetersPerMile);
                double ac = sqMeters / SqMetersPerAcre;
                return $"{miSq:F3} mi² ({ac:F1} ac)";
            }
            if (sqMeters >= SqMetersPerAcre)
            {
                double ac = sqMeters / SqMetersPerAcre;
                return $"{ac:F2} ac ({sqMeters / (MetersPerFoot * MetersPerFoot):F0} ft²)";
            }
            double ftSq = sqMeters / (MetersPerFoot * MetersPerFoot);
            return $"{ftSq:F2} ft²";
        }

        double converted = ConvertArea(sqMeters, targetUnit);
        string symbol = GetAreaUnitSymbol(targetUnit, sqMeters);
        return $"{converted.ToString($"F{decimals}")} {symbol}";
    }

    #endregion

    #region Elevation Conversions

    public double ConvertElevation(double meters, ElevationUnit targetUnit)
    {
        return targetUnit switch
        {
            ElevationUnit.Millimeters => meters * 1000.0,
            ElevationUnit.Centimeters => meters * 100.0,
            ElevationUnit.Meters => meters,
            ElevationUnit.Kilometers => meters / 1000.0,
            ElevationUnit.Feet => meters / MetersPerFoot,
            ElevationUnit.Inches => meters / MetersPerInch,
            _ => meters
        };
    }

    public string FormatElevation(double meters, ElevationUnit? unit = null, int decimals = 1)
    {
        var targetUnit = unit ?? SelectedElevationUnit;
        if (double.IsNaN(meters) || double.IsInfinity(meters)) return "0.0 m";

        double val = ConvertElevation(meters, targetUnit);
        string symbol = targetUnit switch
        {
            ElevationUnit.Millimeters => "mm",
            ElevationUnit.Centimeters => "cm",
            ElevationUnit.Meters => "m",
            ElevationUnit.Kilometers => "km",
            ElevationUnit.Feet => "ft",
            ElevationUnit.Inches => "in",
            _ => "m"
        };
        return $"{val.ToString($"F{decimals}")} {symbol}";
    }

    #endregion
}
