using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.ViewModels;
using ZEarth.Domain;

namespace ZEarth.Tests
{
    [TestClass]
    public class CameraStateTests
    {
        [TestMethod]
        public void CreateHome_HasValidRegionalBounds()
        {
            var home = CameraState.CreateHome();

            Assert.IsTrue(home.IsValid());
            Assert.AreEqual(26.8206, home.Latitude, 0.001);
            Assert.AreEqual(30.8025, home.Longitude, 0.001);
            Assert.AreEqual(4_500_000.0, home.Altitude, 1.0);
            Assert.AreEqual(0.0, home.Heading);
            Assert.AreEqual(-90.0, home.Pitch);
            Assert.AreEqual(3.0, home.ZoomLevel);
        }

        [TestMethod]
        public void Sanitize_ClampsExtremesAndNaNs()
        {
            var camera = new CameraState
            {
                Latitude = double.NaN,
                Longitude = double.PositiveInfinity,
                Altitude = -500.0,
                Heading = 450.0,
                Pitch = 10.0,
                ZoomLevel = double.NaN
            };

            camera.Sanitize();

            Assert.IsTrue(camera.IsValid());
            Assert.AreEqual(CameraState.DefaultLatitude, camera.Latitude);
            Assert.AreEqual(CameraState.DefaultLongitude, camera.Longitude);
            Assert.AreEqual(CameraState.MinAltitudeMeters, camera.Altitude);
            Assert.AreEqual(90.0, camera.Heading, 0.001);
            Assert.AreEqual(-5.0, camera.Pitch); // Clamped to horizon limit
            Assert.AreEqual(3.0, camera.ZoomLevel);
        }

        [TestMethod]
        public void Sanitize_WrapsLongitudes()
        {
            var camera = new CameraState(0, 195.0, 1_000_000);
            Assert.AreEqual(-165.0, camera.Longitude, 0.001);

            var camera2 = new CameraState(0, -200.0, 1_000_000);
            Assert.AreEqual(160.0, camera2.Longitude, 0.001);
        }

        [TestMethod]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new CameraState(30.0, 31.0, 500_000, 45.0, -60.0, 0.0, 8.5);
            var clone = original.Clone();

            Assert.AreEqual(original.Latitude, clone.Latitude);
            Assert.AreEqual(original.Longitude, clone.Longitude);
            Assert.AreEqual(original.Altitude, clone.Altitude);
            Assert.AreEqual(original.Heading, clone.Heading);
            Assert.AreEqual(original.Pitch, clone.Pitch);
            Assert.AreEqual(original.ZoomLevel, clone.ZoomLevel);

            clone.Latitude = 10.0;
            Assert.AreNotEqual(original.Latitude, clone.Latitude);
        }

        [TestMethod]
        public void FormatEyeAltitude_PreservesPrecisionAcrossRanges()
        {
            // Close up < 100m
            Assert.AreEqual("45.2 m", MainViewModel.FormatEyeAltitude(45.2));

            // Under 1,000m
            Assert.AreEqual("850 m", MainViewModel.FormatEyeAltitude(850.0));

            // Between 1km and 10km (dual precision: km with 2 decimals + meters)
            Assert.AreEqual("1.45 km (1,450 m)", MainViewModel.FormatEyeAltitude(1450.0));
            Assert.AreEqual("3.20 km (3,200 m)", MainViewModel.FormatEyeAltitude(3200.0));

            // Between 10km and 100km (1 decimal km)
            Assert.AreEqual("24.5 km", MainViewModel.FormatEyeAltitude(24500.0));

            // Over 100km
            Assert.AreEqual("450 km", MainViewModel.FormatEyeAltitude(450000.0));
            Assert.AreEqual("4,500 km", MainViewModel.FormatEyeAltitude(4500000.0));
        }

        [TestMethod]
        public void FormatZoomLevel_FormatsProperly()
        {
            Assert.AreEqual("Z16.2", MainViewModel.FormatZoomLevel(16.2));
            Assert.AreEqual("Z3.0", MainViewModel.FormatZoomLevel(3.0));
            Assert.AreEqual("Z0", MainViewModel.FormatZoomLevel(0.0));
            Assert.AreEqual("Z0", MainViewModel.FormatZoomLevel(double.NaN));
        }
    }
}
