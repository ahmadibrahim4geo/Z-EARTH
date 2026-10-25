using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ZEarth.Domain.Kml;

/// <summary>
/// Builds a portable selected-item export and carries any document/folder
/// Style/StyleMap/Schema definitions and KMZ assets inherited from ancestors.
/// The original Places tree remains untouched.
/// </summary>
public static class KmlSelectionExport
{
    public static PlaceItem Create(PlaceItem selected, IEnumerable<PlaceItem> currentRoots)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(currentRoots);
        var path = new List<PlaceItem>();
        bool Find(PlaceItem node)
        {
            path.Add(node);
            if (ReferenceEquals(node, selected) || node.Id == selected.Id) return true;
            foreach (var child in node.Children)
                if (Find(child)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
        foreach (var root in currentRoots)
            if (Find(root)) break;

        var export = new PlaceItem(selected.Name, PlaceItemType.Folder);
        foreach (var ancestor in path.Take(Math.Max(0, path.Count - 1)))
        {
            foreach (var xml in ancestor.PreservedXmlFragments)
            {
                XElement element;
                try { element = XElement.Parse(xml); }
                catch (System.Xml.XmlException) { continue; }
                if (element.Name.LocalName is "Style" or "StyleMap" or "Schema")
                    if (!export.PreservedXmlFragments.Contains(xml))
                        export.PreservedXmlFragments.Add(xml);
            }
            foreach (var asset in ancestor.KmzResourcesBase64)
            {
                if (export.KmzResourcesBase64.TryGetValue(asset.Key, out var existing) &&
                    existing != asset.Value)
                    throw new System.IO.InvalidDataException(
                        "Conflicting inherited KMZ asset paths; cannot create portable export.");
                export.KmzResourcesBase64[asset.Key] = asset.Value;
            }
        }
        export.AddChild(selected.Clone());
        return export;
    }
}
