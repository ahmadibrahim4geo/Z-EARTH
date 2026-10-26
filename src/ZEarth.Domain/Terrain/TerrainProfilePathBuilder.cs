using System;
using System.Collections.Generic;
using ZEarth.Domain.Measurement;

namespace ZEarth.Domain.Terrain;

/// <summary>
/// Geographic great-circle interpolation for bounded, reproducible terrain
/// sampling. The requested output is geometry only; its Z is intentionally
/// zero, never used as a measured ground elevation.
/// </summary>
public static class TerrainProfilePathBuilder
{
    public sealed record PathQuery(double DistanceMeters, CoordinatePoint Location);

    public static IReadOnlyList<PathQuery> Build(
        IReadOnlyList<CoordinatePoint> vertices, int sampleLimit = 160)
    {
        if (vertices == null || vertices.Count < 2 || sampleLimit < 2 || sampleLimit > 256)
            return Array.Empty<PathQuery>();
        var cumulative = new double[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            var point = vertices[i];
            if (!double.IsFinite(point.Longitude) || !double.IsFinite(point.Latitude) ||
                point.Longitude < -180 || point.Longitude > 180 ||
                point.Latitude < -90 || point.Latitude > 90)
                return Array.Empty<PathQuery>();
            if (i > 0)
            {
                double segment = GeodesicMeasurementService.ComputeDistanceMeters(
                    vertices[i - 1].Longitude, vertices[i - 1].Latitude,
                    point.Longitude, point.Latitude);
                if (!double.IsFinite(segment) || segment < 0) return Array.Empty<PathQuery>();
                cumulative[i] = cumulative[i - 1] + segment;
            }
        }
        var length = cumulative[^1];
        if (length < 0.01 || !double.IsFinite(length)) return Array.Empty<PathQuery>();
        // Adaptive coarse route spacing, bounded to 160 requests (never unlimited
        // download/calls for large user-owned GPS tracks).
        int count = Math.Clamp((int)Math.Ceiling(length / 250.0) + 1, 2, sampleLimit);
        var results = new List<PathQuery>(count);
        int segmentIndex = 0;
        for (int j = 0; j < count; j++)
        {
            double distance = length * j / (count - 1);
            while (segmentIndex < vertices.Count - 2 && cumulative[segmentIndex + 1] < distance)
                segmentIndex++;
            int next = segmentIndex + 1;
            double segLength = cumulative[next] - cumulative[segmentIndex];
            double t = segLength < 0.01 ? 1.0 :
                Math.Clamp((distance - cumulative[segmentIndex]) / segLength, 0, 1);
            var a = vertices[segmentIndex];
            var b = vertices[next];
            var (lon, lat) = GreatCirclePoint(a.Longitude, a.Latitude, b.Longitude, b.Latitude, t);
            if (j == 0) { lon = vertices[0].Longitude; lat = vertices[0].Latitude; }
            if (j == count - 1) { lon = vertices[^1].Longitude; lat = vertices[^1].Latitude; }
            results.Add(new PathQuery(distance, new CoordinatePoint(lon, lat, 0)));
        }
        return results;
    }

    private static (double Longitude, double Latitude) GreatCirclePoint(
        double lonA, double latA, double lonB, double latB, double fraction)
    {
        const double rad = Math.PI / 180.0;
        var phiA = latA * rad; var phiB = latB * rad;
        var lambdaA = lonA * rad; var lambdaB = lonB * rad;
        var ax = Math.Cos(phiA) * Math.Cos(lambdaA);
        var ay = Math.Cos(phiA) * Math.Sin(lambdaA);
        var az = Math.Sin(phiA);
        var bx = Math.Cos(phiB) * Math.Cos(lambdaB);
        var by = Math.Cos(phiB) * Math.Sin(lambdaB);
        var bz = Math.Sin(phiB);
        double omega = Math.Acos(Math.Clamp(ax * bx + ay * by + az * bz, -1.0, 1.0));
        if (omega < 1e-9 || Math.Abs(Math.Sin(omega)) < 1e-9)
        {
            var delta = (lonB - lonA + 540) % 360 - 180;
            double longitude = lonA + fraction * delta;
            return (WrapLongitude(longitude), latA + (latB - latA) * fraction);
        }
        double wa = Math.Sin((1 - fraction) * omega) / Math.Sin(omega);
        double wb = Math.Sin(fraction * omega) / Math.Sin(omega);
        double x = wa * ax + wb * bx;
        double y = wa * ay + wb * by;
        double z = wa * az + wb * bz;
        return (WrapLongitude(Math.Atan2(y, x) / rad),
            Math.Atan2(z, Math.Sqrt(x * x + y * y)) / rad);
    }

    private static double WrapLongitude(double value) =>
        ((value + 180) % 360 + 360) % 360 - 180;
}
