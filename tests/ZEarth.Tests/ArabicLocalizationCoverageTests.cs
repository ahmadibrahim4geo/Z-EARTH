using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class ArabicLocalizationCoverageTests
{
    [TestCleanup]
    public void Cleanup()
    {
        LocalizationService.Instance.SetLanguage("en");
    }

    [TestMethod]
    public void MainRibbonAndExplorerLabels_HaveArabicTranslations()
    {
        var service = LocalizationService.Instance;
        foreach (var label in new[]
        {
            "Project", "Navigation", "View", "Drawing", "Measurement", "Basemap",
            "Historical Imagery", "Terrain", "Tools", "Help", "Project File",
            "Camera Controls", "Coordinate Grid", "Lighting & Atmosphere", "3D Model",
            "Geodesic Tools", "Basemap Gallery", "Elevation Model", "EXPLORER",
            "Search", "By Name", "By Coordinates", "My Places", "Working…"
        })
        {
            Assert.IsTrue(service.HasArabicTranslation(label), $"Missing Arabic UI translation: {label}");
        }
    }

    [TestMethod]
    public void LiteralTranslation_PreservesWhitespaceAndRestoresEnglish()
    {
        var service = LocalizationService.Instance;
        service.SetLanguage("ar");
        Assert.AreEqual("  المشروع  ", service.TranslateLiteral("  Project  "));
        service.SetLanguage("en");
        Assert.AreEqual("  Project  ", service.TranslateLiteral("  Project  "));
    }

    [TestMethod]
    public void EnglishMode_ContainsZeroArabicCharacters()
    {
        var service = LocalizationService.Instance;
        service.SetLanguage("en");

        var testKeys = new[]
        {
            "App.Title", "Tab.Project", "Tab.View", "Tab.Drawing", "Tab.Measurement",
            "Tab.Layers", "Tab.Terrain", "Tab.Tools", "Tab.Help",
            "Nav.Home", "Nav.FullExtent", "Nav.ZoomIn", "Nav.ZoomOut", "Nav.NorthUp", "Nav.ResetPitch",
            "Nav.3DGlobe", "Nav.2DMap", "Places.MyPlaces", "Places.Temporary", "Places.SavedScenes",
            "Places.AddPlacemark", "Places.AddFolder", "Places.AddPath", "Places.AddPolygon", "Places.Delete",
            "Measure.Distance", "Measure.Area", "Measure.Clear", "Measure.Snapping",
            "Layers.Basemap", "Layers.Offline", "Layers.OSM", "Layers.OpenTopo", "Layers.Dark", "Layers.AddLayer",
            "Terrain.Model", "Terrain.Exaggeration", "Terrain.Reset",
            "Telemetry.Coordinates", "Telemetry.Altitude", "Telemetry.Heading", "Telemetry.CRS"
        };

        var arabicRegex = new Regex(@"[\u0600-\u06FF]");

        foreach (var key in testKeys)
        {
            var enText = service.GetString(key);
            Assert.IsFalse(arabicRegex.IsMatch(enText),
                $"English string for key '{key}' contains Arabic characters: '{enText}'");
        }
    }

    [TestMethod]
    public void ArabicMode_PlacesAndMeasurements_HaveNoBilingualMashups()
    {
        var service = LocalizationService.Instance;
        service.SetLanguage("ar");

        Assert.AreEqual("📁 أماكني", service.GetString("Places.MyPlaces"));
        Assert.AreEqual("📁 أماكن مؤقتة", service.GetString("Places.Temporary"));
        Assert.AreEqual("🎬 المشاهد المحفوظة", service.GetString("Places.SavedScenes"));
        Assert.AreEqual("تفعيل المحاذاة والالتقاط", service.GetString("Measure.Snapping"));
        Assert.AreEqual("بيانات متجهة غير متصلة", service.GetString("Layers.Offline"));
        Assert.AreEqual("خريطة كارتو الداكنة", service.GetString("Layers.Dark"));

        // No bilingual slashes
        var slashBilingual = new Regex(@"[\u0600-\u06FF]+\s*/\s*[A-Za-z]+");
        foreach (var key in new[] { "Places.MyPlaces", "Places.Temporary", "Places.SavedScenes", "Measure.Snapping", "Layers.Offline", "Layers.Dark" })
        {
            var arText = service.GetString(key);
            Assert.IsFalse(slashBilingual.IsMatch(arText), $"Arabic string '{arText}' contains bilingual slash");
        }
    }
}
