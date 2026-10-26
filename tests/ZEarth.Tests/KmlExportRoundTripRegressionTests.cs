using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Kml;
using ZEarth.Domain.Persistence;

namespace ZEarth.Tests;

[TestClass]
public sealed class KmlExportRoundTripRegressionTests
{
    [TestMethod]
    public void ExportSelectedSinglePlacemarkContainsActualGeometry()
    {
        var item = new PlaceItem("Cairo 📍", PlaceItemType.Placemark, 30.0444, 31.2357, 52.5);
        item.StyleUrl = "#style-example";
        var kml = KmlSerializer.SerializeToString(item);
        var xml = XDocument.Parse(kml);
        Assert.AreEqual(1, xml.Descendants().Count(e => e.Name.LocalName == "Placemark"));
        Assert.AreEqual(1, xml.Descendants().Count(e => e.Name.LocalName == "Point"));
        Assert.IsTrue(kml.Contains("31.2357,30.0444,52.5"));
        var reopened = KmlParser.ParseDocument(xml);
        Assert.AreEqual(1, reopened.Children.Count);
        Assert.AreEqual(item.Name, reopened.Children[0].Name);
        Assert.AreEqual(item.Longitude, reopened.Children[0].Longitude);
        Assert.AreEqual("#style-example", reopened.Children[0].StyleUrl);
    }

    [TestMethod]
    public void ExportSelectedLineAndPolygonRetainsGeometryAndPolygonHole()
    {
        var line = new PlaceItem("Nile transect", PlaceItemType.Path);
        line.Coordinates.Add(new CoordinatePoint(31, 30, 10));
        line.Coordinates.Add(new CoordinatePoint(31.1, 30.1, 20));
        var lineReopened = KmlParser.ParseDocument(XDocument.Parse(KmlSerializer.SerializeToString(line)));
        Assert.AreEqual(PlaceItemType.Path, lineReopened.Children.Single().ItemType);
        Assert.AreEqual(2, lineReopened.Children.Single().Coordinates.Count);

        var polygon = new PlaceItem("Boundary", PlaceItemType.Polygon);
        polygon.Coordinates.AddRange(new[]
        {
            new CoordinatePoint(31,30),new CoordinatePoint(32,30),
            new CoordinatePoint(32,31),new CoordinatePoint(31,30)
        });
        polygon.InnerRings.Add(new System.Collections.Generic.List<CoordinatePoint> {
            new CoordinatePoint(31.1,30.1),new CoordinatePoint(31.2,30.1),
            new CoordinatePoint(31.2,30.2),new CoordinatePoint(31.1,30.1)
        });
        var polyReopened = KmlParser.ParseDocument(XDocument.Parse(KmlSerializer.SerializeToString(polygon)));
        Assert.AreEqual(PlaceItemType.Polygon, polyReopened.Children.Single().ItemType);
        Assert.AreEqual(1, polyReopened.Children.Single().InnerRings.Count);
    }

    [TestMethod]
    public void TimeStampRoundTripIsNotDropped()
    {
        const string source = "<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>" +
            "<Placemark><name>Timed</name><TimeStamp><when>2026-01-15T12:00:00Z</when></TimeStamp>" +
            "<Point><coordinates>31,30,1</coordinates></Point></Placemark></Document></kml>";
        var item = KmlParser.ParseDocument(XDocument.Parse(source));
        Assert.IsTrue(item.Children.Single().PreservedXmlFragments.Any(f => f.Contains("TimeStamp")));
        var saved = KmlSerializer.SerializeToString(item);
        Assert.IsTrue(saved.Contains("2026-01-15T12:00:00Z"));
        Assert.IsTrue(saved.Contains("31,30,1"));
    }

    [TestMethod]
    public void KmzAssetsRetainArchivePathsAcrossImportProjectCloneAndExport()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zearth-kmz-roundtrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "source.kmz");
            var exported = Path.Combine(folder, "exported.kmz");
            var tree = new PlaceItem("Source", PlaceItemType.Folder);
            tree.PreservedXmlFragments.Add(
                "<Style xmlns=\"http://www.opengis.net/kml/2.2\" id=\"original\"><IconStyle>" +
                "<Icon><href>files/markers/pin.png</href></Icon></IconStyle></Style>");
            tree.AddChild(new PlaceItem("Point", PlaceItemType.Placemark, 30, 31) { StyleUrl = "#original" });
            var icon = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 1, 2, 3 };
            tree.KmzResourcesBase64["files/markers/pin.png"] = Convert.ToBase64String(icon);
            KmzService.CreatePackage(source, tree);

            var imported = KmzService.ExtractAndParse(source, Path.Combine(folder, "unpacked"));
            Assert.AreEqual(Convert.ToBase64String(icon), imported.KmzResourcesBase64["files/markers/pin.png"]);
            KmzService.CreatePackage(exported, imported.Clone());
            using var archive = ZipFile.OpenRead(exported);
            var entry = archive.GetEntry("files/markers/pin.png");
            Assert.IsNotNull(entry);
            using var data = entry.Open();
            using var ms = new MemoryStream();
            data.CopyTo(ms);
            CollectionAssert.AreEqual(icon, ms.ToArray());
            var doc = archive.GetEntry("doc.kml");
            Assert.IsNotNull(doc);
            using var reader = new StreamReader(doc.Open(), Encoding.UTF8);
            var text = reader.ReadToEnd();
            Assert.IsTrue(text.Contains("files/markers/pin.png"));
            Assert.IsTrue(text.Contains("#original"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void ImportedKmlColorsAndStyleMapNormalResolveToCorrectCssWithoutChangingXml()
    {
        const string source = "<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>" +
            "<Style id=\"a\"><IconStyle><color>ff0000ff</color>" +
            "<Icon><href>files/marker.png</href></Icon></IconStyle></Style>" +
            "<StyleMap id=\"m\"><Pair><key>normal</key><styleUrl>#a</styleUrl></Pair>" +
            "<Pair><key>highlight</key><styleUrl>#other</styleUrl></Pair></StyleMap>" +
            "<Placemark><name>Red</name><styleUrl>#m</styleUrl>" +
            "<Point><coordinates>31,30,0</coordinates></Point></Placemark></Document></kml>";
        var original = XDocument.Parse(source);
        var parsed = KmlParser.ParseDocument(original);
        var point = parsed.Children.Single();
        Assert.AreEqual("#FF0000", point.ColorHex);
        Assert.AreEqual("files/marker.png", point.ExtendedData["KmlIconHref"]);
        Assert.AreEqual("#m", point.StyleUrl);
        var restoredXml = KmlSerializer.SerializeToString(parsed);
        Assert.IsTrue(restoredXml.Contains("ff0000ff"));
        Assert.IsTrue(restoredXml.Contains("<StyleMap"));
        Assert.IsTrue(restoredXml.Contains("#other"));
    }

    [TestMethod]
    public void InvalidPreservedStyleFailsExportBeforeOverwritingAnExistingKml()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zearth-style-safe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "previous.kml");
            File.WriteAllText(path, "original contents");
            var item = new PlaceItem("Broken XML", PlaceItemType.Folder);
            item.PreservedXmlFragments.Add("<Style>");
            Assert.ThrowsException<InvalidDataException>(() => KmlSerializer.SerializeToFile(item, path));
            Assert.AreEqual("original contents", File.ReadAllText(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void KmlAbgrColorsDecodeOpacityAndRedBlueOrder()
    {
        Assert.IsTrue(KmlStyleResolver.TryParseKmlColor("7f00ff00", out var css, out var alpha));
        Assert.AreEqual("#00FF00", css);
        Assert.AreEqual(127.0 / 255, alpha, 0.0001);
        Assert.IsFalse(KmlStyleResolver.TryParseKmlColor("not-hex", out _, out _));
    }

    [TestMethod]
    public void UnsupportedMultiGeometryMustRoundTripWithoutDroppingSecondaryShapes()
    {
        const string original = "<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>" +
            "<Placemark><name>Multiple geometries</name><MultiGeometry>" +
            "<Point><coordinates>31,30,0</coordinates></Point>" +
            "<LineString><coordinates>31,30,0 32,31,0</coordinates></LineString>" +
            "</MultiGeometry></Placemark></Document></kml>";
        var parsed = KmlParser.ParseDocument(XDocument.Parse(original));
        Assert.IsTrue(parsed.Children.Single().PreservedXmlFragments.Any(x => x.Contains("MultiGeometry")));
        var emitted = XDocument.Parse(KmlSerializer.SerializeToString(parsed));
        var multi = emitted.Descendants().Single(x => x.Name.LocalName == "MultiGeometry");
        Assert.AreEqual(1, multi.Descendants().Count(x => x.Name.LocalName == "Point"));
        Assert.AreEqual(1, multi.Descendants().Count(x => x.Name.LocalName == "LineString"));
        Assert.AreEqual(1, emitted.Descendants().Count(x => x.Name.LocalName == "Point"),
            "Must not duplicate the simplified primary Point alongside MultiGeometry");
    }

    [TestMethod]
    public void ExportPreservesSurveyPrecisionAndSubmeterElevation()
    {
        const double latitude = 29.9812345678912;
        const double longitude = 31.1234567891234;
        const double altitude = 123.987654321;
        var original = new PlaceItem("Survey point", PlaceItemType.Placemark, latitude, longitude, altitude);
        var kml = KmlSerializer.SerializeToString(original);
        var reopened = KmlParser.ParseDocument(XDocument.Parse(kml)).Children.Single();
        Assert.AreEqual(longitude, reopened.Longitude!.Value, 0.00000000000001);
        Assert.AreEqual(latitude, reopened.Latitude!.Value, 0.00000000000001);
        Assert.AreEqual(altitude, reopened.Altitude!.Value, 0.00000000001);
        Assert.IsFalse(kml.Contains("31.123457,"), "Coordinates must not be rounded to 6 decimals");
    }

    [TestMethod]
    public void ExportNestedSelectedPointCarriesAncestorStyleMapAndIconBytes()
    {
        const string kml = "<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>" +
            "<Style id=\"blue\"><IconStyle><color>ffff0000</color>" +
            "<Icon><href>files/icon.png</href></Icon></IconStyle></Style>" +
            "<StyleMap id=\"selected\"><Pair><key>normal</key><styleUrl>#blue</styleUrl></Pair></StyleMap>" +
            "<Folder><name>Egypt</name><Placemark><name>Cairo</name>" +
            "<styleUrl>#selected</styleUrl><Point><coordinates>31,30</coordinates></Point>" +
            "</Placemark></Folder></Document></kml>";
        var root = KmlParser.ParseDocument(XDocument.Parse(kml));
        root.KmzResourcesBase64["files/icon.png"] = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var selected = root.Children.Single().Children.Single();
        var packageTree = KmlSelectionExport.Create(selected, new[] { root });
        Assert.AreEqual(1, packageTree.Children.Count);
        Assert.AreEqual("Cairo", packageTree.Children.Single().Name);
        Assert.AreEqual(1, packageTree.KmzResourcesBase64.Count);
        var output = KmlSerializer.SerializeToString(packageTree);
        var parsedXml = XDocument.Parse(output);
        Assert.AreEqual(1, parsedXml.Descendants().Count(x => x.Name.LocalName == "Point"));
        Assert.AreEqual(1, parsedXml.Descendants().Count(x => x.Name.LocalName == "StyleMap"));
        Assert.AreEqual(1, parsedXml.Descendants().Count(x => x.Name.LocalName == "Style"));
        Assert.AreEqual("#selected", parsedXml.Descendants().Single(x => x.Name.LocalName == "styleUrl" &&
            x.Parent!.Name.LocalName == "Placemark").Value);
        Assert.AreEqual("#0000FF", selected.ColorHex, "KML AABBGGRR must resolve blue correctly");
    }

    [TestMethod]
    public void ZearthAuthoritativeSqliteSaveOpenPreservesImportedAssetsAndKmlTimes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zearth-sqlite-kmz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var project = ProjectDocument.CreateDefault();
            var imported = new PlaceItem("Imported KMZ", PlaceItemType.Folder);
            var timePoint = new PlaceItem("Historic view", PlaceItemType.Placemark, 30.0444, 31.2357, 42)
            {
                TimeSpanBegin = "2020-01-01",
                TimeSpanEnd = "2021-01-01",
                IsProtected = false
            };
            imported.AddChild(timePoint);
            imported.KmzResourcesBase64["files/logo.png"] =
                Convert.ToBase64String(new byte[] { 7, 19, 25, 38 });
            project.MyPlaces.Add(imported);
            var database = Path.Combine(dir, "project.db");
            SqliteProjectStorage.SaveToDatabase(project, database);
            var loaded = SqliteProjectStorage.LoadFromDatabase(database);
            var reopened = loaded.MyPlaces.Single(x => x.Name == "Imported KMZ");
            CollectionAssert.AreEqual(new byte[] { 7, 19, 25, 38 },
                Convert.FromBase64String(reopened.KmzResourcesBase64["files/logo.png"]));
            Assert.AreEqual("2020-01-01", reopened.Children.Single().TimeSpanBegin);
            Assert.AreEqual("2021-01-01", reopened.Children.Single().TimeSpanEnd);
            Assert.IsTrue(loaded.MyPlaces.Any(x => x.Name == "Temporary Places" && x.IsProtected),
                "Protected system folders stay protected after Save As / Open");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void InvalidKmzAssetCollisionNeverOverwritesPriorArchive()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zearth-kmz-safe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var output = Path.Combine(folder, "safe.kmz");
            var original = new PlaceItem("Original", PlaceItemType.Folder);
            KmzService.CreatePackage(output, original);
            var before = File.ReadAllBytes(output);
            var conflict = new PlaceItem("Conflict", PlaceItemType.Folder);
            conflict.KmzResourcesBase64["files/icon.png"] = Convert.ToBase64String(new byte[] { 1 });
            var second = new PlaceItem("Second", PlaceItemType.Folder);
            second.KmzResourcesBase64["files/icon.png"] = Convert.ToBase64String(new byte[] { 2 });
            conflict.AddChild(second);
            Assert.ThrowsException<InvalidDataException>(() => KmzService.CreatePackage(output, conflict));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(output));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void KmzExtractionRejectsCaseVariantSiblingTraversalOnLinux()
    {
        // Linux file paths are case-sensitive. An OrdinalIgnoreCase prefix check
        // previously accepted ../archive/ when the extraction root was Archive/.
        if (!OperatingSystem.IsLinux()) return;

        var folder = Path.Combine(Path.GetTempPath(), "zearth-kmz-case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var archivePath = Path.Combine(folder, "malicious.kmz");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var doc = archive.CreateEntry("doc.kml");
                using (var writer = new StreamWriter(doc.Open(), Encoding.UTF8))
                    writer.Write("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document/></kml>");
                var malicious = archive.CreateEntry("../archive/escape.txt");
                using (var writer = new StreamWriter(malicious.Open(), Encoding.UTF8))
                    writer.Write("this must not escape");
            }

            var destination = Path.Combine(folder, "Archive");
            var escapedFile = Path.Combine(folder, "archive", "escape.txt");
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => KmzService.ExtractAndParse(archivePath, destination));
            Assert.IsFalse(File.Exists(escapedFile), "KMZ entry must never escape destination.");
        }
        finally { Directory.Delete(folder, true); }
    }

}
