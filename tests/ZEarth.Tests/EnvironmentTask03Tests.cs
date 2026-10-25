using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    /// <summary>
    /// Task 03 coverage: sunlight / atmosphere / time-of-day used to fall through to the JS
    /// unsupported-action responder, so the ribbon buttons did nothing on the globe. These tests
    /// pin the engine contract, the host dispatch payloads, the state-echo events, and the
    /// Sunlight → Time slider → Atmosphere ribbon order.
    /// </summary>
    [TestClass]
    public class EnvironmentTask03Tests
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

        [TestMethod]
        public void Engine_ImplementsEnvironmentContract()
        {
            var js = File.ReadAllText(Path.Combine(
                FindRepoRoot(), "src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            Assert.IsTrue(js.Contains("const EnvironmentManager"), "Engine must define the environment manager");
            Assert.IsTrue(js.Contains("case 'environment.setSunlight'"), "Engine must handle environment.setSunlight");
            Assert.IsTrue(js.Contains("case 'environment.setTimeOfDay'"), "Engine must handle environment.setTimeOfDay");
            Assert.IsTrue(js.Contains("case 'environment.setAtmosphere'"), "Engine must handle environment.setAtmosphere");
            Assert.IsTrue(js.Contains("globe.enableLighting"), "Sunlight must drive the day/night terminator");
            Assert.IsTrue(js.Contains("skyAtmosphere.show"), "Atmosphere must toggle the sky glow");
            Assert.IsTrue(js.Contains("showGroundAtmosphere"), "Atmosphere must toggle the ground haze");
            Assert.IsTrue(js.Contains("environment.sunlightChanged"), "Engine must echo sunlight state to the host");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task Bridge_DispatchesSunlightTimeAtmosphere_Payloads()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            Assert.IsTrue(await bridge.SetSunlightAsync(true, 18.0));
            Assert.IsTrue(await bridge.SetTimeOfDayAsync(6.5));
            Assert.IsTrue(await bridge.SetAtmosphereAsync(false));

            var sun = engine.LastPayloadFor("environment.setSunlight");
            using (var doc = JsonDocument.Parse(sun))
            {
                var p = doc.RootElement.GetProperty("payload");
                Assert.IsTrue(p.GetProperty("enabled").GetBoolean());
                Assert.AreEqual(18.0, p.GetProperty("timeHours").GetDouble(), 1e-9);
            }

            var tod = engine.LastPayloadFor("environment.setTimeOfDay");
            using (var doc = JsonDocument.Parse(tod))
            {
                Assert.AreEqual(6.5, doc.RootElement.GetProperty("payload").GetProperty("timeHours").GetDouble(), 1e-9);
            }

            var atm = engine.LastPayloadFor("environment.setAtmosphere");
            using (var doc = JsonDocument.Parse(atm))
            {
                Assert.IsFalse(doc.RootElement.GetProperty("payload").GetProperty("enabled").GetBoolean());
            }
        }

        [TestMethod]
        public void Bridge_AcceptsEngineStateEchoes()
        {
            var bridge = new GlobeBridge();
            bool? sunlight = null;
            double? time = null;
            bool? atmosphere = null;
            bridge.SunlightChanged += (_, v) => sunlight = v;
            bridge.TimeOfDayChanged += (_, v) => time = v;
            bridge.AtmosphereChanged += (_, v) => atmosphere = v;

            bridge.HandleWebMessage(JsonSerializer.Serialize(new { @event = "environment.sunlightChanged", enabled = true }));
            bridge.HandleWebMessage(JsonSerializer.Serialize(new { @event = "environment.timeChanged", timeHours = 21.25 }));
            bridge.HandleWebMessage(JsonSerializer.Serialize(new { @event = "environment.atmosphereChanged", enabled = false }));

            Assert.AreEqual(true, sunlight!.Value);
            Assert.AreEqual(21.25, time!.Value, 1e-9);
            Assert.AreEqual(false, atmosphere!.Value);
        }

        [TestMethod]
        public void Ribbon_OrdersSunlight_TimeSlider_Atmosphere()
        {
            var xaml = File.ReadAllText(Path.Combine(
                FindRepoRoot(), "src", "ZEarth.App", "Views", "MainWindow.xaml"));

            int sunlight = xaml.IndexOf("ToggleSunlightCommand", StringComparison.Ordinal);
            int slider = xaml.IndexOf("TimeOfDayHours, Mode=TwoWay", StringComparison.Ordinal);
            int atmosphere = xaml.IndexOf("ToggleAtmosphereCommand", StringComparison.Ordinal);

            Assert.IsTrue(sunlight >= 0 && slider > sunlight && atmosphere > slider,
                "Ribbon visual order must be Sunlight → Time slider → Atmosphere " +
                $"(sunlight@{sunlight}, slider@{slider}, atmosphere@{atmosphere})");
        }
    }
}
