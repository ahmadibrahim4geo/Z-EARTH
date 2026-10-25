using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Layers;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    /// <summary>
    /// Task 02 coverage: reference layers used to reach a JS fall-through ("Unsupported action:
    /// reference.setLayer"), so no catalog entry could ever render. These tests pin the engine
    /// contract, the provider templates, and the host dispatch/error path.
    /// </summary>
    [TestClass]
    public class ReferenceLayerTask02Tests
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ZEarth.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "ZEarth.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Repository root could not be located.");
        }

        private static string RepoFile(params string[] relativeParts)
            => Path.Combine(new[] { FindRepoRoot() }.Concat(relativeParts).ToArray());

        [TestMethod]
        public void Engine_ImplementsReferenceLayerContract()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            Assert.IsTrue(js.Contains("const ReferenceLayerManager"), "Engine must define the reference layer manager");
            Assert.IsTrue(js.Contains("case 'reference.setLayer'"), "Engine must handle reference.setLayer");
            Assert.IsTrue(js.Contains("case 'reference.setOpacity'"), "Engine must handle reference.setOpacity");
            Assert.IsTrue(js.Contains("ArcGisMapServerImageryProvider.fromUrl"), "ArcGIS MapServer reference layers must be supported");
            Assert.IsTrue(js.Contains("GeoJsonDataSource.load"), "GeoJSON reference feeds (USGS earthquakes) must be supported");
            Assert.IsTrue(js.Contains("reference.error"), "Provider failures must be reported to the host");
            Assert.IsTrue(js.Contains("type === 'Graticule'"), "Built-in graticules must route to the graticule engine");
        }

        [TestMethod]
        public void Catalog_GibsTemplates_AreWebMercatorCompatible()
        {
            var catalog = new ReferenceLayerCatalog();
            var gibs = catalog.Layers.Where(l => l.LayerType == "WmtsGibs").ToList();

            Assert.IsTrue(gibs.Count > 0, "The catalog must contain at least one NASA GIBS layer");
            foreach (var layer in gibs)
            {
                Assert.IsTrue(layer.Url.Contains("epsg3857"), $"{layer.Id}: Cesium tiles are Web Mercator, not EPSG:4326");
                Assert.IsTrue(layer.Url.Contains("GoogleMapsCompatible"), $"{layer.Id}: GIBS requires the GoogleMapsCompatible tile matrix set");
                Assert.IsFalse(layer.Url.Contains("{TileMatrix}"), $"{layer.Id}: raw WMTS tokens are not understood by UrlTemplateImageryProvider");
                Assert.IsTrue(layer.Url.Contains("{z}") && layer.Url.Contains("{x}") && layer.Url.Contains("{y}"),
                    $"{layer.Id}: the URL must expose z/x/y placeholders");
                Assert.IsFalse(string.IsNullOrWhiteSpace(layer.DateOrTimestamp), $"{layer.Id}: a UTC date token is required");
            }
        }

        [TestMethod]
        public void Catalog_GraticuleDefault_MatchesEngineDefault()
        {
            var catalog = new ReferenceLayerCatalog();
            var graticule = catalog.FindById("ref_geographic_graticules");

            Assert.IsNotNull(graticule);
            Assert.AreEqual("Graticule", graticule!.LayerType);
            Assert.AreEqual("builtin://graticules", graticule.Url);
            // GraticuleManager starts with visible:true; the catalog must not claim otherwise.
            Assert.IsTrue(graticule.IsVisible, "The graticule checkbox must reflect the engine's initial visible state");
        }

        [TestMethod]
        public void Catalog_EveryLayer_HasProviderUrlAndType()
        {
            var catalog = new ReferenceLayerCatalog();

            Assert.AreEqual(9, catalog.GetGroupedCategories().Count, "All nine mandatory categories must remain present");
            Assert.AreEqual(catalog.Layers.Count, catalog.Layers.Select(l => l.Id).Distinct().Count(), "Layer ids must be unique");

            foreach (var layer in catalog.Layers)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(layer.Url), $"{layer.Id}: missing URL");
                Assert.IsFalse(string.IsNullOrWhiteSpace(layer.LayerType), $"{layer.Id}: missing layer type");
                Assert.IsFalse(string.IsNullOrWhiteSpace(layer.Attribution), $"{layer.Id}: missing attribution");
                Assert.IsTrue(new[] { "RasterTile", "ArcGisMapServer", "WmtsGibs", "GeoJson", "Graticule" }.Contains(layer.LayerType),
                    $"{layer.Id}: unexpected layer type '{layer.LayerType}'");
            }
        }

        [TestMethod]
        public void Bridge_DispatchesReferenceSetLayer_WithCatalogPayload()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);
            var layer = new ReferenceLayerCatalog().FindById("ref_borders_coastlines")!;

            var ok = bridge.SetReferenceLayerAsync(
                layer.Id, true, 0.4, layer.Name, layer.Url, layer.LayerType, layer.Attribution,
                layer.WmtsLayer, layer.DateOrTimestamp).GetAwaiter().GetResult();

            Assert.IsTrue(ok);
            var json = engine.LastPayloadFor("reference.setLayer");
            Assert.IsFalse(string.IsNullOrEmpty(json), "The host must post reference.setLayer");

            using var doc = JsonDocument.Parse(json);
            var payload = doc.RootElement.GetProperty("payload");
            Assert.AreEqual("ref_borders_coastlines", payload.GetProperty("id").GetString());
            Assert.IsTrue(payload.GetProperty("visible").GetBoolean());
            Assert.AreEqual(0.4, payload.GetProperty("opacity").GetDouble(), 1e-9);
            Assert.AreEqual("ArcGisMapServer", payload.GetProperty("layerType").GetString());
            Assert.AreEqual(layer.Url, payload.GetProperty("url").GetString());
        }

        [TestMethod]
        public void Bridge_SurfacesReferenceProviderError()
        {
            var bridge = new GlobeBridge();
            ReferenceLayerErrorInfo? reported = null;
            bridge.ReferenceLayerError += (_, info) => reported = info;

            bridge.HandleWebMessage(JsonSerializer.Serialize(new
            {
                @event = "reference.error",
                id = "ref_earth_observation_gibs",
                message = "Failed to obtain image tile X: 1 Y: 3 Level: 2.",
                timesRetried = 0,
                retryable = true
            }));

            Assert.IsNotNull(reported, "reference.error must raise ReferenceLayerError instead of falling through");
            Assert.AreEqual("ref_earth_observation_gibs", reported!.Id);
            Assert.IsTrue(reported.Message.Contains("Failed to obtain image tile"));
            Assert.IsTrue(reported.Retryable);
        }

        [TestMethod]
        public void Catalog_StatusBadge_ReflectsUnavailableProvider()
        {
            var layer = new ReferenceLayerCatalog().FindById("ref_borders_coastlines")!;

            layer.Status = ReferenceLayerStatus.ProviderUnavailable;

            Assert.IsFalse(layer.IsStatusActive);
            Assert.AreEqual("▲ Unavailable", layer.StatusBadgeText);
        }

        [TestMethod]
        public void ZoomToReferenceLayer_UsesFullExtent_NotHardcodedExtent()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);
            var vm = new ZEarth.App.ViewModels.MainViewModel(new ZEarth.App.Services.ProjectService(), bridge);
            var layer = vm.ReferenceLayerCatalog.FindById("ref_borders_coastlines")!;

            vm.ZoomToReferenceLayerCommand.Execute(layer);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline && !engine.Any("camera.fullExtent"))
            {
                System.Threading.Thread.Sleep(10);
            }

            Assert.IsTrue(engine.Any("camera.fullExtent"), "Zoom to a global reference layer must request the full extent");
            Assert.IsFalse(engine.Any("camera.flyTo"), "The old hard-coded Egypt fly-to must be gone");
        }
    }
}
