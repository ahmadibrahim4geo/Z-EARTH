using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Gps;

namespace ZEarth.Tests;

[TestClass]
public sealed class GpxImportTests
{
    private static PlaceItem Parse(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return GpxService.Import(stream, "test");
    }

    [TestMethod]
    public void Gpx11ImportsWaypointsRouteAndSegmentsWithUtcTimes()
    {
        const string xml = """
            <?xml version="1.0"?>
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
              <metadata><name>رحلة الأقصر</name></metadata>
              <wpt lat="25.7" lon="32.65"><ele>78</ele><name>Station</name><time>2025-01-01T11:00:00+02:00</time></wpt>
              <rte><name>Survey</name><rtept lat="25.7" lon="32.65"><ele>76</ele></rtept>
                <rtept lat="25.71" lon="32.66"><time>2025-01-01T09:30:00Z</time></rtept></rte>
              <trk><name>Track A</name><trkseg>
                <trkpt lat="25.72" lon="32.67"><ele>90</ele><time>2025-01-02T12:00:00Z</time></trkpt>
                <trkpt lat="25.73" lon="32.68"><ele>93</ele><time>2025-01-02T12:01:00Z</time></trkpt>
              </trkseg><trkseg>
                <trkpt lat="25.74" lon="32.69"><ele>-3</ele></trkpt>
                <trkpt lat="25.75" lon="32.70"><ele>0</ele></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        var tree = Parse(xml);
        Assert.AreEqual("رحلة الأقصر", tree.Name);
        Assert.AreEqual(3, tree.Children.Count);
        Assert.AreEqual("Station", tree.Children[0].Name);
        Assert.AreEqual("2025-01-01T09:00:00.0000000+00:00", tree.Children[0].ExtendedData["GpsTimeUtc"]);
        var route = tree.Children[1];
        Assert.AreEqual(PlaceItemType.Path, route.ItemType);
        Assert.AreEqual(2, route.Coordinates.Count);
        Assert.AreEqual("1", route.ExtendedData["GpsMissingElevationCount"]);
        var trk = tree.Children[2];
        Assert.AreEqual(2, trk.Children.Count);
        Assert.AreEqual(2, trk.Children[0].Coordinates.Count);
        Assert.AreEqual(-3, trk.Children[1].Coordinates[0].Altitude);
        Assert.AreEqual("0", trk.Children[1].ExtendedData["GpsMissingElevationCount"]);
        Assert.AreEqual("7", tree.ExtendedData["GpsPointCount"]);
        Assert.AreEqual("2", tree.ExtendedData["GpsSegmentCount"]);
    }

    [TestMethod]
    public void Gpx10AndMissingElevationRemainExplicitlyLabeled()
    {
        var result = Parse("<gpx xmlns='http://www.topografix.com/GPX/1/0'><wpt lat='30' lon='31'><name>Cairo</name></wpt></gpx>");
        var pt = result.Children.Single();
        Assert.AreEqual("false", pt.ExtendedData["GpsElevationPresent"]);
        Assert.AreEqual(0, pt.Altitude.GetValueOrDefault());
    }

    [TestMethod]
    public void InvalidCoordinatesAndDtdAreRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Parse("<gpx><wpt lat='99' lon='31'/></gpx>"));
        Assert.ThrowsExactly<System.Xml.XmlException>(() =>
            Parse("<!DOCTYPE gpx [<!ENTITY x SYSTEM 'file:///C:/secret'>]><gpx><wpt lat='30' lon='31'/></gpx>"));
    }

    [TestMethod]
    public void NoInventedGpsTrackForEmptyFile()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Parse("<gpx xmlns='http://www.topografix.com/GPX/1/1' />"));
    }
}
