using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;

namespace ZEarth.Tests
{
    [TestClass]
    public class BridgeMessagesTests
    {
        [TestMethod]
        public void BridgeRequest_SerializesWithSchemaVersionAndRequestId()
        {
            var req = new BridgeRequest
            {
                Action = "camera.flyTo",
                Payload = new FlyToPayload
                {
                    Latitude = 30.0,
                    Longitude = 31.2,
                    Altitude = 100_000,
                    Heading = 15.0,
                    Pitch = -75.0,
                    DurationSeconds = 2.0
                }
            };

            var json = JsonSerializer.Serialize(req);
            Assert.IsTrue(json.Contains("\"schemaVersion\":\"1.0.0\""));
            Assert.IsTrue(json.Contains("\"action\":\"camera.flyTo\""));
            Assert.IsTrue(json.Contains("\"requestId\":"));
            Assert.IsTrue(json.Contains("\"latitude\":30"));

            var deserialized = JsonSerializer.Deserialize<BridgeRequest>(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("1.0.0", deserialized.SchemaVersion);
            Assert.AreEqual("camera.flyTo", deserialized.Action);
            Assert.AreEqual(req.RequestId, deserialized.RequestId);
        }

        [TestMethod]
        public void BridgeResponse_SerializesSuccessAndPayload()
        {
            var resp = new BridgeResponse
            {
                RequestId = "test-12345",
                Action = "camera.home",
                Success = true,
                Payload = new { arrived = true }
            };

            var json = JsonSerializer.Serialize(resp);
            Assert.IsTrue(json.Contains("\"success\":true"));
            Assert.IsTrue(json.Contains("\"requestId\":\"test-12345\""));

            var deserialized = JsonSerializer.Deserialize<BridgeResponse>(json);
            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.Success);
            Assert.AreEqual("test-12345", deserialized.RequestId);
        }

        [TestMethod]
        public void CameraChangedNotification_DeserializesAccurately()
        {
            var json = "{\"schemaVersion\":\"1.0.0\",\"event\":\"camera.changed\",\"camera\":{\"latitude\":29.9792,\"longitude\":31.1342,\"altitude\":1500,\"heading\":0,\"pitch\":-90,\"roll\":0}}";

            var notification = JsonSerializer.Deserialize<CameraChangedNotification>(json);
            Assert.IsNotNull(notification);
            Assert.AreEqual("camera.changed", notification.Event);
            Assert.AreEqual(29.9792, notification.Camera.Latitude, 0.0001);
            Assert.AreEqual(31.1342, notification.Camera.Longitude, 0.0001);
            Assert.AreEqual(1500, notification.Camera.Altitude, 0.1);
        }

        [TestMethod]
        public void GlobeBridge_HandlesEngineReady_DirectAndQuotedJson()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            string? receivedDirect = null;
            string? receivedQuoted = null;

            bridge.EngineReady += (s, msg) => receivedDirect = msg;
            bridge.HandleWebMessage("{\"event\":\"engine.ready\",\"version\":\"1.146.0\"}");
            Assert.IsNotNull(receivedDirect);
            Assert.IsTrue(receivedDirect.Contains("1.146.0"));

            // Test quoted string format from WebView2 postMessage stringification
            bridge.EngineReady += (s, msg) => receivedQuoted = msg;
            bridge.HandleWebMessage("\"{\\\"event\\\":\\\"engine.ready\\\",\\\"version\\\":\\\"1.146.0\\\"}\"");
            Assert.IsNotNull(receivedQuoted);
            Assert.IsTrue(receivedQuoted.Contains("1.146.0"));
        }

        [TestMethod]
        public void GlobeBridge_HandlesSceneModeChanged_AndCameraUpdates()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            ZEarth.Domain.SceneMode? modeReceived = null;
            ZEarth.Domain.CameraState? camReceived = null;

            bridge.SceneModeChanged += (s, m) => modeReceived = m;
            bridge.CameraChanged += (s, c) => camReceived = c;

            bridge.HandleWebMessage("{\"event\":\"scene.modeChanged\",\"mode\":\"Scene2D\"}");
            Assert.AreEqual(ZEarth.Domain.SceneMode.Scene2D, modeReceived);
            Assert.AreEqual(ZEarth.Domain.SceneMode.Scene2D, bridge.CurrentMode);

            bridge.HandleWebMessage("{\"event\":\"camera.changed\",\"camera\":{\"latitude\":26.8206,\"longitude\":30.8025,\"altitude\":4500000,\"heading\":0,\"pitch\":-90,\"roll\":0}}");
            Assert.IsNotNull(camReceived);
            Assert.AreEqual(26.8206, camReceived.Latitude, 0.0001);
            Assert.AreEqual(30.8025, camReceived.Longitude, 0.0001);
            Assert.AreEqual(4500000, camReceived.Altitude, 1.0);
        }

        [TestMethod]
        public void GlobeBridge_HandlesWebError_Notification()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            string? errorReceived = null;

            bridge.WebError += (s, msg) => errorReceived = msg;
            bridge.HandleWebMessage("{\"event\":\"imagery.error\",\"message\":\"Network unreachable\",\"timesRetried\":3}");
            Assert.IsNotNull(errorReceived);
            Assert.IsTrue(errorReceived.Contains("Network unreachable"));
        }

        [TestMethod]
        public void GlobeBridge_TerrainChanged_ReportsRealNumericalProbe()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            string? last = null;
            bridge.TerrainChanged += (sender, data) => last = data;
            bridge.HandleWebMessage("{\"event\":\"terrain.changed\",\"type\":\"globalDem\",\"probe\":{\"level\":8,\"x\":149,\"y\":109,\"sampleCount\":4225,\"minimumElevationM\":0,\"maximumElevationM\":200}}");
            Assert.IsNotNull(last);
            StringAssert.Contains(last, "\"sampleCount\":4225");
            StringAssert.Contains(last, "\"maximumElevationM\":200");
        }

        [TestMethod]
        public void GlobeBridge_HandlesSunlightAndAtmosphereMessages()
        {
            var bridge = new ZEarth.Globe.GlobeBridge();
            bool? sunlightReceived = null;
            bool? atmosphereReceived = null;
            double? timeReceived = null;

            bridge.SunlightChanged += (s, enabled) => sunlightReceived = enabled;
            bridge.AtmosphereChanged += (s, enabled) => atmosphereReceived = enabled;
            bridge.TimeOfDayChanged += (s, time) => timeReceived = time;

            bridge.HandleWebMessage("{\"event\":\"environment.sunlightChanged\",\"enabled\":true}");
            Assert.AreEqual(true, sunlightReceived);

            bridge.HandleWebMessage("{\"event\":\"environment.atmosphereChanged\",\"enabled\":false}");
            Assert.AreEqual(false, atmosphereReceived);

            bridge.HandleWebMessage("{\"event\":\"environment.timeChanged\",\"timeHours\":15.5}");
            Assert.IsNotNull(timeReceived);
            Assert.AreEqual(15.5, timeReceived.Value, 0.01);
        }
    }
}
