using System;
using System.Collections.Generic;

namespace ZEarth.Domain.Measurement;

public record Measurement3DResult(
    double GeodesicHorizontalDistanceMeters,
    double VerticalDistanceMeters,
    double SlopeDistanceMeters,
    double SlopePercentage,
    double SlopeAngleDegrees,
    string FormattedHorizontalDistance,
    string Formatted3DDistance,
    string FormattedSlope
);

public record PolygonAreaResult(
    double AreaSquareMeters,
    double AreaSquareKilometers,
    double AreaHectares,
    double AreaAcres,
    double PerimeterMeters,
    string FormattedArea,
    string FormattedPerimeter,
    string AlgorithmName
);

/// <summary>
/// High-precision geodetic ellipsoidal measurement service (WGS 84 / GRS80).
/// Implements Vincenty's inverse formula for geodesic distance and spherical/ellipsoidal excess for polygon areas.
/// </summary>
public static class GeodesicMeasurementService
{
    public const string AlgorithmName = "WGS84 Ellipsoidal Geodesic (Vincenty / Authalic Excess)";

    // WGS 84 Ellipsoid Constants
    private const double SemiMajorAxis_A = 6378137.0; // meters
    private const double Flattening_F = 1.0 / 298.257223563;
    private const double SemiMinorAxis_B = SemiMajorAxis_A * (1.0 - Flattening_F); // 6356752.314245 m
    private const double AuthalicRadius_R = 6371007.1809; // m

    private const double Deg2Rad = Math.PI / 180.0;
    private const double Rad2Deg = 180.0 / Math.PI;

    public static double ComputeDistanceMeters(double lat1, double lon1, double lat2, double lon2) => CalculateGeodesicDistance(lat1, lon1, lat2, lon2);

    /// <summary>
    /// Calculates high-precision geodesic distance (in meters) between two WGS84 points using Vincenty's inverse algorithm.
    /// Falls back to Great-Circle Haversine if points are antipodal or nearly so.
    /// </summary>
    public static double CalculateGeodesicDistance(double lat1, double lon1, double lat2, double lon2)
    {
        if (Math.Abs(lat1 - lat2) < 1e-12 && Math.Abs(lon1 - lon2) < 1e-12)
        {
            return 0.0;
        }

        double phi1 = lat1 * Deg2Rad;
        double phi2 = lat2 * Deg2Rad;
        double L = (lon2 - lon1) * Deg2Rad;

        double tanU1 = (1.0 - Flattening_F) * Math.Tan(phi1);
        double cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        double sinU1 = tanU1 * cosU1;

        double tanU2 = (1.0 - Flattening_F) * Math.Tan(phi2);
        double cosU2 = 1.0 / Math.Sqrt(1.0 + tanU2 * tanU2);
        double sinU2 = tanU2 * cosU2;

        double lambda = L;
        double lambdaPrev;
        int maxIterations = 200;
        double cosSqAlpha = 0.0;
        double sinSigma = 0.0;
        double cos2SigmaM = 0.0;
        double cosSigma = 0.0;
        double sigma = 0.0;

        do
        {
            double sinLambda = Math.Sin(lambda);
            double cosLambda = Math.Cos(lambda);

            sinSigma = Math.Sqrt((cosU2 * sinLambda) * (cosU2 * sinLambda) +
                (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda) * (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda));

            if (Math.Abs(sinSigma) < 1e-15)
            {
                return 0.0; // Coincident points
            }

            cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
            sigma = Math.Atan2(sinSigma, cosSigma);

            double sinAlpha = (cosU1 * cosU2 * sinLambda) / sinSigma;
            cosSqAlpha = 1.0 - sinAlpha * sinAlpha;

            if (Math.Abs(cosSqAlpha) < 1e-15)
            {
                cos2SigmaM = 0.0; // Equatorial line
            }
            else
            {
                cos2SigmaM = cosSigma - (2.0 * sinU1 * sinU2) / cosSqAlpha;
            }

            double C = (Flattening_F / 16.0) * cosSqAlpha * (4.0 + Flattening_F * (4.0 - 3.0 * cosSqAlpha));
            lambdaPrev = lambda;
            lambda = L + (1.0 - C) * Flattening_F * sinAlpha *
                (sigma + C * sinSigma * (cos2SigmaM + C * cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)));

        } while (Math.Abs(lambda - lambdaPrev) > 1e-12 && --maxIterations > 0);

        if (maxIterations == 0)
        {
            // Fallback to Haversine
            return CalculateGreatCircleDistance(lat1, lon1, lat2, lon2);
        }

        double uSq = cosSqAlpha * ((SemiMajorAxis_A * SemiMajorAxis_A - SemiMinorAxis_B * SemiMinorAxis_B) / (SemiMinorAxis_B * SemiMinorAxis_B));
        double A = 1.0 + (uSq / 16384.0) * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
        double B = (uSq / 1024.0) * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));

        double deltaSigma = B * sinSigma * (cos2SigmaM + (B / 4.0) * (cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM) -
            (B / 6.0) * cos2SigmaM * (-3.0 + 4.0 * sinSigma * sinSigma) * (-3.0 + 4.0 * cos2SigmaM * cos2SigmaM)));

        double s = SemiMinorAxis_B * A * (sigma - deltaSigma);
        return Math.Max(0.0, s);
    }

    /// <summary>
    /// Kaaba coordinates in Makkah, Saudi Arabia (WGS84).
    /// </summary>
    public const double KaabaLatitude = 21.422487;
    public const double KaabaLongitude = 39.826206;

    /// <summary>
    /// Calculates high-precision geodesic initial bearing / forward azimuth (degrees 0..360) 
    /// from point 1 to point 2 using Vincenty's inverse formula on WGS84 ellipsoid.
    /// </summary>
    public static double CalculateForwardAzimuth(double lat1, double lon1, double lat2, double lon2)
    {
        if (Math.Abs(lat1 - lat2) < 1e-12 && Math.Abs(lon1 - lon2) < 1e-12)
            return 0.0;

        double phi1 = lat1 * Deg2Rad;
        double phi2 = lat2 * Deg2Rad;
        double L = (lon2 - lon1) * Deg2Rad;

        double tanU1 = (1.0 - Flattening_F) * Math.Tan(phi1);
        double cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        double sinU1 = tanU1 * cosU1;

        double tanU2 = (1.0 - Flattening_F) * Math.Tan(phi2);
        double cosU2 = 1.0 / Math.Sqrt(1.0 + tanU2 * tanU2);
        double sinU2 = tanU2 * cosU2;

        double lambda = L;
        double lambdaPrev;
        int maxIterations = 200;
        double cosSqAlpha = 0.0;
        double sinSigma = 0.0;
        double cos2SigmaM = 0.0;
        double cosSigma = 0.0;
        double sigma = 0.0;

        do
        {
            double sinLambda = Math.Sin(lambda);
            double cosLambda = Math.Cos(lambda);

            sinSigma = Math.Sqrt((cosU2 * sinLambda) * (cosU2 * sinLambda) +
                (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda) * (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda));

            if (Math.Abs(sinSigma) < 1e-15)
                return 0.0;

            cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
            sigma = Math.Atan2(sinSigma, cosSigma);

            double sinAlpha = (cosU1 * cosU2 * sinLambda) / sinSigma;
            cosSqAlpha = 1.0 - sinAlpha * sinAlpha;

            if (Math.Abs(cosSqAlpha) < 1e-15)
            {
                cos2SigmaM = 0.0;
            }
            else
            {
                cos2SigmaM = cosSigma - (2.0 * sinU1 * sinU2) / cosSqAlpha;
            }

            double C = (Flattening_F / 16.0) * cosSqAlpha * (4.0 + Flattening_F * (4.0 - 3.0 * cosSqAlpha));
            lambdaPrev = lambda;
            lambda = L + (1.0 - C) * Flattening_F * sinAlpha *
                (sigma + C * sinSigma * (cos2SigmaM + C * cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)));

        } while (Math.Abs(lambda - lambdaPrev) > 1e-12 && --maxIterations > 0);

        double sinL = Math.Sin(lambda);
        double cosL = Math.Cos(lambda);
        double f1 = cosU2 * sinL;
        double f2 = cosU1 * sinU2 - sinU1 * cosU2 * cosL;
        double alpha1 = Math.Atan2(f1, f2);
        double az = (alpha1 * Rad2Deg + 360.0) % 360.0;
        return az;
    }

    /// <summary>
    /// Calculates high-precision geodesic Qibla forward azimuth (degrees 0..360) from a given WGS84 point.
    /// </summary>
    public static double CalculateQiblaAzimuth(double lat, double lon) =>
        CalculateForwardAzimuth(lat, lon, KaabaLatitude, KaabaLongitude);

    /// <summary>
    /// Great-circle Haversine distance with WGS84 authalic radius.
    /// </summary>
    public static double CalculateGreatCircleDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Deg2Rad;
        double phi2 = lat2 * Deg2Rad;
        double dPhi = (lat2 - lat1) * Deg2Rad;
        double dLam = (lon2 - lon1) * Deg2Rad;

        double a = Math.Sin(dPhi / 2.0) * Math.Sin(dPhi / 2.0) +
                   Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dLam / 2.0) * Math.Sin(dLam / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return AuthalicRadius_R * c;
    }

    /// <summary>
    /// Calculates cumulative geodesic distance along an ordered sequence of points.
    /// </summary>
    public static double CalculatePolylineDistance(IReadOnlyList<(double lat, double lon)> points)
    {
        if (points == null || points.Count < 2) return 0.0;

        double total = 0.0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            total += CalculateGeodesicDistance(points[i].lat, points[i].lon, points[i + 1].lat, points[i + 1].lon);
        }
        return total;
    }

    /// <summary>
    /// Computes full 3D measurement metrics including planimetric geodesic distance, vertical altitude difference,
    /// 3D slope distance, slope angle, and slope percentage.
    /// </summary>
    public static Measurement3DResult Calculate3DMeasurement(
        double lat1, double lon1, double alt1,
        double lat2, double lon2, double alt2)
    {
        double horizMeters = CalculateGeodesicDistance(lat1, lon1, lat2, lon2);
        double vertMeters = Math.Abs(alt2 - alt1);
        double slopeMeters = Math.Sqrt(horizMeters * horizMeters + vertMeters * vertMeters);

        double slopeAngle = horizMeters > 1e-6 ? Math.Atan2(vertMeters, horizMeters) * Rad2Deg : (vertMeters > 0 ? 90.0 : 0.0);
        double slopePercent = horizMeters > 1e-6 ? (vertMeters / horizMeters) * 100.0 : 0.0;

        return new Measurement3DResult(
            GeodesicHorizontalDistanceMeters: horizMeters,
            VerticalDistanceMeters: vertMeters,
            SlopeDistanceMeters: slopeMeters,
            SlopePercentage: slopePercent,
            SlopeAngleDegrees: slopeAngle,
            FormattedHorizontalDistance: FormatDistance(horizMeters),
            Formatted3DDistance: FormatDistance(slopeMeters),
            FormattedSlope: $"{slopePercent:F1}% ({slopeAngle:F1}°)"
        );
    }

    /// <summary>
    /// Calculates geodetic polygon area using spherical excess on the WGS 84 authalic sphere,
    /// supporting exterior ring and arbitrary interior holes.
    /// </summary>
    public static PolygonAreaResult CalculatePolygonArea(
        IReadOnlyList<(double lat, double lon)> exteriorRing,
        IReadOnlyList<IReadOnlyList<(double lat, double lon)>>? interiorHoles = null)
    {
        if (exteriorRing == null || exteriorRing.Count < 3)
        {
            return new PolygonAreaResult(0, 0, 0, 0, 0, "0 m²", "0 m", AlgorithmName);
        }

        double outerArea = ComputeRingArea(exteriorRing);
        double outerPerimeter = ComputeRingPerimeter(exteriorRing);

        double holesArea = 0.0;
        double holesPerimeter = 0.0;

        if (interiorHoles != null)
        {
            foreach (var hole in interiorHoles)
            {
                if (hole != null && hole.Count >= 3)
                {
                    holesArea += ComputeRingArea(hole);
                    holesPerimeter += ComputeRingPerimeter(hole);
                }
            }
        }

        double netAreaMetersSq = Math.Max(0.0, outerArea - holesArea);
        double netAreaKmSq = netAreaMetersSq / 1_000_000.0;
        double netAreaHectares = netAreaMetersSq / 10_000.0;
        double netAreaAcres = netAreaMetersSq * 0.000247105;
        double totalPerimeterMeters = outerPerimeter + holesPerimeter;

        return new PolygonAreaResult(
            AreaSquareMeters: netAreaMetersSq,
            AreaSquareKilometers: netAreaKmSq,
            AreaHectares: netAreaHectares,
            AreaAcres: netAreaAcres,
            PerimeterMeters: totalPerimeterMeters,
            FormattedArea: FormatArea(netAreaMetersSq),
            FormattedPerimeter: FormatDistance(totalPerimeterMeters),
            AlgorithmName: AlgorithmName
        );
    }

    private static double ComputeRingArea(IReadOnlyList<(double lat, double lon)> ring)
    {
        int n = ring.Count;
        if (n < 3) return 0.0;

        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            var p1 = ring[i];
            var p2 = ring[(i + 1) % n];

            double lon1 = p1.lon * Deg2Rad;
            double lat1 = p1.lat * Deg2Rad;
            double lon2 = p2.lon * Deg2Rad;
            double lat2 = p2.lat * Deg2Rad;

            double dLon = lon2 - lon1;
            // Handle meridian wrapping
            if (dLon > Math.PI) dLon -= 2.0 * Math.PI;
            else if (dLon < -Math.PI) dLon += 2.0 * Math.PI;

            sum += dLon * (2.0 + Math.Sin(lat1) + Math.Sin(lat2));
        }

        double excess = Math.Abs(sum / 2.0);
        return excess * AuthalicRadius_R * AuthalicRadius_R;
    }

    private static double ComputeRingPerimeter(IReadOnlyList<(double lat, double lon)> ring)
    {
        int n = ring.Count;
        if (n < 2) return 0.0;

        double perim = 0.0;
        for (int i = 0; i < n; i++)
        {
            var p1 = ring[i];
            var p2 = ring[(i + 1) % n];
            perim += CalculateGeodesicDistance(p1.lat, p1.lon, p2.lat, p2.lon);
        }
        return perim;
    }

    public static string FormatDistance(double meters)
    {
        return Units.UnitService.Instance.FormatDistance(meters);
    }

    public static string FormatArea(double squareMeters)
    {
        return Units.UnitService.Instance.FormatArea(squareMeters);
    }
}
