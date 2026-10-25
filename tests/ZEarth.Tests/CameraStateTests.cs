using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                Pitch = 10.0
            };

            camera.Sanitize();

            Assert.IsTrue(camera.IsValid());
            Assert.AreEqual(CameraState.DefaultLatitude, camera.Latitude);
            Assert.AreEqual(CameraState.DefaultLongitude, camera.Longitude);
            Assert.AreEqual(CameraState.MinAltitudeMeters, camera.Altitude);
            Assert.AreEqual(90.0, camera.Heading, 0.001);
            Assert.AreEqual(-5.0, camera.Pitch); // Clamped to horizon limit
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
            var original = new CameraState(30.0, 31.0, 500_000, 45.0, -60.0);
            var clone = original.Clone();

            Assert.AreEqual(original.Latitude, clone.Latitude);
            Assert.AreEqual(original.Longitude, clone.Longitude);
            Assert.AreEqual(original.Altitude, clone.Altitude);
            Assert.AreEqual(original.Heading, clone.Heading);
            Assert.AreEqual(original.Pitch, clone.Pitch);

            clone.Latitude = 10.0;
            Assert.AreNotEqual(original.Latitude, clone.Latitude);
        }
    }
}
