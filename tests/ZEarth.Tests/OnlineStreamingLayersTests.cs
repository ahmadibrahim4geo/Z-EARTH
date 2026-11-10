using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain.Layers;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class OnlineStreamingLayersTests
    {
        [TestMethod]
        public void ReferenceCatalog_ContainsWeatherStationsAndWorldGazetteer()
        {
            var catalog = new ReferenceLayerCatalog();
            var weatherLayer = catalog.Layers.FirstOrDefault(l => l.Id == "ref_weather_stations");
            Assert.IsNotNull(weatherLayer, "ref_weather_stations should exist in catalog.");
            Assert.AreEqual("WeatherStationsGeoJson", weatherLayer.LayerType);
            Assert.AreEqual(ReferenceLayerCategory.Weather, weatherLayer.Category);
            Assert.IsTrue(weatherLayer.Url.Contains("aviationweather.gov"));

            var gazetteerLayer = catalog.Layers.FirstOrDefault(l => l.Id == "ref_world_gazetteer");
            Assert.IsNotNull(gazetteerLayer, "ref_world_gazetteer should exist in catalog.");
            Assert.AreEqual("WorldGazetteer", gazetteerLayer.LayerType);
            Assert.AreEqual(ReferenceLayerCategory.PlacesAndNames, gazetteerLayer.Category);
        }

        [TestMethod]
        public void ReferenceCatalog_PreservesNineStandardCategories()
        {
            var catalog = new ReferenceLayerCatalog();
            var groups = catalog.GetGroupedCategories();
            Assert.AreEqual(9, groups.Count);
            Assert.IsTrue(groups.Any(g => g.Category == ReferenceLayerCategory.Weather));
            Assert.IsTrue(groups.Any(g => g.Category == ReferenceLayerCategory.PlacesAndNames));
        }

        [TestMethod]
        public void UserLayersFolder_CreatedAndScanned()
        {
            var vm = new MainViewModel(new ProjectService(), new GlobeBridge());
            Assert.IsNotNull(vm.UserLayersCategory);
            Assert.IsTrue(vm.IsLayersExpanded);
            Assert.IsTrue(vm.IsLayersVisible);
            Assert.IsFalse(vm.IsLayersMaximized);

            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ZEarth", "Layers");
            Assert.IsTrue(Directory.Exists(folder), $"Directory '{folder}' should have been created.");
        }

        [TestMethod]
        public void LayersFilter_NarrowsCategoriesAndPreservesMatches()
        {
            var vm = new MainViewModel(new ProjectService(), new GlobeBridge());
            vm.LayersFilterText = "Weather";
            Assert.IsTrue(vm.FilteredLayerCategories.Count > 0);
            Assert.IsTrue(vm.FilteredLayerCategories.Any(g => g.Layers.Any(l => l.Name.Contains("Weather", StringComparison.OrdinalIgnoreCase))));

            vm.LayersFilterText = string.Empty;
            Assert.IsTrue(vm.FilteredLayerCategories.Count >= 9);
        }

        [TestMethod]
        public async Task SetWeatherMetricCommand_ExecutesCleanly()
        {
            var vm = new MainViewModel(new ProjectService(), new GlobeBridge());
            Assert.IsNotNull(vm.SetWeatherMetricCommand);
            Assert.IsTrue(vm.SetWeatherMetricCommand.CanExecute("humidity"));

            vm.SetWeatherMetricCommand.Execute("humidity");
            await Task.Delay(50);
            Assert.IsTrue(vm.StatusMessage.Contains("Humidity") || vm.StatusMessage.Contains("الرطوبة"));
        }
    }
}
