using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ZEarth.Domain.Kml;

/// <summary>
/// Interprets the common KML 2.2 style subset for the live Cesium view
/// without modifying the preserved source XML or external asset references.
/// Unsupported styles remain intact for round-trip export.
/// </summary>
public static class KmlStyleResolver
{
    public static void Apply(PlaceItem documentRoot)
    {
        ArgumentNullException.ThrowIfNull(documentRoot);
        var styles = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var maps = new Dictionary<string, string>(StringComparer.Ordinal);
        var styled = new List<(PlaceItem Item, XElement? Inline)>();

        void Gather(PlaceItem item)
        {
            XElement? inline = null;
            foreach (var fragment in item.PreservedXmlFragments)
            {
                XElement xml;
                try { xml = XElement.Parse(fragment); }
                catch (System.Xml.XmlException) { continue; }
                var id = xml.Attribute("id")?.Value;
                switch (xml.Name.LocalName)
                {
                    case "Style":
                        if (!string.IsNullOrWhiteSpace(id))
                            styles.TryAdd("#" + id, xml);
                        else inline = xml;
                        break;
                    case "StyleMap":
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var normal = xml.Elements().FirstOrDefault(e => e.Name.LocalName == "Pair" &&
                                Child(e, "key") == "normal");
                            var url = normal != null ? Child(normal, "styleUrl") : null;
                            if (!string.IsNullOrWhiteSpace(url))
                                maps["#" + id] = url.Trim();
                        }
                        break;
                }
            }
            styled.Add((item, inline));
            foreach (var child in item.Children) Gather(child);
        }
        Gather(documentRoot);
        foreach (var (item, inline) in styled)
        {
            if (item.ItemType == PlaceItemType.Folder) continue;
            var style = inline;
            if (style == null && !string.IsNullOrWhiteSpace(item.StyleUrl))
            {
                var styleUrl = item.StyleUrl.Trim();
                // Resolve StyleMap 'normal' without allowing cyclic references.
                for (var depth = 0; depth < 8 && maps.TryGetValue(styleUrl, out var next); depth++)
                    styleUrl = next;
                styles.TryGetValue(styleUrl, out style);
            }
            if (style == null) continue;
            var line = style.Elements().FirstOrDefault(e => e.Name.LocalName == "LineStyle");
            var poly = style.Elements().FirstOrDefault(e => e.Name.LocalName == "PolyStyle");
            var icon = style.Elements().FirstOrDefault(e => e.Name.LocalName == "IconStyle");
            var label = style.Elements().FirstOrDefault(e => e.Name.LocalName == "LabelStyle");
            var color = Child(item.ItemType == PlaceItemType.Path ? line
                        : item.ItemType == PlaceItemType.Polygon ? poly : icon, "color");
            if (TryParseKmlColor(color, out var css, out var alpha))
            {
                item.ColorHex = css;
                if (item.ItemType == PlaceItemType.Polygon)
                    item.ExtendedData["FillOpacity"] = alpha.ToString("0.####", CultureInfo.InvariantCulture);
                else if (alpha < 1)
                    item.ExtendedData["KmlAlpha"] = alpha.ToString("0.####", CultureInfo.InvariantCulture);
            }
            var width = Child(line, "width");
            if (double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels)
                && double.IsFinite(pixels) && pixels > 0 && pixels < 100)
                item.ExtendedData["LineWidth"] = pixels.ToString(CultureInfo.InvariantCulture);
            var href = icon?.Descendants().FirstOrDefault(e => e.Name.LocalName == "href")?.Value;
            if (!string.IsNullOrWhiteSpace(href))
                item.ExtendedData["KmlIconHref"] = href.Trim();
            var size = Child(label, "scale");
            if (double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                && double.IsFinite(scale) && scale > 0 && scale <= 10)
                item.ExtendedData["LabelSize"] = (12 * scale).ToString(CultureInfo.InvariantCulture);
        }
    }

    public static bool TryParseKmlColor(string? abgr, out string css, out double alpha)
    {
        css = "#00c8ff";
        alpha = 1;
        if (abgr == null || abgr.Length != 8 ||
            !uint.TryParse(abgr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;
        var a = (byte)(value >> 24);
        var b = (byte)(value >> 16);
        var g = (byte)(value >> 8);
        var r = (byte)value;
        css = $"#{r:X2}{g:X2}{b:X2}";
        alpha = a / 255d;
        return true;
    }

    private static string? Child(XElement? element, string name) =>
        element?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
}
