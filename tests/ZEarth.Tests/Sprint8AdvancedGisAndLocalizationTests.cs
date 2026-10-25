using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Gis;
using ZEarth.Domain.Imagery;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint8AdvancedGisAndLocalizationTests
    {
        private string _testDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ZEarth_Sprint8_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        [TestMethod]
        public void VectorDatasetReader_ReadGeoJson_ParsesPointsAndPolygons()
        {
            // Arrange
            var geoJsonPath = Path.Combine(_testDir, "cairo_sites.geojson");
            var geoJsonContent = @"{
                ""type"": ""FeatureCollection"",
                ""features"": [
                    {
                        ""type"": ""Feature"",
                        ""properties"": { ""name"": ""Cairo Tower"", ""category"": ""Landmark"" },
                        ""geometry"": {
                            ""type"": ""Point"",
                            ""coordinates"": [31.2298, 30.0459, 187.0]
                        }
                    },
                    {
                        ""type"": ""Feature"",
                        ""properties"": { ""name"": ""Tahrir Square"", ""category"": ""Plaza"" },
                        ""geometry"": {
                            ""type"": ""Polygon"",
                            ""coordinates"": [
                                [
                                    [31.2330, 30.0440, 0.0],
                                    [31.2360, 30.0440, 0.0],
                                    [31.2360, 30.0460, 0.0],
                                    [31.2330, 30.0460, 0.0],
                                    [31.2330, 30.0440, 0.0]
                                ]
                            ]
                        }
                    }
                ]
            }";
            File.WriteAllText(geoJsonPath, geoJsonContent, Encoding.UTF8);

            // Act
            var result = VectorDatasetReader.ReadFile(geoJsonPath);

            // Assert
            Assert.IsTrue(result.Success, "GeoJSON import should succeed");
            Assert.AreEqual("GeoJSON", result.SourceFormat);
            Assert.AreEqual(2, result.Features.Count, "Should parse 2 features");

            var point = result.Features[0];
            Assert.AreEqual("Cairo Tower", point.Name);
            Assert.AreEqual(PlaceItemType.Placemark, point.ItemType);
            Assert.IsNotNull(point.Longitude);
            Assert.IsNotNull(point.Latitude);
            Assert.AreEqual(31.2298, point.Longitude.Value, 0.0001);
            Assert.AreEqual(30.0459, point.Latitude.Value, 0.0001);

            var poly = result.Features[1];
            Assert.AreEqual("Tahrir Square", poly.Name);
            Assert.AreEqual(PlaceItemType.Polygon, poly.ItemType);
            Assert.AreEqual(5, poly.Coordinates.Count);

            // Bounds check
            Assert.IsTrue(result.MinLon <= 31.2298 && result.MaxLon >= 31.2360);
            Assert.IsTrue(result.MinLat <= 30.0440 && result.MaxLat >= 30.0460);
        }

        [TestMethod]
        public void VectorDatasetReader_ReadGeoJson_ReprojectsUtm36N_ToWgs84()
        {
            // Arrange: Feature in UTM Zone 36N coordinates for Cairo
            var geoJsonPath = Path.Combine(_testDir, "utm_feature.geojson");
            var geoJsonContent = @"{
                ""type"": ""FeatureCollection"",
                ""crs"": { ""type"": ""name"", ""properties"": { ""name"": ""urn:ogc:def:crs:EPSG::32636"" } },
                ""features"": [
                    {
                        ""type"": ""Feature"",
                        ""properties"": { ""name"": ""Cairo UTM Projected"" },
                        ""geometry"": {
                            ""type"": ""Point"",
                            ""coordinates"": [329900.0, 3325017.0]
                        }
                    }
                ]
            }";
            File.WriteAllText(geoJsonPath, geoJsonContent, Encoding.UTF8);

            // Act
            var result = VectorDatasetReader.ReadFile(geoJsonPath);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("EPSG:32636", result.SourceCrs);
            Assert.AreEqual(1, result.Features.Count);

            var feature = result.Features[0];
            Assert.IsNotNull(feature.Longitude);
            Assert.IsNotNull(feature.Latitude);
            // Cairo longitude ~ 31.2357, latitude ~ 30.0444
            Assert.AreEqual(31.2357, feature.Longitude.Value, 0.01, "UTM 36N Easting should reproject to Cairo Longitude");
            Assert.AreEqual(30.0444, feature.Latitude.Value, 0.01, "UTM 36N Northing should reproject to Cairo Latitude");
        }

        [TestMethod]
        public void VectorDatasetReader_ReadShapefile_MissingPrj_FlagsNeedsCrsResolution()
        {
            // Arrange: Build a minimal valid ESRI Shapefile containing 1 Point (ShapeType=1), WITHOUT .prj file
            var shpPath = Path.Combine(_testDir, "unprojected_points.shp");
            CreateMinimalPointShapefile(shpPath, 31.2357, 30.0444);

            // Act
            var result = VectorDatasetReader.ReadFile(shpPath);

            // Assert
            Assert.IsTrue(result.Success, "Should parse shapefile geometry");
            Assert.IsTrue(result.NeedsCrsResolution, "Missing .prj must flag NeedsCrsResolution (AC-008)");
            Assert.AreEqual("UNKNOWN", result.SourceCrs);
            Assert.AreEqual(1, result.Features.Count);
        }

        [TestMethod]
        public void VectorDatasetReader_ReadShapefile_WithPrj_ParsesCrsAndCoordinates()
        {
            // Arrange: Build a minimal valid ESRI Shapefile with companion .prj file
            var shpPath = Path.Combine(_testDir, "alexandria_site.shp");
            var prjPath = Path.Combine(_testDir, "alexandria_site.prj");
            CreateMinimalPointShapefile(shpPath, 29.9187, 31.2001);
            File.WriteAllText(prjPath, "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]");

            // Act
            var result = VectorDatasetReader.ReadFile(shpPath);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.NeedsCrsResolution, "With valid .prj, NeedsCrsResolution should be false");
            Assert.AreEqual("EPSG:4326", result.SourceCrs);
            Assert.AreEqual(1, result.Features.Count);

            var feat = result.Features[0];
            Assert.IsNotNull(feat.Longitude);
            Assert.IsNotNull(feat.Latitude);
            Assert.AreEqual(29.9187, feat.Longitude.Value, 0.0001);
            Assert.AreEqual(31.2001, feat.Latitude.Value, 0.0001);
        }

        [TestMethod]
        public void SpatialRTree_InsertAndQuery_ReturnsContainedEntities()
        {
            // Arrange: Spatial index with Egypt and non-Egypt cities
            var tree = new SpatialRTree<string>();
            tree.InsertPoint("Cairo", 31.2357, 30.0444);
            tree.InsertPoint("Alexandria", 29.9187, 31.2001);
            tree.InsertPoint("Aswan", 32.8998, 24.0889);
            tree.InsertPoint("London", -0.1276, 51.5074);
            tree.InsertPoint("Tokyo", 139.6917, 35.6895);

            Assert.AreEqual(5, tree.Count);

            // Act: Query Egypt Bounding Box: Lon [24..37], Lat [22..32]
            var egyptCities = tree.Query(24.0, 22.0, 37.0, 32.0);

            // Assert
            Assert.AreEqual(3, egyptCities.Count);
            CollectionAssert.Contains(egyptCities, "Cairo");
            CollectionAssert.Contains(egyptCities, "Alexandria");
            CollectionAssert.Contains(egyptCities, "Aswan");
            Assert.IsFalse(egyptCities.Contains("London"));
            Assert.IsFalse(egyptCities.Contains("Tokyo"));
        }

        [TestMethod]
        public void SpatialRTree_HighCapacity_QueryPerformance()
        {
            // Arrange: 10,000 entities
            var tree = new SpatialRTree<int>();
            var rnd = new Random(42);

            for (int i = 0; i < 10000; i++)
            {
                double lon = (rnd.NextDouble() * 360.0) - 180.0;
                double lat = (rnd.NextDouble() * 160.0) - 80.0;
                tree.InsertPoint(i, lon, lat);
            }

            Assert.AreEqual(10000, tree.Count);

            // Act: Benchmark spatial query
            var sw = Stopwatch.StartNew();
            var results = tree.Query(20.0, 20.0, 40.0, 40.0);
            sw.Stop();

            // Assert: Query across 10,000 spatial records should complete in under 50 ms
            Assert.IsTrue(sw.ElapsedMilliseconds < 50, $"R-Tree spatial query took {sw.ElapsedMilliseconds}ms (must be < 50ms)");
            Assert.IsTrue(results.Count > 0, "Query should find entities inside the 20x20 degree box");
        }

        [TestMethod]
        public void LocalizationService_EnglishAndArabic_StringsAndFlowDirection()
        {
            var loc = LocalizationService.Instance;

            // Test English defaults
            loc.SetLanguage("en");
            Assert.IsFalse(loc.IsArabic);
            Assert.AreEqual("Project", loc.GetString("Tab.Project"));
            Assert.AreEqual("Measure Distance", loc.GetString("Measure.Distance"));
            Assert.AreEqual("Elevation Model", loc.GetString("Terrain.Model"));

            // Switch to Arabic
            loc.SetLanguage("ar");
            Assert.IsTrue(loc.IsArabic);
            Assert.AreEqual("المشروع", loc.GetString("Tab.Project"));
            Assert.AreEqual("قياس المسافة", loc.GetString("Measure.Distance"));
            Assert.AreEqual("نموذج الارتفاع الرقمي", loc.GetString("Terrain.Model"));

            // Toggle back to English
            loc.ToggleLanguage();
            Assert.IsFalse(loc.IsArabic);
            Assert.AreEqual("Project", loc.GetString("Tab.Project"));
        }

        [TestMethod]
        public void HistoricalLayerInfo_BadgeFormatting_AndMetadata()
        {
            var date = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var info = new HistoricalLayerInfo("s2_20250615", "Sentinel-2 Nile Delta Summer 2025", date, "Sentinel-2 MSI", 10.0, 2.5);

            Assert.AreEqual("s2_20250615", info.LayerId);
            Assert.AreEqual("Sentinel-2 Nile Delta Summer 2025", info.Title);
            Assert.AreEqual(date, info.AcquisitionDate);
            Assert.AreEqual(date.AddDays(2), info.PublicationDate);
            Assert.AreEqual("Sentinel-2 MSI", info.Sensor);
            Assert.AreEqual(10.0, info.ResolutionMeters);
            Assert.AreEqual(2.5, info.CloudCoverPercentage);

            var badge = info.GetFormattedBadge();
            StringAssert.Contains(badge, "2025-06-15");
            StringAssert.Contains(badge, "Sentinel-2 MSI");
            StringAssert.Contains(badge, "10.0m");
            StringAssert.Contains(badge, "2.5%");
        }

        [TestMethod]
        public void VectorDatasetReader_ReprojectsEgyptRedBelt_ToWgs84()
        {
            // False Easting 615000, False Northing 810000 -> Origin (31°E, 30°N)
            var (lon, lat) = VectorDatasetReader.ReprojectToWgs84(615000.0, 810000.0, "EPSG:22992");
            Assert.AreEqual(31.0, lon, 0.05, "Origin Easting should correspond to 31.0° E");
            Assert.AreEqual(30.0, lat, 0.05, "Origin Northing should correspond to 30.0° N");
        }

        private static void CreateMinimalPointShapefile(string shpPath, double lon, double lat)
        {
            using var fs = File.Create(shpPath);
            using var bw = new BinaryWriter(fs);

            // Shapefile Header (100 bytes)
            // Big-endian file code (9994)
            bw.Write(new byte[] { 0x00, 0x00, 0x27, 0x0A });
            // 5 unused 32-bit ints
            bw.Write(new byte[20]);
            // File length in 16-bit words (50 words for header + 14 words for 1 record = 64 words = 128 bytes)
            int fileLengthWords = 64;
            bw.Write(new byte[] { 0x00, 0x00, 0x00, (byte)fileLengthWords });
            // Version (1000) Little-endian
            bw.Write(1000);
            // ShapeType (1 = Point) Little-endian
            bw.Write(1);
            // Bounding box: xmin, ymin, xmax, ymax (all doubles)
            bw.Write(lon);
            bw.Write(lat);
            bw.Write(lon);
            bw.Write(lat);
            // zmin, zmax, mmin, mmax (4 doubles)
            bw.Write(0.0);
            bw.Write(0.0);
            bw.Write(0.0);
            bw.Write(0.0);

            // Record 1 Header:
            // Record number (1) Big-endian
            bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 });
            // Content length in 16-bit words: 10 words (4 bytes shapeType + 16 bytes coords = 20 bytes = 10 words)
            bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x0A });

            // Record 1 Contents:
            // ShapeType (1 = Point) Little-endian
            bw.Write(1);
            // X (lon), Y (lat) doubles
            bw.Write(lon);
            bw.Write(lat);
        }
    }
}
