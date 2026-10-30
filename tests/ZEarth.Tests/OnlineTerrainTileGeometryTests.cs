using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class OnlineTerrainTileGeometryTests
{
    [TestMethod]
    public void CopernicusInteriorTilesAlignPixelCentresWithCesiumVertices()
    {
        const double halfWorld = 20037508.342789244;
        const int level = 8, x = 145, y = 105;
        var bounds = OnlineTerrainTileService.GetMercatorSampleBounds(level, x, y);
        var span = halfWorld * 2 / (1 << level);
        var expectedWest = -halfWorld + x * span;
        var expectedNorth = halfWorld - y * span;
        var pixelSize = (bounds.East - bounds.West) / 65;
        Assert.AreEqual(expectedWest, bounds.West + pixelSize / 2, 1e-7);
        Assert.AreEqual(expectedWest + span, bounds.East - pixelSize / 2, 1e-7);
        Assert.AreEqual(expectedNorth, bounds.North - pixelSize / 2, 1e-7);
        Assert.AreEqual(expectedNorth - span, bounds.South + pixelSize / 2, 1e-7);
    }

    [TestMethod]
    public void BoundsAtWorldEdgesStayInsideWebMercatorDomain()
    {
        var bounds = OnlineTerrainTileService.GetMercatorSampleBounds(0, 0, 0);
        Assert.AreEqual(-20037508.342789244, bounds.West, 1e-8);
        Assert.AreEqual(20037508.342789244, bounds.North, 1e-8);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            OnlineTerrainTileService.GetMercatorSampleBounds(2, 4, 0));
    }
}
