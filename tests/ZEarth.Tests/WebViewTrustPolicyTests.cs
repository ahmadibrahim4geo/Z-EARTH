using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class WebViewTrustPolicyTests
{
    [TestMethod]
    [DataRow("https://cesium.local/index.html", true)]
    [DataRow("https://CESIUM.LOCAL/index.html#map", true)]
    [DataRow("https://cesium.local/", true)]
    [DataRow("https://cesium.local.evil.example/index.html", false)]
    [DataRow("https://evil.example/?redirect=cesium.local", false)]
    [DataRow("http://cesium.local/index.html", false)]
    [DataRow("https://cesium.local:444/index.html", false)]
    [DataRow("https://evil.example@cesium.local/index.html", false)]
    [DataRow("https://cesium.local/uploaded.html", false)]
    [DataRow("javascript:alert(1)", false)]
    [DataRow("file:///C:/Windows/win.ini", false)]
    [DataRow("", false)]
    public void OnlyPackagedCesiumDocumentCanPostCommands(string source, bool expected)
    {
        Assert.AreEqual(expected, WebViewTrustPolicy.IsTrustedGlobeDocument(source));
    }

    [TestMethod]
    public void NullSourceRejected()
        => Assert.IsFalse(WebViewTrustPolicy.IsTrustedGlobeDocument(null));
}
