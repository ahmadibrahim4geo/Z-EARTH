using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Kml;

namespace ZEarth.Tests;

[TestClass]
public sealed class KmlLegacyGoogleEarth21Tests
{
    [TestMethod]
    public void Earth21DocumentRecognizedWithStylesNestedPlacesAndCoordinates()
    {
        // Synthetic fixture, not a copy of Google's bundled userpalette.kml.
        const string source = """
            <?xml version="1.0" encoding="utf-8"?>
            <kml xmlns="http://earth.google.com/kml/2.1">
              <Document>
                <name>Legacy Google Earth</name>
                <Style id="style-normal">
                  <IconStyle><scale>1.3</scale><Icon><href>images/pin.png</href></Icon></IconStyle>
                </Style>
                <StyleMap id="style-map">
                  <Pair><key>normal</key><styleUrl>#style-normal</styleUrl></Pair>
                </StyleMap>
                <Folder>
                  <name>Historic places</name>
                  <Placemark>
                    <name>القاهرة</name><visibility>0</visibility>
                    <styleUrl>#style-map</styleUrl>
                    <Point><coordinates>31.2357,30.0444,22</coordinates></Point>
                  </Placemark>
                </Folder>
              </Document>
            </kml>
            """;
        var root = KmlParser.ParseDocument(XDocument.Parse(source), "Untitled");
        Assert.AreEqual("Legacy Google Earth", root.Name);
        Assert.AreEqual(1, root.Children.Count);
        Assert.AreEqual("Historic places", root.Children[0].Name);
        var point = root.Children[0].Children.Single();
        Assert.AreEqual(PlaceItemType.Placemark, point.ItemType);
        Assert.AreEqual("القاهرة", point.Name);
        Assert.IsFalse(point.IsVisible);
        Assert.AreEqual(31.2357, point.Longitude.GetValueOrDefault(), 1e-7);
        Assert.AreEqual(30.0444, point.Latitude.GetValueOrDefault(), 1e-7);
        Assert.AreEqual(22.0, point.Altitude.GetValueOrDefault(), 1e-7);
        Assert.AreEqual("#style-map", point.StyleUrl);
        Assert.IsTrue(root.PreservedXmlFragments.Any(x => x.Contains("StyleMap")));
        Assert.IsTrue(root.PreservedXmlFragments.Any(x => x.Contains("IconStyle")));
    }

    [TestMethod]
    public void Standard22DocumentStillRecognized()
    {
        const string source = """
            <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
              <name>Modern document</name>
              <Placemark><name>Luxor</name><Point><coordinates>32.65,25.7,0</coordinates></Point></Placemark>
            </Document></kml>
            """;
        var root = KmlParser.ParseDocument(XDocument.Parse(source));
        Assert.AreEqual("Modern document", root.Name);
        Assert.AreEqual("Luxor", root.Children.Single().Name);
    }

    [TestMethod]
    public void UnnamespacedDocumentStillRecognized()
    {
        const string source = """
            <kml><Document><name>Plain</name><Placemark><name>A</name><Point>
              <coordinates>31,30,0</coordinates></Point></Placemark></Document></kml>
            """;
        var root = KmlParser.ParseDocument(XDocument.Parse(source));
        Assert.AreEqual("Plain", root.Name);
        Assert.AreEqual(1, root.Children.Count);
    }
}
