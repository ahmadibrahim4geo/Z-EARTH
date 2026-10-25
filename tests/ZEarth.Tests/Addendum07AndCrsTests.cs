using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Measurement;

namespace ZEarth.Tests
{
    [TestClass]
    public class Addendum07AndCrsTests
    {
        [TestMethod]
        public void QiblaAzimuth_Cairo_ComputesApproximately136Degrees()
        {
            // Cairo coordinates: 30.0444° N, 31.2357° E
            double cairoLat = 30.0444;
            double cairoLon = 31.2357;

            double azimuth = GeodesicMeasurementService.CalculateQiblaAzimuth(cairoLat, cairoLon);

            // True Vincenty inverse bearing from Cairo to Kaaba on WGS84 is ~135.98° -> rounds to 136°
            Assert.AreEqual(136.0, Math.Round(azimuth), 1.0, $"Expected Cairo Qibla azimuth ~136°, got {azimuth:F2}°");
        }

        [TestMethod]
        public void QiblaAzimuth_Riyadh_ComputesApproximately244Degrees()
        {
            // Riyadh coordinates: 24.7136° N, 46.6753° E
            double riyadhLat = 24.7136;
            double riyadhLon = 46.6753;

            double azimuth = GeodesicMeasurementService.CalculateQiblaAzimuth(riyadhLat, riyadhLon);

            // True Vincenty inverse bearing from Riyadh to Kaaba on WGS84 is ~243.93° -> rounds to 244°
            Assert.AreEqual(244.0, Math.Round(azimuth), 1.0, $"Expected Riyadh Qibla azimuth ~244°, got {azimuth:F2}°");
        }

        [TestMethod]
        public void BasemapRegistry_AllTenProviders_CategorizedProperly()
        {
            var registry = new ImageryRegistry();
            var providers = registry.Basemaps.ToList();

            Assert.IsTrue(providers.Count >= 10, $"At least 10 basemap providers expected, got {providers.Count}");

            // Verify categories
            var offline = providers.First(p => p.Id == "offline-vector");
            Assert.AreEqual(BasemapCategory.Offline, offline.Category);

            var esriSat = providers.First(p => p.Id == "esri-imagery");
            Assert.AreEqual(BasemapCategory.Satellite, esriSat.Category);

            var esriHybrid = providers.First(p => p.Id == "esri-hybrid");
            Assert.AreEqual(BasemapCategory.Hybrid, esriHybrid.Category);

            var osm = providers.First(p => p.Id == "osm-standard");
            Assert.AreEqual(BasemapCategory.Streets, osm.Category);

            var esriStreets = providers.First(p => p.Id == "esri-streets");
            Assert.AreEqual(BasemapCategory.Streets, esriStreets.Category);

            var openTopo = providers.First(p => p.Id == "opentopo");
            Assert.AreEqual(BasemapCategory.Topographic, openTopo.Category);

            var esriTopo = providers.First(p => p.Id == "esri-topo");
            Assert.AreEqual(BasemapCategory.Topographic, esriTopo.Category);

            var cartoDark = providers.First(p => p.Id == "carto-dark");
            Assert.AreEqual(BasemapCategory.Cartographic, cartoDark.Category);

            var cartoLight = providers.First(p => p.Id == "carto-light");
            Assert.AreEqual(BasemapCategory.Cartographic, cartoLight.Category);

            var esriGray = providers.First(p => p.Id == "esri-gray");
            Assert.AreEqual(BasemapCategory.Cartographic, esriGray.Category);
        }

        [TestMethod]
        public void CrsRegistry_Taxonomy_ContainsRequiredCategoriesAndProjections()
        {
            var registry = new CrsRegistry();
            var all = registry.GetAll();

            Assert.IsTrue(all.Count >= 25, $"Catalog should have comprehensive definitions, found {all.Count}");

            // Verify WGS 84
            var wgs84 = all.FirstOrDefault(c => c.EpsgCode == 4326);
            Assert.IsNotNull(wgs84);
            Assert.AreEqual("EPSG:4326", wgs84.AuthorityCode);
            Assert.IsFalse(wgs84.IsVerticalOnly);

            // Verify Egypt Red Belt
            var redBelt = all.FirstOrDefault(c => c.EpsgCode == 22992);
            Assert.IsNotNull(redBelt);
            Assert.AreEqual("Egypt 1907 / Red Belt", redBelt.Name);
            Assert.AreEqual("EPSG:22992", redBelt.AuthorityCode);
            Assert.IsTrue(redBelt.CategoryPath.Contains("Africa/Egypt") || redBelt.CategoryPath.Contains("Egypt"));

            // Verify UTM Zones 35N, 36N, 37N
            Assert.IsTrue(all.Any(c => c.EpsgCode == 32635), "UTM Zone 35N EPSG:32635 must be present");
            Assert.IsTrue(all.Any(c => c.EpsgCode == 32636), "UTM Zone 36N EPSG:32636 must be present");
            Assert.IsTrue(all.Any(c => c.EpsgCode == 32637), "UTM Zone 37N EPSG:32637 must be present");

            // Verify Web Mercator
            Assert.IsTrue(all.Any(c => c.EpsgCode == 3857), "Web Mercator EPSG:3857 must be present");
        }

        [TestMethod]
        public void CrsRegistry_VerticalOnly_CannotBeAppliedTo2DMap()
        {
            var registry = new CrsRegistry();
            var vertEgm96 = registry.GetAll().FirstOrDefault(c => c.EpsgCode == 5773);
            Assert.IsNotNull(vertEgm96, "EGM96 vertical CRS should be registered");
            Assert.IsTrue(vertEgm96.IsVerticalOnly);

            // In ViewModel, CanApply should be false for vertical-only
            var vm = new CrsSelectionViewModel(registry, 4326);
            vm.SelectedCrs = vertEgm96;
            Assert.IsFalse(vm.CanApply, "Vertical-only CRS must not be applicable as horizontal 2D Map CRS");

            // But standard 2D CRS can be applied
            var redBelt = registry.GetByEpsg(22992);
            Assert.IsNotNull(redBelt);
            vm.SelectedCrs = redBelt;
            Assert.IsTrue(vm.CanApply, "Projected horizontal CRS must be applicable as 2D Map CRS");
        }

        [TestMethod]
        public void CrsSelectionViewModel_Favorites_PersistsFavoritesInSettings()
        {
            var registry = new CrsRegistry();
            var settings = SettingsService.Instance;
            var originalFavs = new List<int>(settings.Settings.FavoriteCrsList);

            try
            {
                var vm = new CrsSelectionViewModel(registry, 4326);
                var utm36 = registry.GetByEpsg(32636);
                Assert.IsNotNull(utm36);

                vm.SelectedCrs = utm36;
                bool initialFav = vm.IsSelectedFavorite;

                // Toggle favorite
                vm.ToggleFavoriteCommand.Execute(utm36);
                Assert.AreNotEqual(initialFav, vm.IsSelectedFavorite);

                // Verify setting updated
                if (vm.IsSelectedFavorite)
                {
                    Assert.IsTrue(settings.Settings.FavoriteCrsList.Contains(32636));
                }
            }
            finally
            {
                // Restore original favorites
                settings.Settings.FavoriteCrsList = originalFavs;
                settings.SaveSettings();
            }
        }

        [TestMethod]
        public void CrsSelectionViewModel_SearchFilter_FindsEgyptRedBeltAndUtm()
        {
            var registry = new CrsRegistry();
            var vm = new CrsSelectionViewModel(registry, 4326);

            // Search by "Red Belt"
            vm.SearchQuery = "Red Belt";
            Assert.IsTrue(vm.SearchResults.Count > 0);
            Assert.IsTrue(vm.SearchResults.Any(c => c.EpsgCode == 22992));

            // Search by "22992"
            vm.SearchQuery = "22992";
            Assert.AreEqual(1, vm.SearchResults.Count);
            Assert.AreEqual(22992, vm.SearchResults[0].EpsgCode);

            // Search by "32636"
            vm.SearchQuery = "32636";
            Assert.AreEqual(1, vm.SearchResults.Count);
            Assert.AreEqual(32636, vm.SearchResults[0].EpsgCode);
        }

        [TestMethod]
        public void BasemapRegistry_SetBasemap_ActivatesTargetAndDeactivatesOthers()
        {
            var registry = new ImageryRegistry();
            registry.SetBasemap(ImageryRegistry.ProviderEsriImagery);

            var active = registry.Basemaps.Where(b => b.IsVisible).ToList();
            Assert.AreEqual(1, active.Count, "Exactly one basemap must be visible after SetBasemap");
            Assert.AreEqual(ImageryRegistry.ProviderEsriImagery, active[0].Id);

            // Switch to OpenTopoMap
            registry.SetBasemap(ImageryRegistry.ProviderOpenTopoMap);
            active = registry.Basemaps.Where(b => b.IsVisible).ToList();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(ImageryRegistry.ProviderOpenTopoMap, active[0].Id);
        }

        [TestMethod]
        public void MainViewModel_AvailableBasemaps_HasTenBasemaps()
        {
            var vm = new MainViewModel(new ProjectService(), new ZEarth.Globe.GlobeBridge());
            Assert.IsTrue(vm.AvailableBasemaps.Count >= 10, $"MainViewModel.AvailableBasemaps must expose at least 10 basemaps, got {vm.AvailableBasemaps.Count}");
            Assert.IsTrue(vm.AvailableBasemaps.Any(b => b.Id == ImageryRegistry.ProviderNaturalEarth));
            Assert.IsTrue(vm.AvailableBasemaps.Any(b => b.Id == ImageryRegistry.ProviderEsriImagery));
            Assert.IsTrue(vm.AvailableBasemaps.Any(b => b.Id == ImageryRegistry.ProviderEsriHybrid));
        }

        [TestMethod]
        public void MainViewModel_ActiveLayers_InitiallyContainsOnlyCustomOverlays()
        {
            var vm = new MainViewModel(new ProjectService(), new ZEarth.Globe.GlobeBridge());
            // Built-in basemaps must not be in ActiveLayers to prevent all-basemaps-off lockups
            Assert.AreEqual(0, vm.ActiveLayers.Count, "ActiveLayers should initially contain 0 items (only user-added overlays)");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GlobeBridge_ToggleHybridLabelsAsync_SendsCorrectRequest()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            string? capturedJson = null;
            bridge.SendJsonToWebView = json =>
            {
                capturedJson = json;
                return System.Threading.Tasks.Task.CompletedTask;
            };

            _ = bridge.ToggleHybridLabelsAsync(true);
            Assert.IsNotNull(capturedJson);
            Assert.IsTrue(capturedJson.Contains("layers.toggleHybridLabels"), "Request action must be layers.toggleHybridLabels");
            Assert.IsTrue(capturedJson.Contains("true"), "Request payload must contain show: true");
        }
    }
}
