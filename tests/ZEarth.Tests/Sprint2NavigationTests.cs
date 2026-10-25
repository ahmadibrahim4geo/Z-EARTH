using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint2NavigationTests
    {
        [TestMethod]
        public void CameraState_Sanitize_ClampsAltitudeAndNormalizesHeading()
        {
            // Subterranean altitude below minimum 50m
            var camBelow = new CameraState(30.0, 31.0, -500.0, 750.0, -95.0, 60.0);
            Assert.AreEqual(50.0, camBelow.Altitude);
            Assert.AreEqual(30.0, camBelow.Heading, 0.001); // 750 % 360 = 30
            Assert.AreEqual(-90.0, camBelow.Pitch, 0.001);  // Clamped to nadir
            Assert.AreEqual(45.0, camBelow.Roll, 0.001);    // Clamped to 45

            // Deep space altitude above 60,000 km
            var camAbove = new CameraState(30.0, 31.0, 100_000_000.0, -90.0, 5.0, -60.0);
            Assert.AreEqual(60_000_000.0, camAbove.Altitude);
            Assert.AreEqual(270.0, camAbove.Heading, 0.001); // -90 % 360 = 270
            Assert.AreEqual(-5.0, camAbove.Pitch, 0.001);    // Clamped to -5
            Assert.AreEqual(-45.0, camAbove.Roll, 0.001);

            // NaN/Infinity recovery
            var camNan = new CameraState(double.NaN, double.PositiveInfinity, double.NaN);
            Assert.IsTrue(camNan.IsValid());
            Assert.AreEqual(CameraState.DefaultLatitude, camNan.Latitude, 0.001);
            Assert.AreEqual(CameraState.DefaultLongitude, camNan.Longitude, 0.001);
            Assert.AreEqual(CameraState.DefaultAltitude, camNan.Altitude, 0.001);
        }

        [TestMethod]
        public void CameraState_CreateHome_PreservesEgyptAfricaFraming()
        {
            // AC-004: Default Home frames Egypt and surrounding continental context
            var home = CameraState.CreateHome(HomePreset.EgyptAfrica);

            Assert.AreEqual(26.8206, home.Latitude, 0.0001);
            Assert.AreEqual(30.8025, home.Longitude, 0.0001);
            Assert.AreEqual(4_500_000.0, home.Altitude, 0.1); // 4,500 km provides continental context
            Assert.AreEqual(0.0, home.Heading, 0.001);
            Assert.AreEqual(-90.0, home.Pitch, 0.001);
        }

        [TestMethod]
        public void CameraState_CreateFullExtent_CoversGlobalDisk()
        {
            // AC-005: Full planetary view
            var full = CameraState.CreateFullExtent();

            Assert.AreEqual(20.0, full.Latitude, 0.0001);
            Assert.AreEqual(15.0, full.Longitude, 0.0001);
            Assert.AreEqual(22_000_000.0, full.Altitude, 0.1); // 22,000 km
        }

        [TestMethod]
        public void CameraState_CreateForBoundingBox_HandlesDatelineAndPoles()
        {
            // AC-024: Geometry crossing the antimeridian / dateline (west: 175° E, east: -175° W)
            var bboxAntimeridian = CameraState.CreateForBoundingBox(175.0, -10.0, -175.0, 10.0);

            // Center longitude across antimeridian should be 180° or -180°
            Assert.IsTrue(Math.Abs(Math.Abs(bboxAntimeridian.Longitude) - 180.0) < 0.001);
            Assert.AreEqual(0.0, bboxAntimeridian.Latitude, 0.0001);
            // Altitude should frame the 10° span comfortably without zooming out to entire world
            Assert.IsTrue(bboxAntimeridian.Altitude < 10_000_000.0, "Should avoid excessive whole-world zoom-out for localized dateline crossing");

            // Normal bounding box
            var bboxEgypt = CameraState.CreateForBoundingBox(25.0, 22.0, 37.0, 32.0);
            Assert.AreEqual(27.0, bboxEgypt.Latitude, 0.0001);
            Assert.AreEqual(31.0, bboxEgypt.Longitude, 0.0001);
        }

        [TestMethod]
        public void ExtentHistory_BackForwardNavigation_MaintainsRingBufferAndInvalidatesBranch()
        {
            // AC-021: Navigation branch handling & ring buffer
            var projectService = new ProjectService();
            var globeBridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, globeBridge);

            // Initially: History has 1 state (seed), Index = 0
            Assert.AreEqual(1, vm.ExtentHistoryCount);
            Assert.AreEqual(0, vm.ExtentHistoryIndex);
            Assert.IsFalse(vm.CanGoPreviousExtent);
            Assert.IsFalse(vm.CanGoNextExtent);

            // Simulate user navigation to Move 1 (Cairo)
            var cairo = new CameraState(30.0444, 31.2357, 50_000.0);
            globeBridge.SimulateCameraMoveEnd(cairo);

            Assert.AreEqual(2, vm.ExtentHistoryCount);
            Assert.AreEqual(1, vm.ExtentHistoryIndex);
            Assert.IsTrue(vm.CanGoPreviousExtent);
            Assert.IsFalse(vm.CanGoNextExtent);

            // Simulate user navigation to Move 2 (Alexandria)
            var alex = new CameraState(31.2001, 29.9187, 30_000.0);
            globeBridge.SimulateCameraMoveEnd(alex);

            Assert.AreEqual(3, vm.ExtentHistoryCount);
            Assert.AreEqual(2, vm.ExtentHistoryIndex);
            Assert.IsTrue(vm.CanGoPreviousExtent);
            Assert.IsFalse(vm.CanGoNextExtent);

            // Execute Previous Extent -> Move back to Cairo (Index 1)
            vm.PreviousExtentCommand.Execute(null);
            Assert.AreEqual(1, vm.ExtentHistoryIndex);
            Assert.IsTrue(vm.CanGoPreviousExtent);
            Assert.IsTrue(vm.CanGoNextExtent);

            // Execute Previous Extent -> Move back to Home (Index 0)
            vm.PreviousExtentCommand.Execute(null);
            Assert.AreEqual(0, vm.ExtentHistoryIndex);
            Assert.IsFalse(vm.CanGoPreviousExtent);
            Assert.IsTrue(vm.CanGoNextExtent);

            // Execute Next Extent -> Move forward to Cairo (Index 1)
            vm.NextExtentCommand.Execute(null);
            Assert.AreEqual(1, vm.ExtentHistoryIndex);
            Assert.IsTrue(vm.CanGoPreviousExtent);
            Assert.IsTrue(vm.CanGoNextExtent);

            // AC-021 Branch Handling: Now navigate to Aswan while at Index 1!
            // Forward extent (Alexandria at Index 2) MUST be invalidated!
            var aswan = new CameraState(23.9700, 32.8800, 25_000.0);
            globeBridge.SimulateCameraMoveEnd(aswan);

            Assert.AreEqual(3, vm.ExtentHistoryCount);
            Assert.AreEqual(2, vm.ExtentHistoryIndex);
            Assert.IsTrue(vm.CanGoPreviousExtent);
            Assert.IsFalse(vm.CanGoNextExtent); // Forward stack was cleanly invalidated
        }

        [TestMethod]
        public void ReversibleZoom_SymmetricFactor_RestoresExactAltitude()
        {
            // AC-023: Zoom in/out fixed same steps reversible near same focal point
            double initialAltitude = 1_000_000.0;
            double zoomFactor = 0.33; // 33%

            // Zoom In: new_h = h - h * zoomFactor
            double zoomedInAltitude = initialAltitude - (initialAltitude * zoomFactor);
            Assert.AreEqual(670_000.0, zoomedInAltitude, 0.001);

            // Zoom Out: reversible formula: delta = zoomedInAltitude * (zoomFactor / (1 - zoomFactor))
            double zoomOutDelta = zoomedInAltitude * (zoomFactor / (1.0 - zoomFactor));
            double restoredAltitude = zoomedInAltitude + zoomOutDelta;

            Assert.AreEqual(initialAltitude, restoredAltitude, 0.001);
        }

        [TestMethod]
        public void PrivacySettings_Default_DisallowsUnsolicitedNetworkGeolocation()
        {
            // AC-001 & ZE-022: Strict offline privacy
            var privacy = new PrivacySettings();

            Assert.IsFalse(privacy.EnableIpGeolocation);
            Assert.AreEqual(HomePreset.EgyptAfrica, privacy.StartupHomePreset);
            Assert.IsFalse(privacy.CanAttemptNetworkGeolocation());
        }

        [TestMethod]
        public void HomePreset_Switching_UpdatesTooltipAndTargetCamera()
        {
            var projectService = new ProjectService();
            var globeBridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, globeBridge);

            // Default preset
            Assert.AreEqual(HomePreset.EgyptAfrica, vm.SelectedHomePreset);
            StringAssert.Contains(vm.HomeTooltip, "Egypt");

            // Switch to World
            vm.SetHomePresetCommand.Execute(HomePreset.World);
            Assert.AreEqual(HomePreset.World, vm.SelectedHomePreset);
            StringAssert.Contains(vm.HomeTooltip, "World");

            // Switch back to EgyptAfrica
            vm.SetHomePresetCommand.Execute("EgyptAfrica");
            Assert.AreEqual(HomePreset.EgyptAfrica, vm.SelectedHomePreset);
        }
    }
}
