using System;
using System.Collections.Generic;

namespace ZEarth.Domain.Snapping;

public enum SnapType
{
    Vertex,
    Midpoint,
    Edge
}

[Flags]
public enum SnapModes
{
    None = 0,
    Vertex = 1,
    Midpoint = 2,
    Edge = 4,
    All = Vertex | Midpoint | Edge
}

public record SnapTarget(
    SnapType Type,
    double Latitude,
    double Longitude,
    double Altitude,
    double ScreenX,
    double ScreenY,
    double DistancePixels,
    string? FeatureId = null,
    int SegmentIndex = -1
);

public record IndexedPoint(double Latitude, double Longitude, double Altitude, string? FeatureId);

public record IndexedSegment(
    (double Latitude, double Longitude, double Altitude) P1,
    (double Latitude, double Longitude, double Altitude) P2,
    string? FeatureId,
    int SegmentIndex
)
{
    public (double Latitude, double Longitude, double Altitude) Midpoint => (
        (P1.Latitude + P2.Latitude) / 2.0,
        (P1.Longitude + P2.Longitude) / 2.0,
        (P1.Altitude + P2.Altitude) / 2.0
    );
}

/// <summary>
/// High-performance spatial snap index for 2D and 3D screen-space and geodetic snapping.
/// Indexes vertices, segment midpoints, and edge projections with configurable pixel tolerances.
/// </summary>
public class SpatialSnapIndex
{
    public const double DefaultTolerancePixels = 12.0;

    private readonly List<IndexedPoint> _vertices = new();
    private readonly List<IndexedSegment> _segments = new();

    public int VertexCount => _vertices.Count;
    public int SegmentCount => _segments.Count;

    public void Clear()
    {
        _vertices.Clear();
        _segments.Clear();
    }

    public void AddPoint(double lat, double lon, double alt = 0.0, string? featureId = null)
    {
        _vertices.Add(new IndexedPoint(lat, lon, alt, featureId));
    }

    public void AddPolyline(IReadOnlyList<(double lat, double lon, double alt)> coords, string? featureId = null)
    {
        if (coords == null || coords.Count == 0) return;

        for (int i = 0; i < coords.Count; i++)
        {
            _vertices.Add(new IndexedPoint(coords[i].lat, coords[i].lon, coords[i].alt, featureId));
            if (i > 0)
            {
                _segments.Add(new IndexedSegment(
                    P1: (coords[i - 1].lat, coords[i - 1].lon, coords[i - 1].alt),
                    P2: (coords[i].lat, coords[i].lon, coords[i].alt),
                    FeatureId: featureId,
                    SegmentIndex: i - 1
                ));
            }
        }
    }

    public void AddPolygon(
        IReadOnlyList<(double lat, double lon, double alt)> exteriorRing,
        IReadOnlyList<IReadOnlyList<(double lat, double lon, double alt)>>? interiorHoles = null,
        string? featureId = null)
    {
        if (exteriorRing == null || exteriorRing.Count < 3) return;

        AddLinearRing(exteriorRing, featureId);

        if (interiorHoles != null)
        {
            foreach (var hole in interiorHoles)
            {
                if (hole != null && hole.Count >= 3)
                {
                    AddLinearRing(hole, featureId);
                }
            }
        }
    }

    private void AddLinearRing(IReadOnlyList<(double lat, double lon, double alt)> ring, string? featureId)
    {
        int count = ring.Count;
        for (int i = 0; i < count; i++)
        {
            _vertices.Add(new IndexedPoint(ring[i].lat, ring[i].lon, ring[i].alt, featureId));
            int nextIdx = (i + 1) % count;
            _segments.Add(new IndexedSegment(
                P1: (ring[i].lat, ring[i].lon, ring[i].alt),
                P2: (ring[nextIdx].lat, ring[nextIdx].lon, ring[nextIdx].alt),
                FeatureId: featureId,
                SegmentIndex: i
            ));
        }
    }

    /// <summary>
    /// Searches for the closest snap target within the specified screen-space tolerance (pixels).
    /// Follows priority hierarchy: Vertex -> Midpoint -> Edge.
    /// </summary>
    public SnapTarget? FindSnap(
        double cursorScreenX,
        double cursorScreenY,
        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projectToScreen,
        double tolerancePixels = DefaultTolerancePixels,
        SnapModes modes = SnapModes.All)
    {
        if (projectToScreen == null) throw new ArgumentNullException(nameof(projectToScreen));

        SnapTarget? bestVertex = null;
        double minVertexDist = double.MaxValue;

        // 1. Check Vertices (Priority 1)
        if (modes.HasFlag(SnapModes.Vertex))
        {
            foreach (var v in _vertices)
            {
                var s = projectToScreen((v.Latitude, v.Longitude, v.Altitude));
                double dx = s.screenX - cursorScreenX;
                double dy = s.screenY - cursorScreenY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist <= tolerancePixels && dist < minVertexDist)
                {
                    minVertexDist = dist;
                    bestVertex = new SnapTarget(
                        Type: SnapType.Vertex,
                        Latitude: v.Latitude,
                        Longitude: v.Longitude,
                        Altitude: v.Altitude,
                        ScreenX: s.screenX,
                        ScreenY: s.screenY,
                        DistancePixels: dist,
                        FeatureId: v.FeatureId
                    );
                }
            }

            if (bestVertex != null)
            {
                return bestVertex;
            }
        }

        // 2. Check Midpoints (Priority 2)
        SnapTarget? bestMidpoint = null;
        double minMidpointDist = double.MaxValue;

        if (modes.HasFlag(SnapModes.Midpoint))
        {
            foreach (var seg in _segments)
            {
                var mid = seg.Midpoint;
                var s = projectToScreen(mid);
                double dx = s.screenX - cursorScreenX;
                double dy = s.screenY - cursorScreenY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist <= tolerancePixels && dist < minMidpointDist)
                {
                    minMidpointDist = dist;
                    bestMidpoint = new SnapTarget(
                        Type: SnapType.Midpoint,
                        Latitude: mid.Item1,
                        Longitude: mid.Item2,
                        Altitude: mid.Item3,
                        ScreenX: s.screenX,
                        ScreenY: s.screenY,
                        DistancePixels: dist,
                        FeatureId: seg.FeatureId,
                        SegmentIndex: seg.SegmentIndex
                    );
                }
            }

            if (bestMidpoint != null)
            {
                return bestMidpoint;
            }
        }

        // 3. Check Edge Nearest Projection (Priority 3)
        SnapTarget? bestEdge = null;
        double minEdgeDist = double.MaxValue;

        if (modes.HasFlag(SnapModes.Edge))
        {
            foreach (var seg in _segments)
            {
                var s1 = projectToScreen(seg.P1);
                var s2 = projectToScreen(seg.P2);

                double segDx = s2.screenX - s1.screenX;
                double segDy = s2.screenY - s1.screenY;
                double segLenSq = segDx * segDx + segDy * segDy;

                if (segLenSq < 1e-6) continue;

                double curDx = cursorScreenX - s1.screenX;
                double curDy = cursorScreenY - s1.screenY;

                double t = (curDx * segDx + curDy * segDy) / segLenSq;
                t = Math.Clamp(t, 0.0, 1.0);

                double projScreenX = s1.screenX + t * segDx;
                double projScreenY = s1.screenY + t * segDy;

                double distDx = projScreenX - cursorScreenX;
                double distDy = projScreenY - cursorScreenY;
                double dist = Math.Sqrt(distDx * distDx + distDy * distDy);

                if (dist <= tolerancePixels && dist < minEdgeDist)
                {
                    minEdgeDist = dist;
                    double projLat = seg.P1.Latitude + t * (seg.P2.Latitude - seg.P1.Latitude);
                    double projLon = seg.P1.Longitude + t * (seg.P2.Longitude - seg.P1.Longitude);
                    double projAlt = seg.P1.Altitude + t * (seg.P2.Altitude - seg.P1.Altitude);

                    bestEdge = new SnapTarget(
                        Type: SnapType.Edge,
                        Latitude: projLat,
                        Longitude: projLon,
                        Altitude: projAlt,
                        ScreenX: projScreenX,
                        ScreenY: projScreenY,
                        DistancePixels: dist,
                        FeatureId: seg.FeatureId,
                        SegmentIndex: seg.SegmentIndex
                    );
                }
            }
        }

        return bestEdge;
    }
}
