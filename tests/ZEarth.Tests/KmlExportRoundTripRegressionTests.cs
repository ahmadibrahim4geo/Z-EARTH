using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Kml;

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
        Assert.IsTrue(kml.Contains("31.235700,30.044400,52.5"));
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
        Assert.IsTrue(saved.Contains("31.000000,30.000000,1.0"));
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
            Assert.ThrowsExactly<InvalidDataException>(() => KmzService.CreatePackage(output, conflict));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(output));
        }
        finally { Directory.Delete(folder, true); }
    }
}
