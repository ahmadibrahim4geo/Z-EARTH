using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests;

[TestClass]
public class ExplorerSessionWorkflowTests
{
    private static (MainViewModel Vm, GlobeBridge Bridge, TemporarySessionService Session, string Directory) Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ZEarth.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var session = new TemporarySessionService(Path.Combine(directory, "session.json"));
        var bridge = new GlobeBridge();
        return (new MainViewModel(new ProjectService(), bridge, session), bridge, session, directory);
    }

    [TestMethod]
    public void ImportKmlIsExplorerRootUntilYesStoresItAndReloadsWithHierarchy()
    {
        var (vm, _, session, directory) = Create();
        var kml = Path.Combine(directory, "input.kml");
        File.WriteAllText(kml, "<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document><name>Original</name><Folder><name>Nested</name><Placemark><name>P</name><Point><coordinates>31,30,42</coordinates></Point></Placemark></Folder></Document></kml>");
        vm.ExecuteImportKml(kml);
        var imported = vm.MyPlaces.Single(p => p.Name == "input.kml");
        Assert.IsFalse(vm.TemporaryPlaces.Contains(imported));
        Assert.IsTrue(imported.Children.Count > 0);
        Assert.IsTrue(vm.ResolvePendingPlacesOnClose(MessageBoxResult.Cancel) == false);
        Assert.IsTrue(vm.MyPlaces.Contains(imported));
        Assert.IsTrue(vm.ResolvePendingPlacesOnClose(MessageBoxResult.Yes));
        Assert.IsFalse(vm.MyPlaces.Contains(imported));
        Assert.IsTrue(vm.TemporaryPlaces.Contains(imported));
        var reloaded = session.LoadSession().Single(p => p.Name == "input.kml");
        Assert.AreEqual(imported.Children.Count, reloaded.Children.Count);
        Assert.IsTrue(File.ReadAllText(kml).Contains("Original"), "Original file must remain unchanged.");
    }

    [TestMethod]
    public void DrawIsRootEvenWithFolderSelectedAndCancelShutdownCanRestoreDiscard()
    {
        var (vm, bridge, _, _) = Create();
        var folder = new PlaceItem("Destination", PlaceItemType.Folder);
        vm.MyPlaces.Add(folder);
        vm.SelectedPlace = folder;
        bridge.HandleWebMessage(JsonSerializer.Serialize(new { @event = "draw.completed", geometryType = "path", coordinates = new[] { new { longitude = 31, latitude = 30 }, new { longitude = 32, latitude = 31 } } }));
        var drawn = vm.MyPlaces.Single(p => p.ItemType == PlaceItemType.Path);
        Assert.AreEqual(0, folder.Children.Count);
        Assert.IsTrue(vm.ResolvePendingPlacesOnClose(MessageBoxResult.No));
        Assert.IsFalse(vm.MyPlaces.Contains(drawn));
        vm.RestoreDiscardedPendingOnClose();
        Assert.IsTrue(vm.MyPlaces.Contains(drawn));
    }

    [TestMethod]
    public void FailedSessionWriteKeepsPendingRootAndExistingDestination()
    {
        var (vm, bridge, _, directory) = Create();
        // Replacing a directory with a file must fail; the root remains in memory.
        var unwritable = new TemporarySessionService(Path.Combine(directory, "blocked"));
        Directory.CreateDirectory(unwritable.SessionFilePath);
        var failing = new MainViewModel(new ProjectService(), bridge, unwritable);
        failing.AddPlacemarkCommand.Execute(null);
        var item = failing.MyPlaces.Single(p => p.ItemType == PlaceItemType.Placemark);
        Assert.IsFalse(failing.ResolvePendingPlacesOnClose(MessageBoxResult.Yes));
        Assert.IsTrue(failing.MyPlaces.Contains(item));
        Assert.IsTrue(Directory.Exists(unwritable.SessionFilePath));
    }

    [TestMethod]
    public void MoveToFolderRejectsCyclesAndNonScenesAndTemporaryMoveSurvivesUndo()
    {
        var (vm, _, session, _) = Create();
        var folder = new PlaceItem("Folder", PlaceItemType.Folder);
        var child = new PlaceItem("Child", PlaceItemType.Folder);
        folder.Children.Add(child);
        vm.MyPlaces.Add(folder);
        Assert.IsFalse(vm.MovePlaceToFolder(folder, child));
        Assert.IsFalse(vm.MovePlaceToFolder(folder, vm.CurrentProject.EnsureSavedScenesFolder()));
        Assert.IsTrue(vm.MovePlaceToFolder(folder, vm.TemporaryPlacesFolder));
        Assert.IsTrue(session.LoadSession().Any(p => p.Name == "Folder"));
        vm.UndoCommand.Execute(null);
        Assert.IsTrue(vm.MyPlaces.Contains(folder));
        Assert.IsFalse(session.LoadSession().Any(p => p.Name == "Folder"));
        vm.RedoCommand.Execute(null);
        Assert.IsTrue(session.LoadSession().Any(p => p.Name == "Folder"));
    }

    [TestMethod]
    public void TestProfileRedirectsRecoveryAndTemporarySession()
    {
        var profile = Environment.GetEnvironmentVariable("ZEARTH_PROFILE_DIRECTORY");
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile));
        var service = new ProjectService();
        var session = new TemporarySessionService();
        StringAssert.StartsWith(Path.GetFullPath(service.AutoSavePath), Path.GetFullPath(profile!));
        StringAssert.StartsWith(Path.GetFullPath(session.SessionFilePath), Path.GetFullPath(profile!));
    }

    [TestMethod]
    public async Task StartupSceneWaitsForModeAndBasemapBeforeCameraAndStopsOnFailure()
    {
        var (vm, bridge, _, _) = Create();
        var scene = new PlaceItem("First", PlaceItemType.SavedScene) { Camera = CameraState.CreateHome() };
        scene.ExtendedData["sceneMode"] = SceneMode.Scene2D.ToString();
        scene.ExtendedData["basemapId"] = "esri-hybrid";
        vm.CurrentProject.EnsureSavedScenesFolder().Children.Add(scene);
        var actions = new List<string>();
        string? failAction = null;
        bridge.SendJsonToWebView = json =>
        {
            using var doc = JsonDocument.Parse(json);
            var action = doc.RootElement.GetProperty("action").GetString()!;
            var id = doc.RootElement.GetProperty("requestId").GetString()!;
            actions.Add(action);
            bridge.HandleWebMessage(JsonSerializer.Serialize(new BridgeResponse {
                RequestId = id, Action = action, Success = action != failAction,
                Status = action == failAction ? BridgeStatus.Failed : BridgeStatus.Completed
            }));
            return Task.CompletedTask;
        };
        await vm.RestoreStartupSceneAsync();
        CollectionAssert.AreEqual(new[] { "scene.setMode", "layers.setBasemap", "camera.flyTo" }, actions.Take(3).ToArray());
        actions.Clear();
        failAction = "layers.setBasemap";
        await vm.RestoreStartupSceneAsync();
        Assert.IsFalse(actions.Contains("camera.flyTo"));
        Assert.IsTrue(vm.StatusMessage.Contains("could not", StringComparison.OrdinalIgnoreCase));
    }
}
