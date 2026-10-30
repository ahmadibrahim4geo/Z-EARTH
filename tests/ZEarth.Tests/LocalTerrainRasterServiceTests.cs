using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OSGeo.GDAL;
using OSGeo.OSR;
using ZEarth.App.Services;
using ZEarth.Domain.Gis;

namespace ZEarth.Tests;

[TestClass]
public sealed class LocalTerrainRasterServiceTests
{
    [TestMethod]
    public async Task RegionalRasterPreservesMissingCoverageAndReturnsRealElevations()
    {
        GdalConfiguration.EnsureInitialized();
        Assert.IsTrue(GdalConfiguration.IsAvailable);
        var path = Path.Combine(Path.GetTempPath(), $"zearth-regional-{Guid.NewGuid():N}.tif");
        try
        {
            using (var ds = Gdal.GetDriverByName("GTiff").Create(path, 128, 128, 1, DataType.GDT_Float32, null))
            {
                ds.SetGeoTransform(new[] { 30d, 2d / 128, 0d, 31d, 0d, -2d / 128 });
                using var crs = new SpatialReference(string.Empty);
                crs.ImportFromEPSG(4326);
                crs.ExportToWkt(out var wkt, null);
                ds.SetProjection(wkt);
                using var band = ds.GetRasterBand(1);
                band.SetNoDataValue(-9999);
                band.SetScale(2);
                band.SetOffset(10);
                band.Fill(250, 0);
                ds.FlushCache();
            }
            var service = LocalTerrainRasterService.Instance;
            var metadata = service.Configure(path, "Feet");
            var covered = await service.GetTileAsync(metadata.ProbeLevel, metadata.ProbeX, metadata.ProbeY);
            Assert.IsNotNull(covered);
            var values = new float[65 * 65];
            Buffer.BlockCopy(covered, 0, values, 0, covered.Length);
            Assert.IsTrue(values.Any(v => float.IsFinite(v) && Math.Abs(v - 155.448f) < 0.01f));

            var outside = await service.GetTileAsync(8, 0, 0);
            Assert.IsNotNull(outside);
            Buffer.BlockCopy(outside, 0, values, 0, outside.Length);
            Assert.IsTrue(values.All(float.IsNaN), "Outside the raster must remain missing, not fabricated as zero elevation.");

            var edge = await service.GetTileAsync(8, 149, 105);
            Assert.IsNotNull(edge);
            Buffer.BlockCopy(edge, 0, values, 0, edge.Length);
            Assert.IsTrue(values.Any(float.IsNaN), "Partially covered tiles must preserve missing vertices.");
            Assert.IsTrue(values.Any(v => float.IsFinite(v) && Math.Abs(v - 155.448f) < 0.01f));

            var root = await service.GetTileAsync(0, 0, 0);
            Assert.IsNotNull(root, "A regional raster must not be rejected because the root includes uncovered areas.");
            Buffer.BlockCopy(root, 0, values, 0, root.Length);
            Assert.IsTrue(values.Any(float.IsNaN));

            _ = service.Inspect(path, "Meters");
            Assert.AreEqual("Feet", service.Metadata?.SelectedVerticalUnit,
                "Browsing a candidate must not change the active raster configuration.");
            var meters = service.Configure(path, "Meters");
            var reconfigured = await service.GetTileAsync(meters.ProbeLevel, meters.ProbeX, meters.ProbeY);
            Assert.IsNotNull(reconfigured);
            Buffer.BlockCopy(reconfigured, 0, values, 0, reconfigured.Length);
            Assert.IsTrue(values.Any(v => float.IsFinite(v) && Math.Abs(v - 510f) < 0.01f),
                "Changing units must invalidate the previous tile cache.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
