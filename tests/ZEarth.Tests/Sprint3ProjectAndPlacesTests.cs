using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint3ProjectAndPlacesTests
    {
        private string _testDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ZEarth_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        [TestMethod]
        public async Task ProjectService_SaveAsync_CreatesValidZEarthZipBundle_WithManifestAndAtomicReplacement()
        {
            // ZE-025 & AC-029: .zearth ZIP manifest + atomic save
            var service = new ProjectService();
            var doc = service.CurrentProject;
            doc.Name = "Atomic Test Project";
            doc.Author = "Ahmad Ibrahim";

            var filePath = Path.Combine(_testDir, "test_project.zearth");

            // 1. Initial Save
            var saved = await service.SaveAsync(filePath);
            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(filePath));
            Assert.IsFalse(service.IsModified);

            // 2. Check Magic Bytes (0x50 0x4B 0x03 0x04 -> "PK..")
            var bytes = await File.ReadAllBytesAsync(filePath);
            Assert.IsTrue(bytes.Length > 4);
            Assert.AreEqual(0x50, bytes[0]);
            Assert.AreEqual(0x4B, bytes[1]);
            Assert.AreEqual(0x03, bytes[2]);
            Assert.AreEqual(0x04, bytes[3]);

            // 3. Inspect ZIP Contents
            using (var zipStream = File.OpenRead(filePath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var manifestEntry = archive.GetEntry("manifest.json");
                Assert.IsNotNull(manifestEntry, "manifest.json must be present in .zearth archive");

                using var mr = new StreamReader(manifestEntry.Open());
                var manifestText = await mr.ReadToEndAsync();
                StringAssert.Contains(manifestText, "zearth");
                StringAssert.Contains(manifestText, "1.0.0");
                StringAssert.Contains(manifestText, "Ahmad Ibrahim");

                var projectEntry = archive.GetEntry("project.json");
                Assert.IsNotNull(projectEntry, "project.json must be present in .zearth archive");
            }

            // 4. Overwrite (Atomic Replacement Test)
            doc.Name = "Atomic Overwritten Project";
            var overwritten = await service.SaveAsync(filePath);
            Assert.IsTrue(overwritten);

            // 5. Reopen
            var loaded = await service.LoadAsync(filePath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Atomic Overwritten Project", loaded.Name);
            Assert.AreEqual("Ahmad Ibrahim", loaded.Author);
        }

        [TestMethod]
        public async Task ProjectService_LoadAsync_MigratesLegacyPrototypeJsonFormat()
        {
            // RTM 3.3: Backward compatibility migration for legacy prototype JSON files
            var service = new ProjectService();
            var legacyJsonPath = Path.Combine(_testDir, "legacy_prototype.zearth");

            var prototypeDoc = ProjectDocument.CreateDefault();
            prototypeDoc.Name = "Legacy Prototype Project";
            var json = JsonSerializer.Serialize(prototypeDoc, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(legacyJsonPath, json);

            // Load legacy JSON file
            var loaded = await service.LoadAsync(legacyJsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Legacy Prototype Project", loaded.Name);
            Assert.IsTrue(loaded.MyPlaces.Count > 0);

            // Resaving converts it cleanly to .zearth ZIP bundle
            await service.SaveAsync(legacyJsonPath);
            var header = new byte[4];
            using (var fs = File.OpenRead(legacyJsonPath))
            {
                await fs.ReadExactlyAsync(header, 0, 4);
            }
            Assert.AreEqual(0x50, header[0]);
            Assert.AreEqual(0x4B, header[1]);
        }

        [TestMethod]
        public async Task MyPlaces_NestedFolderHierarchy_PreservesOrderAndArbitraryNesting()
        {
            // ZE-023 & AC-006: 5 nested folders hierarchy test
            var service = new ProjectService();
            var doc = service.CurrentProject;
            doc.MyPlaces.Clear();

            var current = new PlaceItem("Level 1 Folder", PlaceItemType.Folder);
            doc.MyPlaces.Add(current);

            for (int i = 2; i <= 5; i++)
            {
                var next = new PlaceItem($"Level {i} Folder", PlaceItemType.Folder);
                current.AddChild(next);
                current = next;
            }

            // Add deep placemark inside 5th level
            var deepPlacemark = new PlaceItem("Deepest Target", PlaceItemType.Placemark, 30.0, 31.0, 100);
            current.AddChild(deepPlacemark);

            var path = Path.Combine(_testDir, "nested.zearth");
            await service.SaveAsync(path);

            var loaded = await service.LoadAsync(path);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.MyPlaces.Count);

            var l1 = loaded.MyPlaces[0];
            Assert.AreEqual("Level 1 Folder", l1.Name);
            var l2 = l1.Children.FirstOrDefault();
            Assert.IsNotNull(l2);
            var l3 = l2.Children.FirstOrDefault();
            Assert.IsNotNull(l3);
            var l4 = l3.Children.FirstOrDefault();
            Assert.IsNotNull(l4);
            var l5 = l4.Children.FirstOrDefault();
            Assert.IsNotNull(l5);
            Assert.AreEqual("Level 5 Folder", l5.Name);

            var leaf = l5.Children.FirstOrDefault();
            Assert.IsNotNull(leaf);
            Assert.AreEqual("Deepest Target", leaf.Name);
            Assert.AreEqual(30.0, leaf.Latitude);
            Assert.AreEqual(31.0, leaf.Longitude);
        }

        [TestMethod]
        public void TemporaryPlaces_SaveToMyPlaces_MovesItemAndSurvivesIndependently()
        {
            // ZE-024 & AC-010: Temporary Places independent recovery & commit to My Places
            var projectService = new ProjectService();
            var globeBridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, globeBridge);

            var tempFeature = new PlaceItem("GPS Track Log (Temporary)", PlaceItemType.Placemark, 29.9792, 31.1342, 80);
            vm.TemporaryPlaces.Add(tempFeature);
            vm.SelectedPlace = tempFeature;

            Assert.IsTrue(vm.TemporaryPlaces.Contains(tempFeature));
            Assert.IsFalse(vm.MyPlaces.Contains(tempFeature));
            Assert.IsTrue(vm.SaveToMyPlacesCommand.CanExecute(null));

            // Execute Save to My Places
            vm.SaveToMyPlacesCommand.Execute(null);

            Assert.IsFalse(vm.TemporaryPlaces.Contains(tempFeature), "Item should be removed from Temporary Places");
            Assert.IsTrue(vm.MyPlaces.Contains(tempFeature), "Item should be moved to My Places");
            Assert.IsTrue(vm.IsModified, "Project should be marked dirty");
        }

        [TestMethod]
        public void DirtyIndicator_Modifications_UpdateWindowTitleAsterisk()
        {
            // ZE-026 & AC-011: Dirty indicator in window title
            var projectService = new ProjectService();
            var globeBridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, globeBridge);

            Assert.IsFalse(vm.IsModified);
            StringAssert.StartsWith(vm.WindowTitle, "Z EARTH Desktop");

            // Add placemark -> project becomes modified
            vm.AddPlacemarkCommand.Execute(null);

            Assert.IsTrue(vm.IsModified);
            StringAssert.StartsWith(vm.WindowTitle, "* Z EARTH Desktop");
        }

        [TestMethod]
        public async Task AutoSave_PeriodicSessionAndCrashRecovery_RestoresUnsavedState()
        {
            // ZE-027 & AC-012: Periodic autosave and crash recovery
            var service = new ProjectService();
            service.CurrentProject.Name = "Unsaved Crash Session";
            service.CurrentProject.MyPlaces.Add(new PlaceItem("Unsaved Landmark", PlaceItemType.Placemark, 25.0, 30.0, 50));

            var autoSaved = await service.AutoSaveAsync();
            Assert.IsTrue(autoSaved);
            Assert.IsTrue(File.Exists(service.AutoSavePath));

            // Recover
            var recovered = await service.TryRecoverAutoSaveAsync();
            Assert.IsNotNull(recovered);
            Assert.AreEqual("Unsaved Crash Session", recovered.Name);
            Assert.IsTrue(recovered.MyPlaces.Any(p => p.Name == "Unsaved Landmark"));
        }

        [TestMethod]
        public async Task SavedScenes_CameraPerspective_PersistsAndRestores()
        {
            // ZE-028 & AC-005, AC-010: Saved scenes persistence
            var service = new ProjectService();
            var cam = new CameraState(31.2001, 29.9187, 12_000.0, 45.0, -45.0, 0.0);
            var scene = new PlaceItem("Alexandria Coast Angle", cam);
            service.CurrentProject.SavedScenes.Add(scene);

            var path = Path.Combine(_testDir, "scenes.zearth");
            await service.SaveAsync(path);

            var loaded = await service.LoadAsync(path);
            Assert.IsNotNull(loaded);
            var restoredScene = loaded.SavedScenes.FirstOrDefault(s => s.Name == "Alexandria Coast Angle");
            Assert.IsNotNull(restoredScene);
            Assert.IsNotNull(restoredScene.Camera);
            Assert.AreEqual(31.2001, restoredScene.Camera.Latitude, 0.0001);
            Assert.AreEqual(29.9187, restoredScene.Camera.Longitude, 0.0001);
            Assert.AreEqual(12_000.0, restoredScene.Camera.Altitude, 0.1);
            Assert.AreEqual(45.0, restoredScene.Camera.Heading, 0.1);
            Assert.AreEqual(-45.0, restoredScene.Camera.Pitch, 0.1);
        }

        [TestMethod]
        public void UndoRedo_TreeCRUDOperations_RevertAndReapplyCleanly()
        {
            // ZE-029 & AC-014: Undo/Redo model
            var projectService = new ProjectService();
            var globeBridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, globeBridge);

            int initialCount = vm.MyPlaces.Count;
            Assert.IsFalse(vm.CanUndo);
            Assert.IsFalse(vm.CanRedo);

            // 1. Add Placemark
            vm.AddPlacemarkCommand.Execute(null);
            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count);
            Assert.IsTrue(vm.CanUndo);
            Assert.IsFalse(vm.CanRedo);

            // 2. Undo Add
            vm.UndoCommand.Execute(null);
            Assert.AreEqual(initialCount, vm.MyPlaces.Count);
            Assert.IsFalse(vm.CanUndo);
            Assert.IsTrue(vm.CanRedo);

            // 3. Redo Add
            vm.RedoCommand.Execute(null);
            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count);
            Assert.IsTrue(vm.CanUndo);
            Assert.IsFalse(vm.CanRedo);

            // 4. Delete item
            var addedItem = vm.MyPlaces.Last();
            vm.SelectedPlace = addedItem;
            vm.DeletePlaceCommand.Execute(null);
            Assert.AreEqual(initialCount, vm.MyPlaces.Count);

            // 5. Undo Delete
            vm.UndoCommand.Execute(null);
            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count);
            Assert.AreEqual(addedItem.Name, vm.MyPlaces.Last().Name);
        }
    }
}
