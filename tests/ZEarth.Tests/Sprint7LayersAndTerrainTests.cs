using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Terrain;
using ZEarth.Globe;

namespace ZEarth.Tests;

[TestClass]
public class Sprint7LayersAndTerrainTests
{
    [TestMethod]
    public void ImageryRegistry_BuiltInProviders_AreConfiguredCorrectly()
    {
        var registry = new ImageryRegistry();
        var layers = registry.Layers;

        Assert.IsTrue(layers.Count >= 4, "Registry should contain at least 4 built-in layers");

        var offlineLayer = registry.GetLayer(ImageryRegistry.ProviderNaturalEarth);
        Assert.IsNotNull(offlineLayer);
        Assert.IsTrue(offlineLayer.IsBaseLayer);
        Assert.IsTrue(offlineLayer.IsOfflineAvailable);
        StringAssert.Contains(offlineLayer.Name, "Natural Earth");

        var osmLayer = registry.GetLayer(ImageryRegistry.ProviderOpenStreetMap);
        Assert.IsNotNull(osmLayer);
        Assert.IsTrue(osmLayer.IsBaseLayer);
        Assert.IsFalse(osmLayer.IsOfflineAvailable);
        StringAssert.Contains(osmLayer.Attribution, "OpenStreetMap");

        var topoLayer = registry.GetLayer(ImageryRegistry.ProviderOpenTopoMap);
        Assert.IsNotNull(topoLayer);
        Assert.AreEqual(ImageryProviderType.OpenTopoMap, topoLayer.ProviderType);

        var darkLayer = registry.GetLayer(ImageryRegistry.ProviderCartoDark);
        Assert.IsNotNull(darkLayer);
        Assert.AreEqual(ImageryProviderType.CartoDbDark, darkLayer.ProviderType);
    }

    [TestMethod]
    public void ImageryRegistry_AddCustomLayer_ValidatesAndIndexesCorrectly()
    {
        var registry = new ImageryRegistry();
        int initialCount = registry.Layers.Count;

        var custom = new ImageryLayer(
            id: "custom-voyager",
            name: "Carto Voyager",
            providerType: ImageryProviderType.XyzTileServer,
            attribution: "© CARTO",
            isBaseLayer: false,
            isOffline: false
        )
        {
            UrlTemplate = "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png"
        };

        bool added = registry.AddCustomLayer(custom);
        Assert.IsTrue(added);
        Assert.AreEqual(initialCount + 1, registry.Layers.Count);
        Assert.AreEqual(initialCount, custom.Order);

        // Adding duplicate ID must fail
        bool duplicateAdded = registry.AddCustomLayer(new ImageryLayer("custom-voyager", "Dup", ImageryProviderType.XyzTileServer, ""));
        Assert.IsFalse(duplicateAdded);
    }

    [TestMethod]
    public void ImageryRegistry_RemoveLayer_PreventsRemovingBaseLayers()
    {
        var registry = new ImageryRegistry();

        // Cannot remove built-in base layers
        bool removedBase = registry.RemoveLayer(ImageryRegistry.ProviderOpenStreetMap);
        Assert.IsFalse(removedBase);

        // Can remove custom layer
        var custom = new ImageryLayer("custom-1", "Custom 1", ImageryProviderType.XyzTileServer, "");
        registry.AddCustomLayer(custom);

        bool removedCustom = registry.RemoveLayer("custom-1");
        Assert.IsTrue(removedCustom);
        Assert.IsNull(registry.GetLayer("custom-1"));
    }

    [TestMethod]
    public void ImageryRegistry_Reordering_MovesUpAndDownCorrectly()
    {
        var registry = new ImageryRegistry();
        var l1 = new ImageryLayer("c-1", "Layer 1", ImageryProviderType.XyzTileServer, "");
        var l2 = new ImageryLayer("c-2", "Layer 2", ImageryProviderType.XyzTileServer, "");
        registry.AddCustomLayer(l1);
        registry.AddCustomLayer(l2);

        int idx1Before = registry.Layers.ToList().FindIndex(x => x.Id == "c-1");
        int idx2Before = registry.Layers.ToList().FindIndex(x => x.Id == "c-2");
        Assert.IsTrue(idx1Before < idx2Before);

        // Move c-2 Up
        bool movedUp = registry.MoveUp("c-2");
        Assert.IsTrue(movedUp);

        int idx2After = registry.Layers.ToList().FindIndex(x => x.Id == "c-2");
        Assert.AreEqual(idx1Before, idx2After);

        // Move c-2 Down
        bool movedDown = registry.MoveDown("c-2");
        Assert.IsTrue(movedDown);
        int idx2AfterDown = registry.Layers.ToList().FindIndex(x => x.Id == "c-2");
        Assert.AreEqual(idx2Before, idx2AfterDown);
    }

    [TestMethod]
    [DataRow("https://tile.openstreetmap.org/{z}/{x}/{y}.png", ImageryProviderType.OpenStreetMap, true)]
    [DataRow("http://server.com/tiles/{z}/{x}/{y}", ImageryProviderType.XyzTileServer, true)]
    [DataRow("https://server.com/tiles/{z}/{y}", ImageryProviderType.XyzTileServer, false)] // Missing {x}
    [DataRow("ftp://tiles.org/{z}/{x}/{y}.png", ImageryProviderType.XyzTileServer, false)] // Invalid scheme
    [DataRow("not-a-valid-url", ImageryProviderType.XyzTileServer, false)]
    [DataRow("", ImageryProviderType.XyzTileServer, false)]
    [DataRow("https://server.com/wms?service=WMS", ImageryProviderType.WmsServer, true)]
    public void ImageryRegistry_ValidateUrlTemplate_AcceptsValidAndRejectsMalformed(string url, ImageryProviderType type, bool expected)
    {
        bool isValid = ImageryRegistry.ValidateUrlTemplate(url, type);
        Assert.AreEqual(expected, isValid);
    }

    [TestMethod]
    public void ImageryRegistry_GetCombinedAttribution_ConcatenatesVisibleLayers()
    {
        var registry = new ImageryRegistry();
        var attr = registry.GetCombinedAttribution();
        Assert.IsNotNull(attr);
        StringAssert.Contains(attr, "Natural Earth");
        StringAssert.Contains(attr, "OpenStreetMap");

        // Hide OSM
        var osm = registry.GetLayer(ImageryRegistry.ProviderOpenStreetMap);
        if (osm != null) osm.IsVisible = false;

        var attrOfflineOnly = registry.GetCombinedAttribution();
        StringAssert.Contains(attrOfflineOnly, "Natural Earth");
        Assert.IsFalse(attrOfflineOnly.Contains("OpenStreetMap contributors"));
    }

    [TestMethod]
    public void TerrainProviderConfig_ExaggerationClamping_RespectsMinAndMax()
    {
        var config = new TerrainProviderConfig();
        Assert.AreEqual(1.0, config.Exaggeration);

        config.SetExaggeration(3.5);
        Assert.AreEqual(3.5, config.Exaggeration);

        // Min clamp (0.2)
        config.SetExaggeration(0.05);
        Assert.AreEqual(0.2, config.Exaggeration);

        // Max clamp (10.0)
        config.SetExaggeration(15.0);
        Assert.AreEqual(10.0, config.Exaggeration);

        // Reset
        config.ResetExaggeration();
        Assert.AreEqual(1.0, config.Exaggeration);

        // NaN / Infinity safety
        config.SetExaggeration(double.NaN);
        Assert.AreEqual(1.0, config.Exaggeration);

        config.SetExaggeration(double.PositiveInfinity);
        Assert.AreEqual(1.0, config.Exaggeration);
    }

    [TestMethod]
    public void TerrainProviderConfig_Presets_ContainsRequiredMultipliers()
    {
        var presets = TerrainProviderConfig.Presets;
        CollectionAssert.Contains(presets, 1.0);
        CollectionAssert.Contains(presets, 1.5);
        CollectionAssert.Contains(presets, 2.0);
        CollectionAssert.Contains(presets, 3.0);
        CollectionAssert.Contains(presets, 5.0);
    }

    [TestMethod]
    public void ImageryLayerViewModel_OpacityAndVisibility_NotifiesAndClamps()
    {
        var layer = new ImageryLayer("test-1", "Test Layer", ImageryProviderType.XyzTileServer, "Test Attribution");
        bool visCallbackCalled = false;
        bool opCallbackCalled = false;

        var vm = new ImageryLayerViewModel(
            layer,
            onVisibilityChanged: _ => visCallbackCalled = true,
            onOpacityChanged: _ => opCallbackCalled = true
        );

        vm.IsVisible = false;
        Assert.IsFalse(layer.IsVisible);
        Assert.IsTrue(visCallbackCalled);

        vm.Opacity = 0.75;
        Assert.AreEqual(0.75, layer.Opacity);
        Assert.AreEqual("75%", vm.OpacityPercentText);
        Assert.IsTrue(opCallbackCalled);

        // Clamping
        vm.Opacity = 1.8;
        Assert.AreEqual(1.0, vm.Opacity);
        Assert.AreEqual("100%", vm.OpacityPercentText);

        vm.Opacity = -0.5;
        Assert.AreEqual(0.0, vm.Opacity);
        Assert.AreEqual("0%", vm.OpacityPercentText);
    }

    [TestMethod]
    public void TerrainProviderConfig_CreateWorldTerrain_HasExpectedMetadata()
    {
        var world = TerrainProviderConfig.CreateWorldTerrain();
        Assert.AreEqual(TerrainType.CesiumWorldTerrain, world.Type);
        StringAssert.Contains(world.Name, "Global 3D Mesh");
        StringAssert.Contains(world.Attribution, "Copernicus DEM");
        Assert.IsFalse(string.IsNullOrWhiteSpace(world.Url));
    }

    [TestMethod]
    public void MainViewModel_SelectBasemap_UpdatesActiveBasemapAndAttribution()
    {
        var bridge = new GlobeBridge();
        var projectService = new ProjectService();
        var vm = new MainViewModel(projectService, bridge);

        Assert.IsNotNull(vm.SelectedBasemapId);

        // Select Offline Vector
        vm.ExecuteSelectBasemap(ImageryRegistry.ProviderNaturalEarth);
        Assert.AreEqual(ImageryRegistry.ProviderNaturalEarth, vm.SelectedBasemapId);
        StringAssert.Contains(vm.ActiveBasemap, "Natural Earth");

        // Select OpenTopo
        vm.ExecuteSelectBasemap(ImageryRegistry.ProviderOpenTopoMap);
        Assert.AreEqual(ImageryRegistry.ProviderOpenTopoMap, vm.SelectedBasemapId);
        StringAssert.Contains(vm.ActiveBasemap, "OpenTopoMap");
    }

    [TestMethod]
    public void MainViewModel_SetVerticalExaggeration_ClampsAndUpdatesDisplay()
    {
        var bridge = new GlobeBridge();
        var projectService = new ProjectService();
        var vm = new MainViewModel(projectService, bridge);

        Assert.AreEqual("1.0x", vm.VerticalExaggerationDisplay);

        vm.ExecuteSetVerticalExaggeration(3.0);
        Assert.AreEqual(3.0, vm.VerticalExaggeration);
        Assert.AreEqual("3.0x", vm.VerticalExaggerationDisplay);

        vm.ExecuteResetVerticalExaggeration();
        Assert.AreEqual(1.0, vm.VerticalExaggeration);
        Assert.AreEqual("1.0x", vm.VerticalExaggerationDisplay);
    }
}
