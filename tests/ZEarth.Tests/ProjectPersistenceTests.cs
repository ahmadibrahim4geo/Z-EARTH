using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;

namespace ZEarth.Tests
{
    [TestClass]
    public class ProjectPersistenceTests
    {
        [TestMethod]
        public void CreateDefault_GeneratesValidHierarchy()
        {
            var project = ProjectDocument.CreateDefault();

            Assert.IsNotNull(project);
            Assert.AreEqual("Ahmad Ibrahim", project.Author);
            Assert.AreEqual("EPSG:4326", project.Crs);
            Assert.AreEqual(SceneMode.Scene3D, project.SceneMode);
            Assert.AreEqual(2, project.MyPlaces.Count, "A new project must contain only the two protected system folders.");
            Assert.AreEqual(0, project.SavedScenes.Count, "A new project must not contain demo scenes.");

            var scenes = project.MyPlaces[0];
            Assert.AreEqual("folder_saved_scenes", scenes.Id);
            Assert.IsTrue(scenes.IsProtected);
            Assert.AreEqual(0, scenes.Children.Count);

            var temporary = project.MyPlaces[1];
            Assert.AreEqual("folder_temporary_places", temporary.Id);
            Assert.AreEqual("Temporary Places", temporary.Name);
            Assert.IsTrue(temporary.IsProtected);
            Assert.AreEqual(0, temporary.Children.Count, "Temporary Places must start as an empty root folder.");
            Assert.AreEqual(0, project.TemporaryPlaces.Count, "The legacy separate collection must remain empty.");
        }

        [TestMethod]
        public void JsonRoundTrip_PreservesAllPropertiesAndHierarchy()
        {
            var original = ProjectDocument.CreateDefault();
            original.Name = "Test Persistence Project";
            original.SceneMode = SceneMode.Scene2D;
            original.Camera = new CameraState(31.2, 29.9, 150_000, 45.0, -45.0);

            var customFolder = new PlaceItem("Custom Survey Area", PlaceItemType.Folder);
            customFolder.AddChild(new PlaceItem("Survey Point Alpha", PlaceItemType.Placemark, 30.5, 31.5, 120));
            original.EnsureTemporaryPlacesFolder().AddChild(customFolder);

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var restored = JsonSerializer.Deserialize<ProjectDocument>(json);
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Id, restored.Id);
            Assert.AreEqual(original.Name, restored.Name);
            Assert.AreEqual(original.Author, restored.Author);
            Assert.AreEqual(SceneMode.Scene2D, restored.SceneMode);
            Assert.AreEqual(31.2, restored.Camera.Latitude, 0.0001);
            Assert.AreEqual(29.9, restored.Camera.Longitude, 0.0001);

            var restoredTemporaryFolder = restored.EnsureTemporaryPlacesFolder();
            Assert.AreEqual(1, restoredTemporaryFolder.Children.Count);
            var restoredFolder = restoredTemporaryFolder.Children[0];
            Assert.AreEqual("Custom Survey Area", restoredFolder.Name);
            Assert.AreEqual(1, restoredFolder.Children.Count);
            Assert.AreEqual("Survey Point Alpha", restoredFolder.Children[0].Name);
            Assert.AreEqual(30.5, restoredFolder.Children[0].Latitude!.Value, 0.001);
        }

        [TestMethod]
        public void EnsureTemporaryPlacesFolder_MigratesLegacySeparateCollection()
        {
            var legacy = new ProjectDocument();
            legacy.TemporaryPlaces.Add(new PlaceItem("Legacy Import.kml", PlaceItemType.Folder));

            legacy.EnsureSavedScenesFolder();
            var folder = legacy.EnsureTemporaryPlacesFolder();

            Assert.AreEqual(2, legacy.MyPlaces.Count);
            Assert.AreSame(folder, legacy.MyPlaces[1]);
            Assert.AreEqual("folder_temporary_places", folder.Id);
            Assert.IsTrue(folder.IsProtected);
            Assert.AreEqual(1, folder.Children.Count);
            Assert.AreEqual("Legacy Import.kml", folder.Children[0].Name);
            Assert.AreEqual(0, legacy.TemporaryPlaces.Count);
        }
    }
}
