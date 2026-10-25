using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ZEarth.Domain.Kml
{
    public static class KmlSerializer
    {
        private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";
        private static readonly XNamespace GxNs = "http://www.google.com/kml/ext/2.2";

        public static string SerializeToString(PlaceItem root)
        {
            var doc = SerializeToDocument(root);
            var sb = new StringBuilder();
            using (var writer = new Utf8StringWriter(sb))
            {
                doc.Save(writer);
            }
            return sb.ToString();
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter(StringBuilder builder) : base(builder, CultureInfo.InvariantCulture) { }
            public override Encoding Encoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        public static void SerializeToFile(PlaceItem root, string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var doc = SerializeToDocument(root);
            doc.Save(filePath);
        }

        public static XDocument SerializeToDocument(PlaceItem root)
        {
            var kmlRoot = new XElement(KmlNs + "kml",
                new XAttribute(XNamespace.Xmlns + "gx", GxNs.NamespaceName));

            var docElem = new XElement(KmlNs + "Document",
                new XElement(KmlNs + "name", root.Name));

            // Write any top-level preserved elements from root
            foreach (var frag in root.PreservedXmlFragments)
            {
                try
                {
                    var xFrag = XElement.Parse(frag);
                    docElem.Add(xFrag);
                }
                catch
                {
                    // If parsing fragment fails, skip to avoid invalid XML
                }
            }

            // Write child folders and placemarks
            foreach (var child in root.Children)
            {
                if (child.ItemType == PlaceItemType.Folder)
                {
                    docElem.Add(SerializeFolder(child));
                }
                else
                {
                    docElem.Add(SerializePlacemark(child));
                }
            }

            kmlRoot.Add(docElem);
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), kmlRoot);
        }

        private static XElement SerializeFolder(PlaceItem folder)
        {
            var folderElem = new XElement(KmlNs + "Folder",
                new XElement(KmlNs + "name", folder.Name),
                new XElement(KmlNs + "visibility", folder.IsVisible ? "1" : "0"));

            if (!string.IsNullOrEmpty(folder.Description))
            {
                folderElem.Add(new XElement(KmlNs + "description", folder.Description));
            }

            foreach (var frag in folder.PreservedXmlFragments)
            {
                try
                {
                    folderElem.Add(XElement.Parse(frag));
                }
                catch { }
            }

            foreach (var child in folder.Children)
            {
                if (child.ItemType == PlaceItemType.Folder)
                {
                    folderElem.Add(SerializeFolder(child));
                }
                else
                {
                    folderElem.Add(SerializePlacemark(child));
                }
            }

            return folderElem;
        }

        private static XElement SerializePlacemark(PlaceItem placemark)
        {
            var pElem = new XElement(KmlNs + "Placemark",
                new XElement(KmlNs + "name", placemark.Name),
                new XElement(KmlNs + "visibility", placemark.IsVisible ? "1" : "0"));

            if (!string.IsNullOrEmpty(placemark.Description))
            {
                pElem.Add(new XElement(KmlNs + "description", placemark.Description));
            }

            if (!string.IsNullOrEmpty(placemark.StyleUrl))
            {
                pElem.Add(new XElement(KmlNs + "styleUrl", placemark.StyleUrl));
            }

            // TimeSpan
            if (!string.IsNullOrEmpty(placemark.TimeSpanBegin) || !string.IsNullOrEmpty(placemark.TimeSpanEnd))
            {
                var tsElem = new XElement(KmlNs + "TimeSpan");
                if (!string.IsNullOrEmpty(placemark.TimeSpanBegin)) tsElem.Add(new XElement(KmlNs + "begin", placemark.TimeSpanBegin));
                if (!string.IsNullOrEmpty(placemark.TimeSpanEnd)) tsElem.Add(new XElement(KmlNs + "end", placemark.TimeSpanEnd));
                pElem.Add(tsElem);
            }

            // ExtendedData
            if (placemark.ExtendedData.Count > 0)
            {
                var extElem = new XElement(KmlNs + "ExtendedData");
                foreach (var kvp in placemark.ExtendedData)
                {
                    extElem.Add(new XElement(KmlNs + "Data",
                        new XAttribute("name", kvp.Key),
                        new XElement(KmlNs + "value", kvp.Value)));
                }
                pElem.Add(extElem);
            }

            // Preserved unparsed XML elements
            foreach (var frag in placemark.PreservedXmlFragments)
            {
                try
                {
                    pElem.Add(XElement.Parse(frag));
                }
                catch { }
            }

            // Geometry Serialization
            SerializeGeometry(placemark, pElem);

            return pElem;
        }

        private static void SerializeGeometry(PlaceItem placemark, XElement pElem)
        {
            if (placemark.ItemType == PlaceItemType.Placemark && placemark.Longitude.HasValue && placemark.Latitude.HasValue)
            {
                double lon = placemark.Longitude.Value;
                double lat = placemark.Latitude.Value;
                double alt = placemark.Altitude ?? 0.0;
                string coordStr = string.Format(CultureInfo.InvariantCulture, "{0:F6},{1:F6},{2:F1}", lon, lat, alt);

                pElem.Add(new XElement(KmlNs + "Point",
                    new XElement(KmlNs + "coordinates", coordStr)));
            }
            else if (placemark.ItemType == PlaceItemType.Path && placemark.Coordinates.Count > 0)
            {
                string coordsStr = FormatCoordinatesList(placemark.Coordinates);
                pElem.Add(new XElement(KmlNs + "LineString",
                    new XElement(KmlNs + "tessellate", "1"),
                    new XElement(KmlNs + "coordinates", coordsStr)));
            }
            else if (placemark.ItemType == PlaceItemType.Polygon && placemark.Coordinates.Count > 0)
            {
                var polyElem = new XElement(KmlNs + "Polygon");

                // Outer boundary
                var outer = new XElement(KmlNs + "outerBoundaryIs",
                    new XElement(KmlNs + "LinearRing",
                        new XElement(KmlNs + "coordinates", FormatCoordinatesList(placemark.Coordinates))));
                polyElem.Add(outer);

                // Inner boundaries (holes)
                foreach (var hole in placemark.InnerRings)
                {
                    var inner = new XElement(KmlNs + "innerBoundaryIs",
                        new XElement(KmlNs + "LinearRing",
                            new XElement(KmlNs + "coordinates", FormatCoordinatesList(hole))));
                    polyElem.Add(inner);
                }

                pElem.Add(polyElem);
            }
        }

        private static string FormatCoordinatesList(IEnumerable<CoordinatePoint> points)
        {
            return string.Join(" ", points.Select(p =>
                string.Format(CultureInfo.InvariantCulture, "{0:F6},{1:F6},{2:F1}", p.Longitude, p.Latitude, p.Altitude)));
        }
    }
}
