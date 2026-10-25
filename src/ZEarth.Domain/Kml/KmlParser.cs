using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace ZEarth.Domain.Kml
{
    public static class KmlParser
    {
        private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";
        private static readonly XNamespace GxNs = "http://www.google.com/kml/ext/2.2";

        /// <summary>
        /// Safely loads and parses a KML document from file, enforcing anti-XXE and anti-DTD rules.
        /// </summary>
        public static PlaceItem ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"KML file not found: {filePath}");
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024
            };

            using var stream = File.OpenRead(filePath);
            using var reader = XmlReader.Create(stream, settings);
            var xDoc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);

            return ParseDocument(xDoc, Path.GetFileNameWithoutExtension(filePath));
        }

        /// <summary>
        /// Safely parses a KML document from an XDocument instance.
        /// </summary>
        public static PlaceItem ParseDocument(XDocument doc, string defaultRootName = "KML Root")
        {
            if (doc.Root == null)
            {
                throw new InvalidOperationException("KML document contains no root element.");
            }

            var rootFolder = new PlaceItem(defaultRootName, PlaceItemType.Folder);

            // Find Document or direct Folder/Placemark elements
            var docElement = doc.Root.Element(KmlNs + "Document") ?? doc.Root.Element("Document");
            if (docElement != null)
            {
                var docName = docElement.Element(KmlNs + "name")?.Value ?? docElement.Element("name")?.Value;
                if (!string.IsNullOrWhiteSpace(docName))
                {
                    rootFolder.Name = docName;
                }

                ParseContainerElements(docElement, rootFolder);
            }
            else
            {
                ParseContainerElements(doc.Root, rootFolder);
            }

            return rootFolder;
        }

        private static void ParseContainerElements(XElement container, PlaceItem parent)
        {
            foreach (var elem in container.Elements())
            {
                string localName = elem.Name.LocalName;

                if (localName.Equals("Folder", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = ParseFolder(elem);
                    parent.AddChild(folder);
                }
                else if (localName.Equals("Placemark", StringComparison.OrdinalIgnoreCase))
                {
                    var placemark = ParsePlacemark(elem);
                    parent.AddChild(placemark);
                }
                else if (localName.Equals("Style", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("StyleMap", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("NetworkLink", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("GroundOverlay", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("ScreenOverlay", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("PhotoOverlay", StringComparison.OrdinalIgnoreCase) ||
                         localName.Equals("Tour", StringComparison.OrdinalIgnoreCase) ||
                         elem.Name.Namespace == GxNs)
                {
                    // Preserve unhandled top-level KML features and Google extensions
                    parent.PreservedXmlFragments.Add(elem.ToString(SaveOptions.DisableFormatting));
                }
            }
        }

        private static PlaceItem ParseFolder(XElement folderElem)
        {
            string name = GetElementValue(folderElem, "name") ?? "Untitled Folder";
            var folder = new PlaceItem(name, PlaceItemType.Folder)
            {
                Description = GetElementValue(folderElem, "description") ?? string.Empty,
                IsVisible = ParseVisibility(GetElementValue(folderElem, "visibility"))
            };

            // Preserve any unparsed tags inside the folder
            foreach (var elem in folderElem.Elements())
            {
                string ln = elem.Name.LocalName;
                if (!ln.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                    !ln.Equals("description", StringComparison.OrdinalIgnoreCase) &&
                    !ln.Equals("visibility", StringComparison.OrdinalIgnoreCase) &&
                    !ln.Equals("Folder", StringComparison.OrdinalIgnoreCase) &&
                    !ln.Equals("Placemark", StringComparison.OrdinalIgnoreCase))
                {
                    folder.PreservedXmlFragments.Add(elem.ToString(SaveOptions.DisableFormatting));
                }
            }

            ParseContainerElements(folderElem, folder);
            return folder;
        }

        private static PlaceItem ParsePlacemark(XElement placemarkElem)
        {
            string name = GetElementValue(placemarkElem, "name") ?? "Untitled Placemark";
            var placemark = new PlaceItem(name, PlaceItemType.Placemark)
            {
                Description = GetElementValue(placemarkElem, "description") ?? string.Empty,
                IsVisible = ParseVisibility(GetElementValue(placemarkElem, "visibility")),
                StyleUrl = GetElementValue(placemarkElem, "styleUrl")
            };

            // Parse TimeSpan / TimeStamp
            var timeSpan = FindDescendant(placemarkElem, "TimeSpan");
            if (timeSpan != null)
            {
                placemark.TimeSpanBegin = GetElementValue(timeSpan, "begin");
                placemark.TimeSpanEnd = GetElementValue(timeSpan, "end");
            }

            // Parse ExtendedData
            var extData = FindDescendant(placemarkElem, "ExtendedData");
            if (extData != null)
            {
                foreach (var dataElem in extData.Elements().Where(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase)))
                {
                    string? key = dataElem.Attribute("name")?.Value;
                    string? val = GetElementValue(dataElem, "value");
                    if (!string.IsNullOrEmpty(key) && val != null)
                    {
                        placemark.ExtendedData[key] = val;
                    }
                }
            }

            // Parse Geometry
            ParseGeometry(placemarkElem, placemark);

            // Preserve unparsed elements (LookAt, Camera, Style, gx elements, custom XML)
            var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "description", "visibility", "styleUrl", "TimeSpan", "TimeStamp",
                "ExtendedData", "Point", "LineString", "Polygon", "MultiGeometry"
            };

            foreach (var elem in placemarkElem.Elements())
            {
                if (!knownNames.Contains(elem.Name.LocalName))
                {
                    placemark.PreservedXmlFragments.Add(elem.ToString(SaveOptions.DisableFormatting));
                }
            }

            return placemark;
        }

        private static void ParseGeometry(XElement placemarkElem, PlaceItem placemark)
        {
            // 1. Point
            var pointElem = FindDescendant(placemarkElem, "Point");
            if (pointElem != null)
            {
                placemark.ItemType = PlaceItemType.Placemark;
                string? coordsText = GetElementValue(pointElem, "coordinates");
                var coords = ParseCoordinates(coordsText);
                if (coords.Count > 0)
                {
                    placemark.Longitude = coords[0].Longitude;
                    placemark.Latitude = coords[0].Latitude;
                    placemark.Altitude = coords[0].Altitude;
                    placemark.Coordinates = coords;
                }
                return;
            }

            // 2. LineString
            var lineElem = FindDescendant(placemarkElem, "LineString");
            if (lineElem != null)
            {
                placemark.ItemType = PlaceItemType.Path;
                string? coordsText = GetElementValue(lineElem, "coordinates");
                placemark.Coordinates = ParseCoordinates(coordsText);
                if (placemark.Coordinates.Count > 0)
                {
                    placemark.Longitude = placemark.Coordinates[0].Longitude;
                    placemark.Latitude = placemark.Coordinates[0].Latitude;
                    placemark.Altitude = placemark.Coordinates[0].Altitude;
                }
                return;
            }

            // 3. Polygon
            var polyElem = FindDescendant(placemarkElem, "Polygon");
            if (polyElem != null)
            {
                placemark.ItemType = PlaceItemType.Polygon;
                var outer = FindDescendant(polyElem, "outerBoundaryIs");
                if (outer != null)
                {
                    string? coordsText = FindDescendant(outer, "coordinates")?.Value ?? GetElementValue(outer, "coordinates");
                    placemark.Coordinates = ParseCoordinates(coordsText);
                }

                // Inner boundaries (holes)
                var inners = polyElem.Elements().Where(e => e.Name.LocalName.Equals("innerBoundaryIs", StringComparison.OrdinalIgnoreCase));
                foreach (var inner in inners)
                {
                    string? innerText = FindDescendant(inner, "coordinates")?.Value ?? GetElementValue(inner, "coordinates");
                    var holeCoords = ParseCoordinates(innerText);
                    if (holeCoords.Count > 0)
                    {
                        placemark.InnerRings.Add(holeCoords);
                    }
                }

                if (placemark.Coordinates.Count > 0)
                {
                    placemark.Longitude = placemark.Coordinates[0].Longitude;
                    placemark.Latitude = placemark.Coordinates[0].Latitude;
                    placemark.Altitude = placemark.Coordinates[0].Altitude;
                }
            }
        }

        public static List<CoordinatePoint> ParseCoordinates(string? coordinatesString)
        {
            var result = new List<CoordinatePoint>();
            if (string.IsNullOrWhiteSpace(coordinatesString)) return result;

            var tuples = coordinatesString.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var tuple in tuples)
            {
                var parts = tuple.Split(',');
                if (parts.Length >= 2)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                    {
                        double alt = 0.0;
                        if (parts.Length >= 3)
                        {
                            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out alt);
                        }
                        result.Add(new CoordinatePoint(lon, lat, alt));
                    }
                }
            }
            return result;
        }

        private static string? GetElementValue(XElement parent, string localName)
        {
            var elem = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
            return elem?.Value;
        }

        private static XElement? FindDescendant(XElement parent, string localName)
        {
            return parent.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ParseVisibility(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return true;
            return val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
