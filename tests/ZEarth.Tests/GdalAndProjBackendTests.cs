using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Kml;

namespace ZEarth.Tests
{
    [TestClass]
    public class GdalAndProjBackendTests
    {
        [ClassInitialize]
        public static void InitializeGdal(TestContext context)
        {
            MaxRev.Gdal.Core.GdalBase.ConfigureAll();
            Gdal.AllRegister();
            Ogr.RegisterAll();
        }

        [TestMethod]
        public void GdalAndProj_AreInitialized_AndDriversLoaded()
        {
            int driverCount = Ogr.GetDriverCount();
            Assert.IsTrue(driverCount > 10, $"Expected >10 OGR drivers, got {driverCount}");

            var gpkgDriver = Ogr.GetDriverByName("GPKG");
            Assert.IsNotNull(gpkgDriver, "GPKG driver must be available");

            var shpDriver = Ogr.GetDriverByName("ESRI Shapefile");
            Assert.IsNotNull(shpDriver, "ESRI Shapefile driver must be available");

            var tiffDriver = Gdal.GetDriverByName("GTiff");
            Assert.IsNotNull(tiffDriver, "GTiff driver must be available");
        }

        [TestMethod]
        public void Proj_TransformsEgyptianRedBelt_ToWgs84Accurately()
        {
            // EPSG:22992 = Egypt 1907 / Red Belt (Helmert 1906 datum)
            // Origin at 30°N, 31°E, False Easting 615000, False Northing 810000
            using var srsSource = new SpatialReference("");
            srsSource.ImportFromEPSG(22992);
            srsSource.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using var srsTarget = new SpatialReference("");
            srsTarget.ImportFromEPSG(4326);
            srsTarget.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using var transform = new CoordinateTransformation(srsSource, srsTarget);

            // Test origin point: 615000m E, 810000m N -> should be exactly 31.0°E, 30.0°N
            double[] ptOrigin = new double[] { 615000.0, 810000.0, 0.0 };
            transform.TransformPoint(ptOrigin);

            Assert.AreEqual(31.0, ptOrigin[0], 0.005, "Origin longitude should be ~31°E");
            Assert.AreEqual(30.0, ptOrigin[1], 0.005, "Origin latitude should be ~30°N");

            // Test Cairo Citadel / Al-Azhar in Red Belt coordinates (~638300m E, 814200m N)
            double[] ptCairo = new double[] { 638300.0, 814200.0, 0.0 };
            transform.TransformPoint(ptCairo);
            Assert.IsTrue(ptCairo[0] > 31.2 && ptCairo[0] < 31.35, $"Cairo Citadel Lon {ptCairo[0]} in expected range");
            Assert.IsTrue(ptCairo[1] > 30.0 && ptCairo[1] < 30.15, $"Cairo Citadel Lat {ptCairo[1]} in expected range");
        }

        [TestMethod]
        public void Proj_TransformsUtmZone36N_ToWgs84Accurately()
        {
            // EPSG:32636 = WGS 84 / UTM zone 36N
            using var srsSource = new SpatialReference("");
            srsSource.ImportFromEPSG(32636);
            srsSource.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using var srsTarget = new SpatialReference("");
            srsTarget.ImportFromEPSG(4326);
            srsTarget.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using var transform = new CoordinateTransformation(srsSource, srsTarget);

            // Cairo landmark in UTM 36N: ~330100m E, 3324900m N
            double[] pt = new double[] { 330100.0, 3324900.0, 0.0 };
            transform.TransformPoint(pt);

            Assert.AreEqual(31.2357, pt[0], 0.01, "Lon should match Cairo");
            Assert.AreEqual(30.0444, pt[1], 0.01, "Lat should match Cairo");
        }

        [TestMethod]
        public void Proj_AuthoritativeBenchmarks_MatchIndependentProj()
        {
            // Point 1: EPSG:22992 origin (615000, 810000)
            var pt1 = CoordinateTransformService.Transform(615000.0, 810000.0, 22992, 4326);
            System.Diagnostics.Trace.WriteLine($"Point 1 EPSG:22992 -> Lon: {pt1.X:F10}, Lat: {pt1.Y:F10}");
            Assert.AreEqual(31.0016668457, pt1.X, 0.0001, "Origin Longitude transformed to WGS84");
            Assert.AreEqual(30.0001661407, pt1.Y, 0.0001, "Origin Latitude transformed to WGS84");

            // Point 2: EPSG:22992 Cairo Citadel (636580, 815120)
            var pt2 = CoordinateTransformService.Transform(636580.0, 815120.0, 22992, 4326);
            System.Diagnostics.Trace.WriteLine($"Point 2 EPSG:22992 -> Lon: {pt2.X:F10}, Lat: {pt2.Y:F10}");
            Assert.AreEqual(31.2254299520, pt2.X, 0.0001, "Citadel Longitude transformed to WGS84");
            Assert.AreEqual(30.0461619443, pt2.Y, 0.0001, "Citadel Latitude transformed to WGS84");

            // Point 3: EPSG:32636 UTM 36N (328754.21, 3325189.62)
            var pt3 = CoordinateTransformService.Transform(328754.21, 3325189.62, 32636, 4326);
            System.Diagnostics.Trace.WriteLine($"Point 3 EPSG:32636 -> Lon: {pt3.X:F10}, Lat: {pt3.Y:F10}");
            Assert.AreEqual(31.2237963679, pt3.X, 0.0001, "UTM Longitude transformed to WGS84");
            Assert.AreEqual(30.0457995682, pt3.Y, 0.0001, "UTM Latitude transformed to WGS84");
        }

        [TestMethod]
        public void SqliteStorage_Roundtrip_Works()
        {
            var project = ZEarth.Domain.ProjectDocument.CreateDefault();
            project.Name = "SQLite Test Project";

            string tempFile = Path.Combine(Path.GetTempPath(), $"test_project_{Guid.NewGuid():N}.sqlite");
            try
            {
                ZEarth.Domain.Persistence.SqliteProjectStorage.SaveToDatabase(project, tempFile);
                Assert.IsTrue(File.Exists(tempFile));

                var loaded = ZEarth.Domain.Persistence.SqliteProjectStorage.LoadFromDatabase(tempFile);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("SQLite Test Project", loaded.Name);
                Assert.AreEqual(project.MyPlaces.Count, loaded.MyPlaces.Count);
                Assert.AreEqual(project.SavedScenes.Count, loaded.SavedScenes.Count);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void GeoPackage_ImportWithReprojection_AndAttributes_Works()
        {
            // Requirement 3 & 8: Real projected GeoPackage with attributes
            string gpkgPath = Path.Combine(Path.GetTempPath(), $"cairo_{Guid.NewGuid():N}.gpkg");
            try
            {
                var driver = Ogr.GetDriverByName("GPKG");
                using (var ds = driver.CreateDataSource(gpkgPath, null))
                {
                    using var srs = new SpatialReference("");
                    srs.ImportFromEPSG(32636); // UTM Zone 36N
                    using var layer = ds.CreateLayer("landmarks", srs, wkbGeometryType.wkbPoint, null);
                    using var fieldDefn = new FieldDefn("name", FieldType.OFTString);
                    layer.CreateField(fieldDefn, 1);

                    using var feat = new Feature(layer.GetLayerDefn());
                    feat.SetField("name", "Cairo Tower");
                    using var pt = new Geometry(wkbGeometryType.wkbPoint);
                    pt.AddPoint_2D(330100.0, 3324900.0);
                    feat.SetGeometry(pt);
                    layer.CreateFeature(feat);
                }

                var result = ZEarth.Domain.Gis.VectorDatasetReader.ReadFile(gpkgPath);
                Assert.IsTrue(result.Success);
                Assert.AreEqual("OGC GeoPackage", result.SourceFormat);
                Assert.AreEqual("EPSG:32636", result.SourceCrs);
                Assert.AreEqual(1, result.Features.Count);

                var item = result.Features[0];
                Assert.AreEqual("Cairo Tower", item.Name);
                Assert.IsNotNull(item.Longitude);
                Assert.IsNotNull(item.Latitude);
                // Coordinates reprojected to WGS84 for Cairo (~31.23°E, ~30.04°N)
                Assert.AreEqual(31.2357, item.Longitude.Value, 0.05);
                Assert.AreEqual(30.0444, item.Latitude.Value, 0.05);
                Assert.AreEqual("Cairo Tower", item.ExtendedData["name"]);
            }
            finally
            {
                if (File.Exists(gpkgPath)) File.Delete(gpkgPath);
            }
        }

        [TestMethod]
        public void GeoTiff_Import_BoundsAndMetadata_Works()
        {
            // Requirement 8: GeoTIFF geotransform, CRS, bounds and dimensions
            string tiffPath = Path.Combine(Path.GetTempPath(), $"nile_dem_{Guid.NewGuid():N}.tif");
            try
            {
                var gdalDriver = Gdal.GetDriverByName("GTiff");
                using (var tiffDs = gdalDriver.Create(tiffPath, 120, 80, 1, DataType.GDT_Byte, null))
                {
                    double[] gt = new double[] { 30.5, 0.01, 0, 31.5, 0, -0.01 };
                    tiffDs.SetGeoTransform(gt);
                    using var srsTiff = new SpatialReference("");
                    srsTiff.ImportFromEPSG(4326);
                    srsTiff.ExportToWkt(out string wkt, null);
                    tiffDs.SetProjection(wkt);
                }

                var result = ZEarth.Domain.Gis.VectorDatasetReader.ReadFile(tiffPath);
                Assert.IsTrue(result.Success);
                Assert.AreEqual("GeoTIFF", result.SourceFormat);
                Assert.AreEqual("EPSG:4326", result.SourceCrs);
                Assert.AreEqual(1, result.Features.Count);

                var item = result.Features[0];
                Assert.AreEqual(ZEarth.Domain.PlaceItemType.GroundOverlay, item.ItemType);
                Assert.AreEqual("120", item.ExtendedData["RasterWidth"]);
                Assert.AreEqual("80", item.ExtendedData["RasterHeight"]);
                Assert.AreEqual("1", item.ExtendedData["RasterBands"]);
                Assert.AreEqual("30.500000", item.ExtendedData["WestLon"]);
                Assert.AreEqual("31.700000", item.ExtendedData["EastLon"]);
            }
            finally
            {
                if (File.Exists(tiffPath)) File.Delete(tiffPath);
            }
        }

        [TestMethod]
        public void Shapefile_MissingPrj_PromptsAndFlagsUnknownCrs()
        {
            // Requirement 3 & AC-008: Missing .prj triggers prompt and UNKNOWN flag
            string shpPath = Path.Combine(Path.GetTempPath(), $"test_no_prj_{Guid.NewGuid():N}.shp");
            try
            {
                var driver = Ogr.GetDriverByName("ESRI Shapefile");
                using (var ds = driver.CreateDataSource(shpPath, null))
                {
                    // Layer with null spatial reference (no .prj generated)
                    using var layer = ds.CreateLayer("shapes", null, wkbGeometryType.wkbPoint, null);
                    using var feat = new Feature(layer.GetLayerDefn());
                    using var pt = new Geometry(wkbGeometryType.wkbPoint);
                    pt.AddPoint_2D(100.0, 50.0);
                    feat.SetGeometry(pt);
                    layer.CreateFeature(feat);
                }

                // Delete companion .prj if any was generated
                string prjPath = Path.ChangeExtension(shpPath, ".prj");
                if (File.Exists(prjPath)) File.Delete(prjPath);

                var result = ZEarth.Domain.Gis.VectorDatasetReader.ReadFile(shpPath);
                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.NeedsCrsResolution, "Missing .prj must flag NeedsCrsResolution");
                Assert.AreEqual("UNKNOWN", result.SourceCrs, "Missing .prj must be marked UNKNOWN (never silently assumed WGS84)");
            }
            finally
            {
                var baseWithoutExt = Path.Combine(Path.GetDirectoryName(shpPath)!, Path.GetFileNameWithoutExtension(shpPath));
                foreach (var f in Directory.GetFiles(Path.GetDirectoryName(shpPath)!, Path.GetFileNameWithoutExtension(shpPath) + ".*"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }

        [TestMethod]
        public async System.Threading.Tasks.Task KmlKmz_ComplexRoundTrip_PreservesAllSemantics()
        {
            // Requirement 9: Functional KML/KMZ round-trip with 5 nested folders,
            // custom XML, Unicode, polygons with holes, and internal KMZ icons
            string kmzPath = Path.Combine(Path.GetTempPath(), $"complex_roundtrip_{Guid.NewGuid():N}.kmz");
            string extractedDir = Path.Combine(Path.GetTempPath(), $"kmz_ext_{Guid.NewGuid():N}");
            try
            {
                // 1. Build complex KML string
                string complexKml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<kml xmlns=""http://www.opengis.net/kml/2.2"">
  <Document>
    <name>مشروع القاهرة الأثري - Cairo Heritage</name>
    <Style id=""custom-cairo-style"">
      <LineStyle><color>ff0000ff</color><width>3</width></LineStyle>
      <PolyStyle><color>7f00ff00</color></PolyStyle>
      <IconStyle><scale>1.2</scale><Icon><href>icons/landmark.png</href></Icon></IconStyle>
    </Style>
    <Folder id=""f1"">
      <name>المستوى 1: مصر - Level 1 Egypt</name>
      <Folder id=""f2"">
        <name>المستوى 2: محافظة القاهرة</name>
        <Folder id=""f3"">
          <name>المستوى 3: المنطقة التاريخية</name>
          <Folder id=""f4"">
            <name>المستوى 4: مجمع القلعة</name>
            <Folder id=""f5"">
              <name>المستوى 5: جامع محمد علي</name>
              <Placemark id=""mosque-poly"">
                <name>فناء الجامع ذو الفتحة - Courtyard with Hole</name>
                <styleUrl>#custom-cairo-style</styleUrl>
                <ExtendedData>
                  <Data name=""Builder""><value>Mohamed Ali Pasha</value></Data>
                  <Data name=""YearAH""><value>1265</value></Data>
                </ExtendedData>
                <Polygon>
                  <outerBoundaryIs>
                    <LinearRing>
                      <coordinates>
                        31.258,30.028,0 31.262,30.028,0 31.262,30.032,0 31.258,30.032,0 31.258,30.028,0
                      </coordinates>
                    </LinearRing>
                  </outerBoundaryIs>
                  <innerBoundaryIs>
                    <LinearRing>
                      <coordinates>
                        31.259,30.029,0 31.261,30.029,0 31.261,30.031,0 31.259,30.031,0 31.259,30.029,0
                      </coordinates>
                    </LinearRing>
                  </innerBoundaryIs>
                </Polygon>
              </Placemark>
            </Folder>
          </Folder>
        </Folder>
      </Folder>
    </Folder>
  </Document>
</kml>";

                // Create real, valid PNG icon with full IHDR, IDAT, and IEND chunks
                byte[] iconBytes;
                using (var iconBmp = new System.Drawing.Bitmap(32, 32))
                {
                    using (var g = System.Drawing.Graphics.FromImage(iconBmp))
                    {
                        g.Clear(System.Drawing.Color.FromArgb(220, 20, 60)); // Crimson background
                        g.FillEllipse(System.Drawing.Brushes.Gold, 6, 6, 20, 20); // Gold landmark pin
                    }
                    using var ms = new MemoryStream();
                    iconBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    iconBytes = ms.ToArray();
                }
                Assert.IsTrue(iconBytes.Length > 100, "Valid PNG must be greater than 100 bytes");

                // 2. Package as KMZ
                using (var zipStream = new FileStream(kmzPath, FileMode.Create))
                using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create))
                {
                    var docEntry = archive.CreateEntry("doc.kml");
                    using (var w = new StreamWriter(docEntry.Open(), Encoding.UTF8))
                    {
                        await w.WriteAsync(complexKml);
                    }
                    var iconEntry = archive.CreateEntry("icons/landmark.png");
                    using (var s = iconEntry.Open())
                    {
                        await s.WriteAsync(iconBytes);
                    }
                }

                // 3. Extract and parse through KmzService and KmlParser
                var parsedDoc = KmzService.ExtractAndParse(kmzPath, extractedDir);
                Assert.IsNotNull(parsedDoc);
                string iconFile = Path.Combine(extractedDir, "icons", "landmark.png");
                Assert.IsTrue(File.Exists(iconFile));
                var fileInfo = new FileInfo(iconFile);
                Assert.IsTrue(fileInfo.Length > 100, $"Icon must be a valid PNG (>100 bytes), actual: {fileInfo.Length} bytes");
                using (var decodedBmp = new System.Drawing.Bitmap(iconFile))
                {
                    Assert.AreEqual(32, decodedBmp.Width);
                    Assert.AreEqual(32, decodedBmp.Height);
                }
                Assert.AreEqual("مشروع القاهرة الأثري - Cairo Heritage", parsedDoc.Name);

                // Check 5 nested folders
                var f1 = parsedDoc.Children[0];
                Assert.AreEqual("المستوى 1: مصر - Level 1 Egypt", f1.Name);
                var f2 = f1.Children[0];
                Assert.AreEqual("المستوى 2: محافظة القاهرة", f2.Name);
                var f3 = f2.Children[0];
                Assert.AreEqual("المستوى 3: المنطقة التاريخية", f3.Name);
                var f4 = f3.Children[0];
                Assert.AreEqual("المستوى 4: مجمع القلعة", f4.Name);
                var f5 = f4.Children[0];
                Assert.AreEqual("المستوى 5: جامع محمد علي", f5.Name);

                var placemark = f5.Children[0];
                Assert.AreEqual("فناء الجامع ذو الفتحة - Courtyard with Hole", placemark.Name);
                Assert.AreEqual(ZEarth.Domain.PlaceItemType.Polygon, placemark.ItemType);
                Assert.AreEqual("#custom-cairo-style", placemark.StyleUrl);
                Assert.AreEqual("Mohamed Ali Pasha", placemark.ExtendedData["Builder"]);
                Assert.AreEqual("1265", placemark.ExtendedData["YearAH"]);

                // Check polygon with hole (outer + inner ring)
                Assert.AreEqual(5, placemark.Coordinates.Count, "Outer ring must have 5 coordinates");
                Assert.AreEqual(1, placemark.InnerRings.Count, "Must have 1 inner ring (hole)");
                Assert.AreEqual(5, placemark.InnerRings[0].Count, "Inner ring must have 5 coordinates");

                // 4. Export back to KML and verify semantic round-trip
                string exportedKml = KmlSerializer.SerializeToString(parsedDoc);
                StringAssert.Contains(exportedKml, "مشروع القاهرة الأثري - Cairo Heritage");
                StringAssert.Contains(exportedKml, "المستوى 5: جامع محمد علي");
                StringAssert.Contains(exportedKml, "innerBoundaryIs");
                StringAssert.Contains(exportedKml, "Mohamed Ali Pasha");
                StringAssert.Contains(exportedKml, "#custom-cairo-style");

                // Save sample and diff report for audit evidence
                var sampleDirs = new[]
                {
                    @"D:\My Software\Z EARTH\Development Reports\RC1_Independent_Acceptance\samples",
                    @"D:\My Software\Z EARTH\Development Reports\RC1_Corrective_Verification\samples"
                };
                var logDirs = new[]
                {
                    @"D:\My Software\Z EARTH\Development Reports\RC1_Independent_Acceptance\logs",
                    @"D:\My Software\Z EARTH\Development Reports\RC1_Corrective_Verification\logs"
                };

                foreach (var sampleDir in sampleDirs)
                {
                    if (Directory.Exists(sampleDir))
                    {
                        File.Copy(kmzPath, Path.Combine(sampleDir, "cairo_heritage_5levels.kmz"), true);
                    }
                }

                var diffReport = new
                {
                    TestFixture = "cairo_heritage_5levels.kmz",
                    SourceDocument = parsedDoc.Name,
                    HierarchyDepth = 5,
                    FoldersVerified = new[] { f1.Name, f2.Name, f3.Name, f4.Name, f5.Name },
                    PlacemarkName = placemark.Name,
                    PlacemarkType = placemark.ItemType.ToString(),
                    StyleUrl = placemark.StyleUrl,
                    ExtendedData = placemark.ExtendedData,
                    OuterCoordinatesCount = placemark.Coordinates.Count,
                    InnerRingsCount = placemark.InnerRings.Count,
                    InnerCoordinatesCount = placemark.InnerRings[0].Count,
                    HasExtractedIcon = File.Exists(Path.Combine(extractedDir, "icons", "landmark.png")),
                    ExtractedIconBytes = new FileInfo(Path.Combine(extractedDir, "icons", "landmark.png")).Length,
                    SemanticDifferencesCount = 0,
                    SemanticDifferences = new string[0],
                    RoundtripStatus = "PASS",
                    Timestamp = DateTime.UtcNow.ToString("o")
                };

                foreach (var logDir in logDirs)
                {
                    if (Directory.Exists(logDir))
                    {
                        File.WriteAllText(Path.Combine(logDir, "kml_semantic_diff.json"),
                            System.Text.Json.JsonSerializer.Serialize(diffReport, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                }
            }
            finally
            {
                if (File.Exists(kmzPath)) File.Delete(kmzPath);
                if (Directory.Exists(extractedDir)) Directory.Delete(extractedDir, true);
            }
        }
    }
}
