using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain.Kml;
using ZEarth.Globe;

namespace ZEarth.Tests;

[TestClass]
public sealed class FeaturePropertiesAndEnvironmentTests
{
    [TestMethod]
    public void HtmlPreviewPreservesTableButDropsScriptsAttributesAndExternalResources()
    {
        var raw = "<table style='color:white'><tr><th>الاسم</th><td onclick='bad()'>A &amp; B</td></tr></table><script>alert(1)</script><img src='file:///private'><a href='https://example.com'>link</a>";
        var safe = HtmlSanitizer.SanitizeForPreview(raw);
        StringAssert.Contains(safe, "<table><tr><th>الاسم</th><td>A &amp; B</td></tr></table>");
        Assert.IsFalse(safe.Contains("script", System.StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("onclick", System.StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("file:", System.StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("href", System.StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(safe, "link");
    }

    [TestMethod]
    public void AtmosphereBridgeEchoNotifiesAllButtonColorsOnBothTransitions()
    {
        var bridge = new GlobeBridge();
        var vm = new MainViewModel(new ProjectService(), bridge);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        bridge.HandleWebMessage("{\"event\":\"environment.atmosphereChanged\",\"enabled\":false}");
        Assert.IsFalse(vm.IsAtmosphereEnabled);
        foreach (var name in new[] { "AtmosphereButtonBackground", "AtmosphereButtonBorderBrush", "AtmosphereLabelForeground", "AtmosphereIconBrush" })
            CollectionAssert.Contains(notifications, name);
        notifications.Clear();
        bridge.HandleWebMessage("{\"event\":\"environment.atmosphereChanged\",\"enabled\":true}");
        Assert.IsTrue(vm.IsAtmosphereEnabled);
        CollectionAssert.Contains(notifications, "AtmosphereButtonBackground");
    }
}
