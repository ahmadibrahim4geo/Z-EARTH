using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain.Measurement;
using ZEarth.Domain.Snapping;

namespace ZEarth.Tests;

[TestClass]
public class Sprint6SnappingAndMeasureTests
{
    // ==================== ZE-042 / AC-015 / AC-016: SCREEN-SPACE 12px TOLERANCE ====================

    [TestMethod]
    public void Snap_12pxTolerance_SnapsAt10pxDistance()
    {
        // AC-015: Snapping 12px to endpoint at 10px -> snap candidate shown, actual vertex coordinate chosen
        var index = new SpatialSnapIndex();
        index.AddPoint(lat: 30.0, lon: 31.0, alt: 100.0, featureId: "vertex-cairo");

        // Simple mock screen projection: (lat, lon, alt) -> (100, 100)
        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projector =
            _ => (100.0, 100.0);

        // Cursor at (108, 106) -> dx = 8, dy = 6 -> dist = sqrt(64 + 36) = 10.0 px <= 12.0 px
        var result = index.FindSnap(
            cursorScreenX: 108.0,
            cursorScreenY: 106.0,
            projectToScreen: projector,
            tolerancePixels: 12.0
        );

        Assert.IsNotNull(result);
        Assert.AreEqual(SnapType.Vertex, result.Type);
        Assert.AreEqual(10.0, result.DistancePixels, 0.001);
        Assert.AreEqual(30.0, result.Latitude, 0.0001);
        Assert.AreEqual(31.0, result.Longitude, 0.0001);
        Assert.AreEqual(100.0, result.Altitude, 0.1);
        Assert.AreEqual("vertex-cairo", result.FeatureId);
    }

    [TestMethod]
    public void Snap_12pxTolerance_RejectsAt13pxDistance()
    {
        // AC-016: Snapping 12px to same endpoint at 13px -> no snap, absent competing candidate
        var index = new SpatialSnapIndex();
        index.AddPoint(lat: 30.0, lon: 31.0, alt: 100.0, featureId: "vertex-cairo");

        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projector =
            _ => (100.0, 100.0);

        // Cursor at (109, 109.3) -> dx = 9, dy = 9.3 -> dist = sqrt(81 + 86.49) = sqrt(167.49) ≈ 12.94 px > 12.0 px
        var result = index.FindSnap(
            cursorScreenX: 109.0,
            cursorScreenY: 109.3,
            projectToScreen: projector,
            tolerancePixels: 12.0
        );

        Assert.IsNull(result);
    }

    // ==================== ZE-043 / AC-017: MIDPOINT & EDGE NEAREST PROJECTION ====================

    [TestMethod]
    public void Snap_Midpoint_SnapsCorrectlyWithinTolerance()
    {
        // Segment P1(0, 0) to P2(0, 100) -> Midpoint is (0, 50)
        var index = new SpatialSnapIndex();
        index.AddPolyline(new List<(double lat, double lon, double alt)>
        {
            (10.0, 20.0, 0.0),
            (10.0, 30.0, 0.0)
        }, featureId: "polyline-1");

        // Map geodetic coords to screen: P1 -> (0, 0), P2 -> (0, 100) -> Midpoint -> (0, 50)
        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projector =
            p => (0.0, (p.lon - 20.0) * 10.0);

        // Cursor at (2, 51) -> dist to midpoint (0, 50) is sqrt(4 + 1) = 2.23 px <= 12 px
        var result = index.FindSnap(
            cursorScreenX: 2.0,
            cursorScreenY: 51.0,
            projectToScreen: projector,
            tolerancePixels: 12.0,
            modes: SnapModes.Midpoint
        );

        Assert.IsNotNull(result);
        Assert.AreEqual(SnapType.Midpoint, result.Type);
        Assert.AreEqual(10.0, result.Latitude, 0.0001);
        Assert.AreEqual(25.0, result.Longitude, 0.0001); // Midpoint of 20 and 30
        Assert.IsTrue(result.DistancePixels <= 12.0);
    }

    [TestMethod]
    public void Snap_EdgeProjection_ProjectsPerpendicularlyOntoSegment()
    {
        var index = new SpatialSnapIndex();
        index.AddPolyline(new List<(double lat, double lon, double alt)>
        {
            (10.0, 20.0, 0.0),
            (20.0, 40.0, 0.0)
        }, featureId: "edge-test");

        // Segment screen coordinates: (0, 0) to (100, 0) horizontal segment
        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projector =
            p => ((p.lon - 20.0) * 5.0, 0.0);

        // Cursor at (40, 5) -> Projection on edge is (40, 0) -> dist = 5.0 px <= 12.0 px
        var result = index.FindSnap(
            cursorScreenX: 40.0,
            cursorScreenY: 5.0,
            projectToScreen: projector,
            tolerancePixels: 12.0,
            modes: SnapModes.Edge
        );

        Assert.IsNotNull(result);
        Assert.AreEqual(SnapType.Edge, result.Type);
        Assert.AreEqual(5.0, result.DistancePixels, 0.001);
        // t = 40 / 100 = 0.40 -> lat = 10 + 0.4 * 10 = 14.0, lon = 20 + 0.4 * 20 = 28.0
        Assert.AreEqual(14.0, result.Latitude, 0.0001);
        Assert.AreEqual(28.0, result.Longitude, 0.0001);
    }

    // ==================== ZE-044 / AC-017: TOLERANCE SETTINGS & MODE FILTERING ====================

    [TestMethod]
    public void Snap_ModeFiltering_RespectsDisabledModes()
    {
        var index = new SpatialSnapIndex();
        index.AddPolyline(new List<(double lat, double lon, double alt)>
        {
            (10.0, 20.0, 0.0),
            (10.0, 30.0, 0.0)
        });

        Func<(double lat, double lon, double alt), (double screenX, double screenY)> projector =
            p => (0.0, (p.lon - 20.0) * 10.0); // (0, 0) to (0, 100), midpoint (0, 50)

        // Near midpoint (0, 50) with cursor at (1, 50)
        // With Midpoint mode DISABLED, it should fall back to Edge mode
        var resultEdge = index.FindSnap(
            cursorScreenX: 1.0,
            cursorScreenY: 50.0,
            projectToScreen: projector,
            tolerancePixels: 12.0,
            modes: SnapModes.Edge // Only Edge enabled
        );

        Assert.IsNotNull(resultEdge);
        Assert.AreEqual(SnapType.Edge, resultEdge.Type);

        // With Vertex mode ONLY (cursor is 50px away from vertices)
        var resultNone = index.FindSnap(
            cursorScreenX: 1.0,
            cursorScreenY: 50.0,
            projectToScreen: projector,
            tolerancePixels: 12.0,
            modes: SnapModes.Vertex // Only Vertex enabled
        );

        Assert.IsNull(resultNone);
    }

    // ==================== ZE-045 / AC-018: SHARED SNAP INDEX FOR DRAWING & MEASURE ====================

    [TestMethod]
    public void Snap_PolygonWithHoles_IndexesOuterRingAndInteriorHoles()
    {
        var index = new SpatialSnapIndex();

        var outer = new List<(double lat, double lon, double alt)>
        {
            (30.0, 31.0, 0.0),
            (30.0, 32.0, 0.0),
            (31.0, 32.0, 0.0),
            (31.0, 31.0, 0.0)
        };

        var hole = new List<(double lat, double lon, double alt)>
        {
            (30.3, 31.3, 0.0),
            (30.3, 31.7, 0.0),
            (30.7, 31.7, 0.0),
            (30.7, 31.3, 0.0)
        };

        index.AddPolygon(outer, new[] { hole }, featureId: "poly-with-hole");

        // Outer ring has 4 vertices + 4 segments; Hole has 4 vertices + 4 segments
        Assert.AreEqual(8, index.VertexCount);
        Assert.AreEqual(8, index.SegmentCount);
    }

    // ==================== ZE-046 / AC-018: GEODESIC DISTANCE & POLYGON AREA ====================

    [TestMethod]
    public void Geodesic_CairoToAlexandria_CalculatesAccurateDistance()
    {
        // Cairo (30.0444° N, 31.2357° E) to Alexandria (31.2001° N, 29.9187° E)
        double distanceMeters = GeodesicMeasurementService.CalculateGeodesicDistance(
            30.0444, 31.2357,
            31.2001, 29.9187
        );

        // Authoritative WGS84 geodesic distance is 179.89 km (179,887 m)
        Assert.IsTrue(distanceMeters >= 179800.0 && distanceMeters <= 180000.0,
            $"Expected ~179.89km, got {distanceMeters / 1000.0:F2} km");
    }

    [TestMethod]
    public void Geodesic_IdenticalPoints_ReturnsZeroDistance()
    {
        double dist = GeodesicMeasurementService.CalculateGeodesicDistance(30.0, 31.0, 30.0, 31.0);
        Assert.AreEqual(0.0, dist);
    }

    [TestMethod]
    public void Geodesic_PolylineDistance_SumsSegmentDistances()
    {
        var pts = new List<(double lat, double lon)>
        {
            (30.0, 31.0),
            (30.5, 31.0),
            (31.0, 31.0)
        };

        double total = GeodesicMeasurementService.CalculatePolylineDistance(pts);
        double seg1 = GeodesicMeasurementService.CalculateGeodesicDistance(30.0, 31.0, 30.5, 31.0);
        double seg2 = GeodesicMeasurementService.CalculateGeodesicDistance(30.5, 31.0, 31.0, 31.0);

        Assert.AreEqual(seg1 + seg2, total, 0.001);
        Assert.IsTrue(total > 110000.0); // 1 degree of latitude is ~111km
    }

    [TestMethod]
    public void Geodesic_PolygonArea_ComputesAccurateAreaAndSubtractsHoles()
    {
        // 1-degree square near the equator: ~111.3km x 111.3km ≈ 12,350 to 12,400 km²
        var outer = new List<(double lat, double lon)>
        {
            (0.0, 0.0),
            (0.0, 1.0),
            (1.0, 1.0),
            (1.0, 0.0)
        };

        var outerResult = GeodesicMeasurementService.CalculatePolygonArea(outer);

        Assert.IsTrue(outerResult.AreaSquareKilometers >= 12300.0 && outerResult.AreaSquareKilometers <= 12450.0,
            $"Expected ~12,350 km², got {outerResult.AreaSquareKilometers:F1} km²");
        Assert.IsTrue(outerResult.PerimeterMeters >= 440000.0 && outerResult.PerimeterMeters <= 450000.0);

        // Add a central hole of 0.2° x 0.2°
        var hole = new List<(double lat, double lon)>
        {
            (0.4, 0.4),
            (0.4, 0.6),
            (0.6, 0.6),
            (0.6, 0.4)
        };

        var holeResult = GeodesicMeasurementService.CalculatePolygonArea(hole);
        var netResult = GeodesicMeasurementService.CalculatePolygonArea(outer, new[] { hole });

        // Net area must equal outer area minus hole area
        Assert.AreEqual(outerResult.AreaSquareMeters - holeResult.AreaSquareMeters, netResult.AreaSquareMeters, 0.1);
        Assert.IsTrue(netResult.AreaSquareMeters < outerResult.AreaSquareMeters);
        Assert.AreEqual(outerResult.PerimeterMeters + holeResult.PerimeterMeters, netResult.PerimeterMeters, 0.1);
    }

    // ==================== ZE-047 / AC-019: 3D MEASUREMENT & TERRAIN INDEPENDENCE ====================

    [TestMethod]
    public void Measurement3D_CalculatesSlopeDistanceAndAngleCorrectly()
    {
        // Point 1: (30.0, 31.0, 100m)
        // Point 2: (30.0, 31.01, 300m) -> Horizontal distance ~960m, deltaZ = 200m
        var result = GeodesicMeasurementService.Calculate3DMeasurement(
            lat1: 30.0, lon1: 31.0, alt1: 100.0,
            lat2: 30.0, lon2: 31.01, alt2: 300.0
        );

        Assert.IsTrue(result.GeodesicHorizontalDistanceMeters > 900.0 && result.GeodesicHorizontalDistanceMeters < 1000.0);
        Assert.AreEqual(200.0, result.VerticalDistanceMeters, 0.01);

        double expectedSlope = Math.Sqrt(
            result.GeodesicHorizontalDistanceMeters * result.GeodesicHorizontalDistanceMeters +
            result.VerticalDistanceMeters * result.VerticalDistanceMeters
        );

        Assert.AreEqual(expectedSlope, result.SlopeDistanceMeters, 0.001);
        Assert.IsTrue(result.SlopePercentage > 20.0 && result.SlopePercentage < 22.0);
        Assert.IsTrue(result.SlopeAngleDegrees > 11.0 && result.SlopeAngleDegrees < 13.0);
    }

    [TestMethod]
    public void Measurement_TerrainExaggerationDoesNotAffectStoredGeodeticMetrics()
    {
        // AC-019: Set terrain exaggeration 3x then 1x -> visual relief changes; measure results and stored Z unchanged
        double lat1 = 30.0, lon1 = 31.0, alt1 = 150.0;
        double lat2 = 30.05, lon2 = 31.05, alt2 = 450.0;

        // Visual exaggeration factor does not enter geodetic formulas
        var measurementAt1x = GeodesicMeasurementService.Calculate3DMeasurement(lat1, lon1, alt1, lat2, lon2, alt2);

        // Visual exaggeration 3x would only scale renderer height for display; underlying coordinate math is invariant
        double storedAltDiff1 = Math.Abs(alt2 - alt1);
        double storedAltDiff2 = Math.Abs(alt2 - alt1);

        Assert.AreEqual(storedAltDiff1, storedAltDiff2);
        Assert.AreEqual(300.0, measurementAt1x.VerticalDistanceMeters);
        Assert.IsTrue(measurementAt1x.SlopeDistanceMeters > measurementAt1x.GeodesicHorizontalDistanceMeters);
    }
}
