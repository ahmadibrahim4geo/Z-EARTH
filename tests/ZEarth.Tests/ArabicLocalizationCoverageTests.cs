using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class ArabicLocalizationCoverageTests
{
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
}
