using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Layers;
using ZEarth.Domain.Settings;

namespace ZEarth.Tests;

[TestClass]
public class ReferenceGridWaybackAcceptanceTests
{
    [TestMethod]
    public void ReferenceCatalog_UsesNineGlobalOverlayCategories()
    {
        var catalog = new ReferenceLayerCatalog();
        var groups = catalog.GetGroupedCategories();

        Assert.AreEqual(9, groups.Count);
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ReferenceLayerCategory>(),
            groups.Select(g => g.Category).ToArray());
        Assert.IsTrue(catalog.Layers.Any(l => l.LayerType == "OverpassGeoJson" && !string.IsNullOrWhiteSpace(l.Query)));
        Assert.IsTrue(catalog.Layers.Any(l => l.LayerType == "WikimediaGeoSearch"));
        Assert.IsTrue(catalog.Layers.Any(l => l.LayerType == "WikipediaGeoSearch"));
        Assert.IsFalse(catalog.Layers.Any(l => l.Name.Contains("Ocean Basemap", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void BasemapRegistry_GroupsOsmGeologyAndOceanProducts()
    {
        var providers = ImageryRegistry.Providers;
        Assert.IsTrue(providers.Count(p => p.ProviderGroup == "OpenStreetMap") >= 4);
        Assert.IsTrue(providers.Any(p => p.Category == BasemapCategory.Geology));
        Assert.IsTrue(providers.Any(p => p.Category == BasemapCategory.Ocean));
        Assert.IsFalse(providers.Any(p => p.Id.Contains("google", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Snapping_DefaultsToGroundMeters()
    {
        var settings = SnappingSettings.Instance;
        Assert.AreEqual(SnappingMode.GroundDistance, settings.Mode);
        Assert.IsTrue(settings.GroundToleranceMeters > 0);
    }

    [TestMethod]
    public void WaybackRelease_BuildsPublicEsriTileTemplate()
    {
        var release = new WaybackRelease("32246", new DateTime(2026, 6, 30), "World Imagery (Wayback 2026-06-30)");
        StringAssert.Contains(release.TileUrl, "wayback.maptiles.arcgis.com");
        StringAssert.Contains(release.TileUrl, "/tile/32246/{z}/{y}/{x}");
        Assert.AreEqual("wayback-32246", release.Id);
    }
}
