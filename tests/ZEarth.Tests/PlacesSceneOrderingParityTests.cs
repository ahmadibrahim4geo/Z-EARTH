using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;

namespace ZEarth.Tests;

[TestClass]
public class PlacesSceneOrderingParityTests
{
    [TestMethod]
    public void MovingScenes_ChangesStartupToFirstAndPreservesOrderedCollection()
    {
        var document = ProjectDocument.CreateDefault();
        var folder = document.EnsureSavedScenesFolder();
        var first = new PlaceItem("Cairo", new CameraState(31, 30, 1000, 0, -45, 0));
        var second = new PlaceItem("Alexandria", new CameraState(29.9, 31.2, 1000, 0, -45, 0));
        var last = new PlaceItem("Luxor", new CameraState(32.6, 25.6, 1000, 0, -45, 0));
        folder.AddChild(first);
        folder.AddChild(second);
        folder.AddChild(last);
        document.SyncSavedScenes();

        Assert.IsTrue(document.MoveSavedScene(second, -1));
        Assert.AreSame(second, folder.Children[0]);
        Assert.IsTrue(second.IsStartupScene);
        Assert.IsFalse(first.IsStartupScene);
        Assert.AreSame(second, document.SavedScenes[0]);

        Assert.IsTrue(document.MoveSavedScene(second, 1));
        Assert.AreSame(first, folder.Children[0]);
        Assert.IsTrue(first.IsStartupScene);
    }

    [TestMethod]
    public void SceneReordering_DoesNotMoveSystemFoldersOrCrossBoundaries()
    {
        var document = ProjectDocument.CreateDefault();
        var folder = document.EnsureSavedScenesFolder();
        var scene = new PlaceItem("Cairo", new CameraState(31, 30, 1000, 0, -45, 0));
        folder.AddChild(scene);
        Assert.IsFalse(document.MoveSavedScene(scene, -1));
        Assert.IsFalse(document.MoveSavedScene(scene, 1));
        Assert.IsFalse(document.MoveSavedScene(folder, 1));
        Assert.IsFalse(document.MoveSavedScene(new PlaceItem("Unrelated", PlaceItemType.Placemark), 1));
        Assert.IsFalse(document.MoveSavedScene(scene, 100));
        Assert.AreEqual(1, folder.Children.Count);
        Assert.AreSame(scene, folder.Children[0]);
    }
}
