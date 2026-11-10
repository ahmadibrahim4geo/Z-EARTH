using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

            // KML 2.2 is the standard, but installed Google Earth Pro also ships
            // userpalette.kml with the legacy earth.google.com/kml/2.1 namespace.
            // Use the immediate element's local name so versioned namespaces are
            // accepted without discarding Document/Style/StyleMap children.
            var docElement = doc.Root.Name.LocalName.Equals("Document", StringComparison.OrdinalIgnoreCase)
                ? doc.Root
                : doc.Root.Elements().FirstOrDefault(e =>
                    e.Name.LocalName.Equals("Document", StringComparison.OrdinalIgnoreCase));
            if (docElement != null)
            {
                var docName = GetElementValue(docElement, "name");
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

            // Original Style/StyleMap XML remains preserved; apply only a
            // display projection of the common KML symbol rules.
            KmlStyleResolver.Apply(rootFolder);
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
            string rawName = GetElementValue(placemarkElem, "name") ?? string.Empty;
            string description = GetElementValue(placemarkElem, "description") ?? string.Empty;
            var placemark = new PlaceItem(string.IsNullOrWhiteSpace(rawName) ? "Untitled Placemark" : rawName, PlaceItemType.Placemark)
            {
                Description = description,
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

            // Parse ExtendedData: supports <Data name="...">, <SchemaData><SimpleData name="...">, and direct XML tags
            var extData = FindDescendant(placemarkElem, "ExtendedData");
            if (extData != null)
            {
                // 1. <Data name="Key"><value>Val</value></Data>
                foreach (var dataElem in extData.Elements().Where(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase)))
                {
                    string? key = dataElem.Attribute("name")?.Value;
                    string? val = GetElementValue(dataElem, "value");
                    if (!string.IsNullOrEmpty(key) && val != null)
                    {
                        placemark.ExtendedData[key] = val;
                    }
                }

                // 2. <SchemaData><SimpleData name="Key">Val</SimpleData></SchemaData> (Standard GIS / Database exports)
                foreach (var schemaData in extData.Elements().Where(e => e.Name.LocalName.Equals("SchemaData", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var simpleData in schemaData.Elements().Where(e => e.Name.LocalName.Equals("SimpleData", StringComparison.OrdinalIgnoreCase)))
                    {
                        string? key = simpleData.Attribute("name")?.Value;
                        string val = simpleData.Value;
                        if (!string.IsNullOrEmpty(key) && val != null)
                        {
                            placemark.ExtendedData[key] = val;
                        }
                    }
                }

                // 3. Direct elements inside ExtendedData: <FieldName>Value</FieldName>
                foreach (var directElem in extData.Elements())
                {
                    string ln = directElem.Name.LocalName;
                    if (!ln.Equals("Data", StringComparison.OrdinalIgnoreCase) && !ln.Equals("SchemaData", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!directElem.HasElements && !string.IsNullOrWhiteSpace(directElem.Value) && !placemark.ExtendedData.ContainsKey(ln))
                        {
                            placemark.ExtendedData[ln] = directElem.Value.Trim();
                        }
                    }
                }
            }

            // 4. If ExtendedData is empty, extract attribute rows from HTML tables in description
            if (placemark.ExtendedData.Count == 0 && !string.IsNullOrWhiteSpace(description) && description.Contains("<table", StringComparison.OrdinalIgnoreCase))
            {
                ExtractAttributesFromHtmlTable(description, placemark.ExtendedData);
            }

            // Smart name resolution:
            // Order: 1. name -> 2. type -> 3. facilityId -> 4. featureId -> 5. id
            if (string.IsNullOrWhiteSpace(rawName) || rawName.Equals("Untitled Placemark", StringComparison.OrdinalIgnoreCase) || rawName.Equals("Feature", StringComparison.OrdinalIgnoreCase))
            {
                var nameKey = placemark.ExtendedData.Keys.FirstOrDefault(k =>
                    (k.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 || k.Contains("اسم"))
                    && !string.IsNullOrWhiteSpace(placemark.ExtendedData[k]));

                var typeKey = placemark.ExtendedData.Keys.FirstOrDefault(k =>
                    (k.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0 || k.Contains("نوع"))
                    && !string.IsNullOrWhiteSpace(placemark.ExtendedData[k]));

                var facilityKey = placemark.ExtendedData.Keys.FirstOrDefault(k =>
                    k.IndexOf("facility", StringComparison.OrdinalIgnoreCase) >= 0
                    && !string.IsNullOrWhiteSpace(placemark.ExtendedData[k]));

                var featureKey = placemark.ExtendedData.Keys.FirstOrDefault(k =>
                    k.IndexOf("feature", StringComparison.OrdinalIgnoreCase) >= 0
                    && !string.IsNullOrWhiteSpace(placemark.ExtendedData[k]));

                var idKey = placemark.ExtendedData.Keys.FirstOrDefault(k =>
                    (k.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                     k.IndexOf("objectid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     k.IndexOf("fid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     k.EndsWith("id", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrWhiteSpace(placemark.ExtendedData[k]));

                var bestKey = nameKey ?? typeKey ?? facilityKey ?? featureKey ?? idKey;
                if (bestKey != null && !string.IsNullOrWhiteSpace(placemark.ExtendedData[bestKey]))
                {
                    placemark.Name = placemark.ExtendedData[bestKey].Trim();
                }
            }

            // Parse Geometry
            ParseGeometry(placemarkElem, placemark);

            // Preserve unparsed elements (LookAt, Camera, Style, gx elements, custom XML)
            var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // TimeStamp is not modeled separately: preserve it as raw XML.
                "name", "description", "visibility", "styleUrl", "TimeSpan",
                // MultiGeometry is not fully representable by a single PlaceItem.
                // Preserve the whole original XML rather than silently dropping
                // secondary parts on export. The renderer may show only one geometry.
                "ExtendedData", "Point", "LineString", "Polygon"
            };

            // Keep the original gx:Track XML (including gx:angles/Model metadata)
            // as a preserved fragment: we render sampled coordinates but export
            // the exact source geometry instead of replacing it with a LineString.
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

            // 2. gx:Track: time-tagged KML geometry used by Google Earth.
            // The XML remains preserved unchanged if dates/coords are invalid.
            var gxTrack = placemarkElem.Descendants()
                .FirstOrDefault(e => e.Name == GxNs + "Track");
            if (gxTrack != null)
            {
                var whenElements = gxTrack.Elements()
                    .Where(e => e.Name.LocalName == "when").ToList();
                var coordElements = gxTrack.Elements(GxNs + "coord").ToList();
                if (whenElements.Count == coordElements.Count &&
                    whenElements.Count >= 2 && whenElements.Count <= 200000)
                {
                    var fixes = new List<CoordinatePoint>(coordElements.Count);
                    var times = new List<string>(whenElements.Count);
                    DateTimeOffset? previous = null;
                    bool valid = true;
                    for (int i = 0; i < coordElements.Count; i++)
                    {
                        var values = coordElements[i].Value.Split(
                            new[] { ' ', '\t', '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length < 3 ||
                            !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                            !double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                            !double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var alt) ||
                            !double.IsFinite(lon) || !double.IsFinite(lat) || !double.IsFinite(alt) ||
                            lon < -180 || lon > 180 || lat < -90 || lat > 90 ||
                            !DateTimeOffset.TryParse(whenElements[i].Value, CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) ||
                            (previous.HasValue && time <= previous.Value))
                        {
                            valid = false;
                            break;
                        }
                        fixes.Add(new CoordinatePoint(lon, lat, alt));
                        times.Add(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                        previous = time;
                    }
                    if (valid)
                    {
                        placemark.ItemType = PlaceItemType.Path;
                        placemark.Coordinates = fixes;
                        placemark.Longitude = fixes[0].Longitude;
                        placemark.Latitude = fixes[0].Latitude;
                        placemark.Altitude = fixes[0].Altitude;
                        placemark.TimeSpanBegin = times[0];
                        placemark.TimeSpanEnd = times[^1];
                        placemark.ExtendedData["GpsSourceKind"] = "KML gx:Track";
                        placemark.ExtendedData["GpsKmlTrack"] = "true";
                        placemark.ExtendedData["GpsPointTimesUtcJson"] = JsonSerializer.Serialize(times);
                        return;
                    }
                }
            }

            // 3. LineString
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

        private static void ExtractAttributesFromHtmlTable(string html, Dictionary<string, string> target)
        {
            if (string.IsNullOrWhiteSpace(html)) return;
            var rowRegex = new Regex(@"<tr[^>]*>\s*<(?:td|th)[^>]*>(.*?)<\/(?:td|th)>\s*<td[^>]*>(.*?)<\/td>\s*<\/tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var stripTags = new Regex(@"<[^>]+>", RegexOptions.Compiled);

            foreach (Match m in rowRegex.Matches(html))
            {
                string rawKey = stripTags.Replace(m.Groups[1].Value, string.Empty).Trim();
                string rawVal = stripTags.Replace(m.Groups[2].Value, string.Empty).Trim();
                string key = System.Net.WebUtility.HtmlDecode(rawKey);
                string val = System.Net.WebUtility.HtmlDecode(rawVal);
                if (!string.IsNullOrWhiteSpace(key) && !target.ContainsKey(key))
                {
                    target[key] = val;
                }
            }
        }
    }
}
