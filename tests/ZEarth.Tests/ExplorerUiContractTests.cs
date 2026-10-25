using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZEarth.Tests;

[TestClass]
public sealed class ExplorerUiContractTests
{
    private static string ReadRepoFile(string relative)
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(folder))
        {
            var candidate = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            var parent = Path.GetDirectoryName(folder);
            if (parent == folder) break;
            folder = parent ?? string.Empty;
        }
        throw new FileNotFoundException("Required Explorer contract source missing", relative);
    }

    [TestMethod]
    public void SearchHasExplicitClearButtonAndPendingOnlineResponseInvalidation()
    {
        var xaml = ReadRepoFile("src/ZEarth.App/Views/MainWindow.xaml");
        var vm = ReadRepoFile("src/ZEarth.App/ViewModels/MainViewModel.cs");
        Assert.IsTrue(xaml.Contains("Command=\"{Binding ClearSearchCommand}\""));
        Assert.IsTrue(vm.Contains("SearchResults.Clear()"));
        Assert.IsTrue(vm.Contains("SelectedSearchResult = null;"));
        Assert.IsTrue(vm.Contains("if (revision != _searchRevision) return;"));
        Assert.IsTrue(vm.Contains("SearchNameQuery = string.Empty;"));
    }

    [TestMethod]
    public void MyPlacesFillsPanelHasResizeSplitterAndWrapsAllCommands()
    {
        var xaml = XDocument.Parse(ReadRepoFile("src/ZEarth.App/Views/MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tree = xaml.Descendants().Single(e =>
            e.Name.LocalName == "TreeView" &&
            ((string?)e.Attribute("ItemsSource"))?.Contains("MyPlaces") == true);
        Assert.IsNull(tree.Attribute("MaxHeight"), "My Places must not be capped to 320px");
        Assert.AreEqual("1", (string?)tree.Attribute("Grid.Row"));
        var outerSplitter = xaml.Descendants().Single(e =>
            e.Name.LocalName == "GridSplitter" &&
            (string?)e.Attribute(x + "Name") != "WorkspaceSplitter");
        Assert.AreEqual("Rows", (string?)outerSplitter.Attribute("ResizeDirection"));
        Assert.IsTrue(xaml.Descendants().Any(e => e.Name.LocalName == "WrapPanel" &&
            e.Descendants().Any(c => ((string?)c.Attribute("Command"))?.Contains("ExportSelectedItemCommand") == true) &&
            e.Descendants().Any(c => ((string?)c.Attribute("Command"))?.Contains("DeletePlaceCommand") == true)));
        Assert.IsTrue(xaml.ToString().Contains("Command=\"{Binding FindMyLocationCommand}\""));
    }

    [TestMethod]
    public void WebViewLocationPermissionIsExplicitAndNotPersisted()
    {
        var host = ReadRepoFile("src/ZEarth.App/Views/MainWindow.xaml.cs");
        var bridge = ReadRepoFile("src/ZEarth.Globe/GlobeBridge.cs");
        var js = ReadRepoFile("src/ZEarth.Globe/Assets/cesium-engine.js");
        Assert.IsTrue(host.Contains("args.SavesInProfile = false"));
        Assert.IsTrue(host.Contains("CoreWebView2PermissionKind.Geolocation"));
        Assert.IsTrue(host.Contains("WebViewTrustPolicy.IsTrustedGlobeDocument(args.Uri)"));
        Assert.IsTrue(js.Contains("navigator.geolocation.getCurrentPosition"));
        Assert.IsTrue(js.Contains("maximumAge: 0"));
        Assert.IsTrue(bridge.Contains("device.requestLocation"));
        Assert.IsTrue(bridge.Contains("device.locationFix"));
    }
}
