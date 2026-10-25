using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Kml;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint4CrsAndKmlTests
    {
        private static string GetFixturePath(string fileName)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ZEarth.sln")))
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }

            var fixturePath = Path.Combine(dir ?? AppDomain.CurrentDomain.BaseDirectory, "tests", "ZEarth.Tests", "fixtures", fileName);
            if (File.Exists(fixturePath)) return fixturePath;

            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", fileName);
            if (File.Exists(localPath)) return localPath;

            throw new FileNotFoundException($"KML test fixture not found: {fileName} at {fixturePath}");
        }

        [TestMethod]
        public void Crs_CairoReferenceCoordinates_TransformsToUtm36NAndMgrsWithExactPrecision()
        {
            // Authoritative Cairo coordinate: 30.0444 N, 31.2357 E
            double lon = 31.2357;
            double lat = 30.0444;

            // 1. UTM Zone 36N verification
            var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
            Assert.AreEqual(36, utm.Zone);
            Assert.IsTrue(utm.IsNorthernHemisphere);
            Assert.AreEqual(329900.2, utm.Easting, 1.0, "Cairo UTM Easting should be ~329900 m");
            Assert.AreEqual(3325017.2, utm.Northing, 1.0, "Cairo UTM Northing should be ~3325017 m");

            // 2. Latitude Band and GZD
            char band = MgrsConverter.GetLatitudeBand(lat);
            Assert.AreEqual('R', band);

            // 3. 100k Square ID
            string sq = MgrsConverter.FromUtm(utm.Zone, band, utm.Easting, utm.Northing, 0);
            Assert.AreEqual("36RUU", sq);

            // 4. Multi-precision standard truncation
            string m1 = MgrsConverter.FromLatLon(lon, lat, 5); // 1m precision (10 digits)
            Assert.AreEqual("36RUU2989925016", m1);

            string m10 = MgrsConverter.FromLatLon(lon, lat, 4); // 10m precision (8 digits)
            Assert.AreEqual("36RUU29892501", m10);

            string m100 = MgrsConverter.FromLatLon(lon, lat, 3); // 100m precision (6 digits)
            Assert.AreEqual("36RUU298250", m100);

            string m1k = MgrsConverter.FromLatLon(lon, lat, 2); // 1km precision (4 digits)
            Assert.AreEqual("36RUU2925", m1k);

            string m10k = MgrsConverter.FromLatLon(lon, lat, 1); // 10km precision (2 digits)
            Assert.AreEqual("36RUU22", m10k);
        }

        [TestMethod]
        public void Crs_AlexandriaReferenceCoordinates_TransformsToUtm35NAndMgrsWithExactPrecision()
        {
            // Authoritative Alexandria coordinate: 31.2001 N, 29.9187 E
            double lon = 29.9187;
            double lat = 31.2001;

            // Longitude 29.9187 E is strictly west of the 30°E meridian -> UTM Zone 35N
            var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
            Assert.AreEqual(35, utm.Zone);
            Assert.IsTrue(utm.IsNorthernHemisphere);
            Assert.AreEqual(778109.4, utm.Easting, 1.0, "Alexandria UTM Easting should be ~778109 m");
            Assert.AreEqual(3455449.8, utm.Northing, 1.0, "Alexandria UTM Northing should be ~3455450 m");

            // Latitude Band and GZD
            char band = MgrsConverter.GetLatitudeBand(lat);
            Assert.AreEqual('R', band);

            // 100k Square ID
            string sq = MgrsConverter.FromUtm(utm.Zone, band, utm.Easting, utm.Northing, 0);
            Assert.AreEqual("35RQQ", sq);

            // Multi-precision standard truncation
            string m1 = MgrsConverter.FromLatLon(lon, lat, 5);
            Assert.AreEqual("35RQQ7810955449", m1);

            string m10 = MgrsConverter.FromLatLon(lon, lat, 4);
            Assert.AreEqual("35RQQ78105544", m10);

            string m100 = MgrsConverter.FromLatLon(lon, lat, 3);
            Assert.AreEqual("35RQQ781554", m100);

            string m1k = MgrsConverter.FromLatLon(lon, lat, 2);
            Assert.AreEqual("35RQQ7855", m1k);

            string m10k = MgrsConverter.FromLatLon(lon, lat, 1);
            Assert.AreEqual("35RQQ75", m10k);
        }

        [TestMethod]
        public void Crs_UtmInverseTransform_RoundTripAccurateToSubMillimeter()
        {
            double lon = 31.2357;
            double lat = 30.0444;

            var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
            var (revLon, revLat) = CoordinateTransformService.UtmToWgs84(utm.Easting, utm.Northing, utm.Zone, utm.IsNorthernHemisphere);

            Assert.AreEqual(lon, revLon, 1e-7, "Longitude round-trip should be exact to sub-millimeter");
            Assert.AreEqual(lat, revLat, 1e-7, "Latitude round-trip should be exact to sub-millimeter");
        }

        [TestMethod]
        public void Crs_CoordinateFormatter_ProducesFormattedStrings()
        {
            double lon = 31.2357;
            double lat = 30.0444;

            string dd = CoordinateFormatter.Format(lon, lat, CoordinateDisplayFormat.DecimalDegrees);
            Assert.IsTrue(dd.Contains("30.0444° N"));
            Assert.IsTrue(dd.Contains("31.2357° E"));

            string dms = CoordinateFormatter.Format(lon, lat, CoordinateDisplayFormat.DegreesMinutesSeconds);
            Assert.IsTrue(dms.Contains("30° 02'"));
            Assert.IsTrue(dms.Contains("31° 14'"));

            string utmStr = CoordinateFormatter.Format(lon, lat, CoordinateDisplayFormat.Utm);
            Assert.IsTrue(utmStr.Contains("UTM Zone 36N"));
            Assert.IsTrue(utmStr.Contains("329900m E"));

            string mgrsStr = CoordinateFormatter.Format(lon, lat, CoordinateDisplayFormat.Mgrs);
            Assert.IsTrue(mgrsStr.Contains("MGRS 36R UU 29899 25016"));
        }

        [TestMethod]
        public void Crs_Registry_ProvidesCoreAndEgyptRegionalSystems()
        {
            var registry = new CrsRegistry();
            var all = registry.GetAll();
            Assert.IsTrue(all.Count >= 10);

            // Verify WGS 84 and Web Mercator
            Assert.IsNotNull(registry.GetByEpsg(4326));
            Assert.IsNotNull(registry.GetByEpsg(3857));

            // Verify Egypt systems
            Assert.IsNotNull(registry.GetByEpsg(32635));
            Assert.IsNotNull(registry.GetByEpsg(32636));
            Assert.IsNotNull(registry.GetByEpsg(22992));

            // Search query
            var egyptCrs = registry.Search("Egypt").ToList();
            Assert.IsTrue(egyptCrs.Count >= 3);
        }

        [TestMethod]
        public void Kml_NestedPlacesFixture_PreservesHierarchyAndExtendedDataRoundTrip()
        {
            var path = GetFixturePath("nested-places.kml");
            var parsed = KmlParser.ParseFile(path);

            Assert.IsNotNull(parsed);
            Assert.AreEqual("Z EARTH Fixture — مصر", parsed.Name);

            // Verify top-level preserved style
            Assert.IsTrue(parsed.PreservedXmlFragments.Any(f => f.Contains("blue") && f.Contains("IconStyle")));

            // Check nested folders: Egypt -> Cairo/القاهرة
            Assert.AreEqual(1, parsed.Children.Count);
            var egyptFolder = parsed.Children[0];
            Assert.AreEqual("Egypt", egyptFolder.Name);

            Assert.AreEqual(1, egyptFolder.Children.Count);
            var cairoFolder = egyptFolder.Children[0];
            Assert.AreEqual("Cairo/القاهرة", cairoFolder.Name);

            Assert.AreEqual(1, cairoFolder.Children.Count);
            var placemark = cairoFolder.Children[0];
            Assert.AreEqual("Demo Point", placemark.Name);
            Assert.AreEqual("#blue", placemark.StyleUrl);
            Assert.AreEqual(31.2357, placemark.Longitude!.Value, 1e-4);
            Assert.AreEqual(30.0444, placemark.Latitude!.Value, 1e-4);
            Assert.AreEqual(36.0, placemark.Altitude!.Value, 0.1);

            // ExtendedData
            Assert.IsTrue(placemark.ExtendedData.ContainsKey("Test_Field"));
            Assert.AreEqual("Preserve me", placemark.ExtendedData["Test_Field"]);

            // Round-Trip Test: Serialize back and verify semantic equality
            string serializedXml = KmlSerializer.SerializeToString(parsed);
            Assert.IsTrue(serializedXml.Contains("Demo Point"));
            Assert.IsTrue(serializedXml.Contains("Cairo/القاهرة"));
            Assert.IsTrue(serializedXml.Contains("Preserve me"));
            Assert.IsTrue(serializedXml.Contains("31.2357,30.0444,36"),
                "KML output must retain double coordinate precision without truncation.");

            // Re-parse serialized output
            var reParsed = KmlParser.ParseDocument(System.Xml.Linq.XDocument.Parse(serializedXml));
            Assert.AreEqual("Z EARTH Fixture — مصر", reParsed.Name);
            var repEgypt = reParsed.Children[0];
            var repCairo = repEgypt.Children[0];
            var repPlacemark = repCairo.Children[0];
            Assert.AreEqual("Demo Point", repPlacemark.Name);
            Assert.AreEqual("Preserve me", repPlacemark.ExtendedData["Test_Field"]);
        }

        [TestMethod]
        public void Kml_GeometryTimeFixture_ParsesLineStringPolygonHolesAndTimeSpan()
        {
            var path = GetFixturePath("geometry-time.kml");
            var parsed = KmlParser.ParseFile(path);

            Assert.IsNotNull(parsed);
            Assert.AreEqual("GeometryTimeTest", parsed.Name);

            // Placemark 1: Route (LineString + TimeSpan)
            var route = parsed.Children.FirstOrDefault(c => c.Name == "Route");
            Assert.IsNotNull(route);
            Assert.AreEqual(PlaceItemType.Path, route.ItemType);
            Assert.AreEqual("2020-01-01", route.TimeSpanBegin);
            Assert.AreEqual("2020-02-01", route.TimeSpanEnd);
            Assert.AreEqual(3, route.Coordinates.Count);

            // Placemark 2: Polygon with hole
            var poly = parsed.Children.FirstOrDefault(c => c.Name == "Polygon with hole");
            Assert.IsNotNull(poly);
            Assert.AreEqual(PlaceItemType.Polygon, poly.ItemType);
            Assert.IsTrue(poly.Coordinates.Count >= 4, "Outer ring must have coordinates");
            Assert.AreEqual(1, poly.InnerRings.Count, "Should have 1 inner ring (hole)");
            Assert.IsTrue(poly.InnerRings[0].Count >= 4, "Inner ring must have coordinates");
        }

        [TestMethod]
        public void Kmz_PackageAndExtract_SafelyExtractsKmlTree()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"kmz_test_{Guid.NewGuid():N}");
            var kmzFile = Path.Combine(tempDir, "test.kmz");
            var extractDir = Path.Combine(tempDir, "extracted");

            try
            {
                Directory.CreateDirectory(tempDir);

                var root = new PlaceItem("KMZ Package Root", PlaceItemType.Folder);
                var p1 = new PlaceItem("Cairo Placemark", PlaceItemType.Placemark, 30.0444, 31.2357, 45.0);
                p1.ExtendedData["Author"] = "Ahmad Ibrahim";
                root.AddChild(p1);

                // Create KMZ
                KmzService.CreatePackage(kmzFile, root);
                Assert.IsTrue(File.Exists(kmzFile));

                // Extract and re-parse
                var extractedRoot = KmzService.ExtractAndParse(kmzFile, extractDir);
                Assert.IsNotNull(extractedRoot);
                Assert.AreEqual("KMZ Package Root", extractedRoot.Name);
                Assert.AreEqual(1, extractedRoot.Children.Count);
                Assert.AreEqual("Cairo Placemark", extractedRoot.Children[0].Name);
                Assert.AreEqual("Ahmad Ibrahim", extractedRoot.Children[0].ExtendedData["Author"]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
