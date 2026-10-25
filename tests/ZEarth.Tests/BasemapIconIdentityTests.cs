using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.ViewModels;

namespace ZEarth.Tests;

[TestClass]
public class BasemapIconIdentityTests
{
    [TestMethod]
    public void DifferentBasemapTypes_HaveDifferentMeaningfulGlyphs()
    {
        var street = new BasemapRibbonItem { Id = "osm-standard" };
        var ocean = new BasemapRibbonItem { Id = "esri-ocean" };
        var elevation = new BasemapRibbonItem { Id = "esri-topo" };
        var satellite = new BasemapRibbonItem { Id = "esri-imagery" };
        Assert.AreNotEqual(street.Glyph, ocean.Glyph);
        Assert.AreNotEqual(street.Glyph, elevation.Glyph);
        Assert.AreNotEqual(ocean.Glyph, satellite.Glyph);
        Assert.AreNotEqual(string.Empty, new BasemapRibbonItem { Id = "future-service" }.Glyph);
    }

    [TestMethod]
    public void DefaultIndicator_IsIndependentOfBasemapGlyph()
    {
        var defaultBase = new BasemapRibbonItem { Id = "offline-vector", IsDefault = true };
        var alternate = new BasemapRibbonItem { Id = "esri-ocean", IsDefault = false };
        Assert.IsTrue(defaultBase.IsDefault);
        Assert.IsFalse(alternate.IsDefault);
        Assert.AreNotEqual(defaultBase.Glyph, alternate.Glyph);
    }
}
