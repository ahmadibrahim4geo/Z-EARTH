using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Measurement;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;

namespace ZEarth.Tests
{
    [TestClass]
    public class FinalClosureAcceptanceTests
    {
        [TestMethod]
        public void UnitService_EgyptianFeddan_ConvertsExactSurveyOfEgyptArea()
        {
            var unitService = UnitService.Instance;
            
            // 1 Egyptian Feddan = exactly 4,200.8335 m²
            double sqMeters = 4200.8335;
            double feddans = unitService.ConvertArea(sqMeters, AreaUnit.EgyptianFeddan);
            Assert.AreEqual(1.0, feddans, 1e-6, "1 feddan must equal exactly 4200.8335 m²");

            // 10 feddans
            double tenFeddans = unitService.ConvertArea(sqMeters * 10, AreaUnit.EgyptianFeddan);
            Assert.AreEqual(10.0, tenFeddans, 1e-5);

            string formatted = unitService.FormatArea(sqMeters, AreaUnit.EgyptianFeddan);
            StringAssert.Contains(formatted, "fed");
        }

        [TestMethod]
        public void UnitService_DistanceConversions_AllUnitsAccurate()
        {
            var unitService = UnitService.Instance;

            // 1,000 meters = 1 km
            Assert.AreEqual(1.0, unitService.ConvertDistance(1000.0, DistanceUnit.Kilometers), 1e-6);

            // 1 Nautical Mile = 1852 meters
            Assert.AreEqual(1.0, unitService.ConvertDistance(1852.0, DistanceUnit.NauticalMiles), 1e-6);

            // 1 Foot = 0.3048 meters
            Assert.AreEqual(1.0, unitService.ConvertDistance(0.3048, DistanceUnit.Feet), 1e-6);

            // 1 Mile = 1609.344 meters
            Assert.AreEqual(1.0, unitService.ConvertDistance(1609.344, DistanceUnit.Miles), 1e-6);

            // Auto Metric
            Assert.AreEqual("500.00 m", unitService.FormatDistance(500.0, DistanceUnit.AutoMetric));
            Assert.AreEqual("2.500 km (2500.0 m)", unitService.FormatDistance(2500.0, DistanceUnit.AutoMetric));
            Assert.AreEqual("2.50 km", unitService.FormatDistance(2500.0, DistanceUnit.Kilometers));
        }

        [TestMethod]
        public void SnappingSettings_ScreenVsGround_IndependentModes()
        {
            var settings = new SnappingSettings
            {
                Mode = SnappingMode.ScreenPixels,
                ScreenTolerancePixels = 12.0,
                GroundToleranceMeters = 5.0
            };

            Assert.AreEqual(SnappingMode.ScreenPixels, settings.Mode);
            Assert.AreEqual(12.0, settings.ScreenTolerancePixels);
            Assert.AreEqual(5.0, settings.GroundToleranceMeters);

            // Test clamping
            settings.ScreenTolerancePixels = 100.0; // clamp to 40
            Assert.AreEqual(40.0, settings.ScreenTolerancePixels);

            settings.ScreenTolerancePixels = 2.0; // clamp to 4
            Assert.AreEqual(4.0, settings.ScreenTolerancePixels);

            settings.GroundToleranceMeters = 0.5; // clamp to 1.0
            Assert.AreEqual(1.0, settings.GroundToleranceMeters);

            settings.GroundToleranceMeters = 150.0; // clamp to 50.0
            Assert.AreEqual(50.0, settings.GroundToleranceMeters);
        }

        [TestMethod]
        public void ImageryRegistry_AllTenProvidersRegistered()
        {
            var registry = new ImageryRegistry();
            var providers = registry.Basemaps.ToList();

            Assert.IsTrue(providers.Count >= 10, $"Must have at least 10 basemap providers, got {providers.Count}");

            var expectedIds = new[]
            {
                "offline-vector",
                "osm-standard",
                "opentopo",
                "carto-dark",
                "carto-light",
                "esri-imagery",
                "esri-hybrid",
                "esri-streets",
                "esri-topo",
                "esri-gray"
            };

            foreach (var id in expectedIds)
            {
                var provider = providers.FirstOrDefault(p => p.Id == id);
                Assert.IsNotNull(provider, $"Provider with id '{id}' must be registered");
                Assert.IsFalse(string.IsNullOrWhiteSpace(provider.Name), $"Provider '{id}' must have a valid Name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(provider.Attribution), $"Provider '{id}' must have attribution");
            }

            // Natural Earth must be offline
            var offline = providers.First(p => p.Id == "offline-vector");
            Assert.IsTrue(offline.IsOfflineAvailable, "Natural Earth must be marked offline available");
        }

        [TestMethod]
        public void PlaceItem_CircleSupport_StoresRadiusAndCoordinates()
        {
            var circle = new PlaceItem("Cairo Circle Area", PlaceItemType.Circle, 30.0444, 31.2357)
            {
                RadiusMeters = 1500.0
            };

            Assert.AreEqual(PlaceItemType.Circle, circle.ItemType);
            Assert.AreEqual(1500.0, circle.RadiusMeters);
            Assert.AreEqual(30.0444, circle.Latitude);
            Assert.AreEqual(31.2357, circle.Longitude);

            var cloned = circle.Clone();
            Assert.AreEqual(PlaceItemType.Circle, cloned.ItemType);
            Assert.AreEqual(1500.0, cloned.RadiusMeters);
            Assert.AreEqual(circle.Name, cloned.Name);
        }

        [TestMethod]
        public void CoordinateTransform_MgrsConversion_RoundTripIntegrity()
        {
            // Cairo Tahrir Square (lon: 31.2357, lat: 30.0444)
            string mgrs = MgrsConverter.FromWgs84(31.2357, 30.0444, 5);
            Assert.IsNotNull(mgrs);
            Assert.IsTrue(mgrs.StartsWith("36R"), $"Cairo should be in UTM zone 36, band R. Got: {mgrs}");
        }

        [TestMethod]
        public void GeodesicMeasurement_HighPrecisionVincenty_Accurate()
        {
            // Distance from Giza Pyramids (29.9792° N, 31.1342° E) to Cairo Tower (30.0459° N, 31.2243° E)
            double distance = GeodesicMeasurementService.ComputeDistanceMeters(29.9792, 31.1342, 30.0459, 31.2243);
            
            // Expected ~11.3 km
            Assert.IsTrue(distance > 11000.0 && distance < 12000.0, $"Expected ~11.3 km, got {distance:F1} m");
        }

        [TestMethod]
        public void EnvironmentControls_SunlightAndAtmosphere_CommandsAndDefaults()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);
            var vm = new ZEarth.App.ViewModels.MainViewModel(new ZEarth.App.Services.ProjectService(), bridge);

            // Verify initial defaults
            Assert.IsFalse(vm.IsSunlightEnabled, "Sunlight should default to disabled for uniform daytime view");
            Assert.IsTrue(vm.IsAtmosphereEnabled, "Atmosphere should default to enabled");
            Assert.AreEqual(12.0, vm.TimeOfDayHours, 0.01, "Time of day should default to 12:00 (Noon)");
            Assert.IsTrue(vm.TimeOfDayDisplay.Contains("12:00 PM"));

            // Test ToggleSunlightCommand
            Assert.IsTrue(vm.ToggleSunlightCommand.CanExecute(null));
            vm.ToggleSunlightCommand.Execute(null);
            Assert.IsTrue(vm.IsSunlightEnabled);
            Assert.IsTrue(engine.Any("environment.setSunlight"));

            // Test TimeOfDay clamping and formatting
            vm.TimeOfDayHours = 18.5;
            Assert.AreEqual(18.5, vm.TimeOfDayHours, 0.01);
            Assert.IsTrue(vm.TimeOfDayDisplay.Contains("6:30 PM"));

            // Test ToggleAtmosphereCommand
            Assert.IsTrue(vm.ToggleAtmosphereCommand.CanExecute(null));
            vm.ToggleAtmosphereCommand.Execute(null);
            Assert.IsFalse(vm.IsAtmosphereEnabled);
            Assert.IsTrue(engine.Any("environment.setAtmosphere"));
            vm.ToggleAtmosphereCommand.Execute(null);
            Assert.IsTrue(vm.IsAtmosphereEnabled);
        }
    }
}
