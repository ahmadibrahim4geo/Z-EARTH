using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Gps;
using ZEarth.Domain.Kml;

namespace ZEarth.Tests;

[TestClass]
public sealed class GxTrackInteroperabilityTests
{
    private static readonly XNamespace Gx = "http://www.google.com/kml/ext/2.2";

    [TestMethod]
    public void GpxTimedTrack_ExportsAsStandardGxTrack_WithUtcTimes()
    {
        const string source = """
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="synthetic">
              <trk><name>Timed Sinai</name><trkseg>
                <trkpt lat="29" lon="33"><ele>200</ele><time>2020-06-01T10:00:00Z</time></trkpt>
                <trkpt lat="29.1" lon="33.1"><ele>250</ele><time>2020-06-01T10:05:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var imported = GpxService.Import(stream);
        var xml = KmlSerializer.SerializeToDocument(imported);
        var tracks = xml.Descendants(Gx + "Track").ToList();
        Assert.AreEqual(1, tracks.Count);
        Assert.AreEqual(2, tracks[0].Elements().Count(e => e.Name.LocalName == "when"));
        Assert.AreEqual(2, tracks[0].Elements(Gx + "coord").Count());
        Assert.AreEqual(0, xml.Descendants().Count(e => e.Name.LocalName == "LineString"));
        var readBack = KmlParser.ParseDocument(XDocument.Parse(xml.ToString()));
        var segment = readBack.Children.Single().Children.Single();
        Assert.AreEqual(PlaceItemType.Path, segment.ItemType);
        Assert.AreEqual(2, segment.Coordinates.Count);
        Assert.AreEqual(29.1, segment.Coordinates[1].Latitude, 1e-8);
        Assert.AreEqual("KML gx:Track", segment.ExtendedData["GpsSourceKind"]);
        var times = JsonSerializer.Deserialize<string[]>(segment.ExtendedData["GpsPointTimesUtcJson"]);
        Assert.IsNotNull(times);
        Assert.AreEqual(2, times.Length);
    }

    [TestMethod]
    public void ImportedGxTrack_PreservesAnglesAndOneGeometryOnRoundtrip()
    {
        const string source = """
            <kml xmlns="http://www.opengis.net/kml/2.2"
                 xmlns:gx="http://www.google.com/kml/ext/2.2">
              <Document><name>Flight log</name><Placemark><name>Track</name>
                <gx:Track>
                  <when>2020-01-01T00:00:00Z</when>
                  <when>2020-01-01T00:00:01Z</when>
                  <gx:coord>31 30 120</gx:coord>
                  <gx:coord>31.1 30.1 123</gx:coord>
                  <gx:angles>1 2 3</gx:angles>
                  <gx:angles>4 5 6</gx:angles>
                </gx:Track>
              </Placemark></Document>
            </kml>
            """;
        var imported = KmlParser.ParseDocument(XDocument.Parse(source));
        var place = imported.Children.Single();
        Assert.AreEqual(PlaceItemType.Path, place.ItemType);
        Assert.AreEqual(2, place.Coordinates.Count);
        var exported = XDocument.Parse(KmlSerializer.SerializeToString(imported));
        Assert.AreEqual(1, exported.Descendants(Gx + "Track").Count());
        Assert.AreEqual(2, exported.Descendants(Gx + "angles").Count());
        Assert.AreEqual(0, exported.Descendants().Count(x => x.Name.LocalName == "LineString"));
    }

    [TestMethod]
    public void UntimedGpxRoute_RemainsLineString_NoFalseTimes()
    {
        const string source = """
            <gpx><rte><name>Untimed</name>
            <rtept lat="30" lon="31"/><rtept lat="30.1" lon="31.1"/>
            </rte></gpx>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var root = GpxService.Import(stream);
        var export = KmlSerializer.SerializeToDocument(root);
        Assert.AreEqual(0, export.Descendants(Gx + "Track").Count());
        Assert.AreEqual(1, export.Descendants().Count(x => x.Name.LocalName == "LineString"));
    }
}
