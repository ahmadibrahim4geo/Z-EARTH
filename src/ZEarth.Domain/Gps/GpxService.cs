using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ZEarth.Domain.Gps;

/// <summary>
/// Safe, offline GPX 1.0/1.1 waypoint, route and segmented track importer.
/// It preserves point timestamps separately from elevation; missing elevation
/// is never asserted to be measured terrain (CoordinatePoint requires a number).
/// </summary>
public static class GpxService
{
    public const long MaxFileBytes = 16 * 1024 * 1024;
    public const int MaxGpsPoints = 200_000;
    public const int MaxGpsSegments = 5_000;

    private sealed record GpsFix(CoordinatePoint Coordinate, string? TimeUtc, bool HasElevation);
    private static string? ChildText(XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

    private static IEnumerable<XElement> Children(XElement element, string name) =>
        element.Elements().Where(e => e.Name.LocalName == name);

    public static PlaceItem ImportFile(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("GPX filename is required.", nameof(filename));
        using var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaxFileBytes) throw new InvalidDataException("GPX file exceeds 16 MiB.");
        return Import(file, Path.GetFileNameWithoutExtension(filename));
    }

    public static PlaceItem Import(Stream stream, string fallbackName = "GPS Import")
    {
        if (stream == null || !stream.CanRead) throw new ArgumentException("Readable GPX stream is required.", nameof(stream));
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxFileBytes,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true
        });
        var doc = XDocument.Load(reader, LoadOptions.None);
        var gpx = doc.Root;
        if (gpx == null || gpx.Name.LocalName != "gpx") throw new InvalidDataException("The document is not GPX.");
        string rootName = ChildText(gpx.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata") ?? gpx, "name")
            ?? (string.IsNullOrWhiteSpace(fallbackName) ? "GPS Import" : fallbackName);
        var output = new PlaceItem(rootName, PlaceItemType.Folder);
        output.ExtendedData["GpsFormat"] = "GPX";
        var count = 0;
        var segments = 0;

        GpsFix ReadFix(XElement elem)
        {
            if (++count > MaxGpsPoints) throw new InvalidDataException("GPX point count limit exceeded.");
            var latAttr = elem.Attribute("lat")?.Value;
            var lonAttr = elem.Attribute("lon")?.Value;
            if (!double.TryParse(latAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(lonAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                !double.IsFinite(lat) || !double.IsFinite(lon) ||
                lat < -90 || lat > 90 || lon < -180 || lon > 180)
                throw new InvalidDataException("Invalid GPX longitude or latitude.");
            string? elevText = ChildText(elem, "ele");
            bool hasElevation = !string.IsNullOrWhiteSpace(elevText);
            double elev = 0;
            if (hasElevation && (!double.TryParse(elevText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out elev) || !double.IsFinite(elev)))
                throw new InvalidDataException("Invalid GPX elevation.");
            string? time = ChildText(elem, "time");
            string? utc = null;
            if (!string.IsNullOrWhiteSpace(time))
            {
                if (!DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var stamp))
                    throw new InvalidDataException("Invalid GPX time.");
                utc = stamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }
            return new GpsFix(new CoordinatePoint(lon, lat, elev), utc, hasElevation);
        }

        PlaceItem BuildLine(string name, IEnumerable<XElement> fixes, string source)
        {
            var points = fixes.Select(ReadFix).ToList();
            var item = new PlaceItem(name, points.Count > 1 ? PlaceItemType.Path : PlaceItemType.Folder);
            item.ExtendedData["GpsSourceKind"] = source;
            item.ExtendedData["GpsPointTimesUtcJson"] = JsonSerializer.Serialize(points.Select(p => p.TimeUtc).ToList());
            item.ExtendedData["GpsMissingElevationCount"] = points.Count(p => !p.HasElevation).ToString(CultureInfo.InvariantCulture);
            item.Description = "GPS " + source + "; altitude values represent the source file (no DEM sampling).";
            foreach (var point in points) item.Coordinates.Add(point.Coordinate);
            var validTimes = points.Select(p => p.TimeUtc).Where(t => t != null).ToList();
            if (validTimes.Count > 0)
            {
                item.TimeSpanBegin = validTimes.Min(StringComparer.Ordinal);
                item.TimeSpanEnd = validTimes.Max(StringComparer.Ordinal);
            }
            if (points.Count == 1)
            {
                var only = points[0];
                item.ItemType = PlaceItemType.Placemark;
                item.Longitude = only.Coordinate.Longitude;
                item.Latitude = only.Coordinate.Latitude;
                item.Altitude = only.Coordinate.Altitude;
            }
            return item;
        }

        foreach (var waypoint in Children(gpx, "wpt"))
        {
            var fix = ReadFix(waypoint);
            var item = new PlaceItem(ChildText(waypoint, "name") ?? "Waypoint", PlaceItemType.Placemark,
                fix.Coordinate.Latitude, fix.Coordinate.Longitude, fix.Coordinate.Altitude);
            item.Description = ChildText(waypoint, "desc") ?? "";
            item.ExtendedData["GpsSourceKind"] = "Waypoint";
            item.ExtendedData["GpsElevationPresent"] = fix.HasElevation ? "true" : "false";
            if (fix.TimeUtc != null)
            {
                item.TimeSpanBegin = fix.TimeUtc;
                item.TimeSpanEnd = fix.TimeUtc;
                item.ExtendedData["GpsTimeUtc"] = fix.TimeUtc;
            }
            output.Children.Add(item);
        }
        int routeNumber = 0;
        foreach (var route in Children(gpx, "rte"))
        {
            var name = ChildText(route, "name") ?? $"Route {++routeNumber}";
            var item = BuildLine(name, Children(route, "rtept"), "Route");
            if (item.Coordinates.Count > 0) output.Children.Add(item);
        }
        int trackNumber = 0;
        foreach (var track in Children(gpx, "trk"))
        {
            var name = ChildText(track, "name") ?? $"Track {++trackNumber}";
            var container = new PlaceItem(name, PlaceItemType.Folder);
            int segmentNumber = 0;
            foreach (var seg in Children(track, "trkseg"))
            {
                if (++segments > MaxGpsSegments) throw new InvalidDataException("GPX track segment count limit exceeded.");
                var item = BuildLine($"{name} / Segment {++segmentNumber}", Children(seg, "trkpt"), "Track");
                if (item.Coordinates.Count > 0) container.Children.Add(item);
            }
            if (container.Children.Count > 0) output.Children.Add(container);
        }
        if (count == 0 || output.Children.Count == 0)
            throw new InvalidDataException("The GPX file contains no supported waypoints, routes or track segments.");
        output.ExtendedData["GpsPointCount"] = count.ToString(CultureInfo.InvariantCulture);
        output.ExtendedData["GpsSegmentCount"] = segments.ToString(CultureInfo.InvariantCulture);
        return output;
    }
}
