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

    [TestMethod]
    public void WaybackConfig_ParsesActualEsriSchemaWithReleaseDatesAndTrustedWmts()
    {
        var json = "{\"56102\":{\"itemTitle\":\"World Imagery (Wayback 2023-12-07)\"," +
            "\"releaseNum\":56102,\"releaseDateLabel\":\"2023-12-07\"," +
            "\"itemURL\":\"https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/56102/{level}/{row}/{col}\"}}";
        var records = WaybackService.ParseReleasesJson(json);
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(new DateTime(2023,12,7), records[0].Date);
        StringAssert.Contains(records[0].TileUrl, "/tile/56102/{z}/{y}/{x}");
    }

    [TestMethod]
    public void WaybackConfig_DateOnlyReleaseLabel_RemainsCalendarDateInLocalTimezone()
    {
        var json = "{\"56102\":{\"releaseNum\":56102,\"releaseDateLabel\":\"2023-12-07\"," +
            "\"itemURL\":\"https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tile/56102/{level}/{row}/{col}\"}}";

        var releases = WaybackService.ParseReleasesJson(json);
        Assert.AreEqual(1, releases.Count);
        var release = releases[0];
        Assert.AreEqual(new DateTime(2023, 12, 7), release.Date);
        Assert.AreEqual(TimeSpan.Zero, release.Date.TimeOfDay);
    }

    [TestMethod]
    public void WaybackConfig_RejectsUntrustedOriginAndDoesNotFabricateFallbackReleases()
    {
        var json = "{\"2023\":{\"releaseNum\":123,\"releaseDateLabel\":\"2023-12-07\"," +
            "\"itemURL\":\"https://evil.example/World_Imagery/MapServer/tile/123/{level}/{row}/{col}\"}}";
        Assert.AreEqual(0, WaybackService.ParseReleasesJson(json).Count);
        Assert.IsNull(WaybackService.NormalizeTileUrl("https://wayback.maptiles.arcgis.com.evil.example/World_Imagery/tile/123/{z}/{y}/{x}"));
    }
}
