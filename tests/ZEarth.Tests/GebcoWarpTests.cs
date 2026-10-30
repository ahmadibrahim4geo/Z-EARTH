using System.Reflection;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OSGeo.GDAL;
using OSGeo.OSR;
using ZEarth.App.Services;
using ZEarth.Domain.Gis;

namespace ZEarth.Tests;

[TestClass]
public sealed class GebcoWarpTests
{
    [TestMethod]
    public void GlobalRasterProducesCompleteNumericRootIncludingDateLine()
    {
        using var source = CreateWorldRaster(false);
        var bytes = Warp(source);
        Assert.IsNotNull(bytes);
        Assert.AreEqual(65 * 65 * sizeof(float), bytes.Length);
        var heights = new float[65 * 65];
        Buffer.BlockCopy(bytes, 0, heights, 0, bytes.Length);
        Assert.IsTrue(heights.All(float.IsFinite));
        Assert.IsTrue(heights.Min() < 0 && heights.Max() > 0);
    }

    [TestMethod]
    public void MissingSourceDataDoesNotBecomeInventedTerrain()
    {
        using var source = CreateWorldRaster(true);
        var ex = Assert.ThrowsException<TargetInvocationException>(() => Warp(source));
        Assert.IsInstanceOfType<InvalidDataException>(ex.InnerException);
    }

    private static Dataset CreateWorldRaster(bool noData)
    {
        GdalConfiguration.EnsureInitialized();
        Assert.IsTrue(GdalConfiguration.IsAvailable);
        var source = Gdal.GetDriverByName("MEM").Create("", 360, 180, 1, DataType.GDT_Float32, null);
        source.SetGeoTransform(new[] { -180d, 1d, 0d, 90d, 0d, -1d });
        using var crs = new SpatialReference("");
        crs.ImportFromEPSG(4326);
        crs.ExportToWkt(out var wkt, null);
        source.SetProjection(wkt);
        using var band = source.GetRasterBand(1);
        band.SetNoDataValue(-9999);
        var values = Enumerable.Range(0, 360 * 180)
            .Select(i => noData ? -9999f : (float)(i % 360 - 180)).ToArray();
        Assert.AreEqual(CPLErr.CE_None, band.WriteRaster(0, 0, 360, 180, values, 360, 180, 0, 0));
        return source;
    }

    private static byte[]? Warp(Dataset source) => (byte[]?)typeof(OnlineTerrainTileService)
        .GetMethod("ReprojectSourceToTile", BindingFlags.Static | BindingFlags.NonPublic)!
        .Invoke(null, new object[] { source, 0, 0, 0 });
}
