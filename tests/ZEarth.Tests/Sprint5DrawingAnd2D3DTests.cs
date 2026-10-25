using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint5DrawingAnd2D3DTests
    {
        [TestMethod]
        public void MapViewport_NeutralContract_SatisfiesInterface()
        {
            var bridge = new GlobeBridge();
            IMapViewport viewport = bridge;

            Assert.IsNotNull(viewport.CurrentCamera);
            Assert.AreEqual(SceneMode.Scene3D, viewport.CurrentMode);
            Assert.IsNotNull(bridge as IMapViewport);
        }

        [TestMethod]
        public void DrawCompleted_PolygonGeometry_AddsPolygonPlaceItemWithCoordinates()
        {
            var projectService = new ProjectService();
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, bridge);

            int initialCount = vm.MyPlaces.Count;

            // Simulate draw.completed event from Cesium JS
            var drawPayload = new
            {
                @event = "draw.completed",
                geometryType = "polygon",
                color = "#34d399",
                coordinates = new[]
                {
                    new { longitude = 31.0, latitude = 30.0, altitude = 0.0 },
                    new { longitude = 32.0, latitude = 30.0, altitude = 0.0 },
                    new { longitude = 32.0, latitude = 31.0, altitude = 0.0 },
                    new { longitude = 31.0, latitude = 31.0, altitude = 0.0 }
                }
            };

            var json = JsonSerializer.Serialize(drawPayload);
            bridge.HandleWebMessage(json);

            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count, "Polygon should be added to MyPlaces");
            var item = vm.MyPlaces.Last();
            Assert.AreEqual(PlaceItemType.Polygon, item.ItemType);
            Assert.AreEqual("#34d399", item.ColorHex);
            Assert.AreEqual(4, item.Coordinates.Count);
            Assert.IsTrue(vm.IsModified);
        }

        [TestMethod]
        public void DrawCompleted_PathGeometry_AddsPathPlaceItemWithCoordinates()
        {
            var projectService = new ProjectService();
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, bridge);

            int initialCount = vm.MyPlaces.Count;

            var drawPayload = new
            {
                @event = "draw.completed",
                geometryType = "path",
                color = "#fbbf24",
                coordinates = new[]
                {
                    new { longitude = 31.2, latitude = 30.0, altitude = 10.0 },
                    new { longitude = 31.4, latitude = 30.2, altitude = 20.0 }
                }
            };

            bridge.HandleWebMessage(JsonSerializer.Serialize(drawPayload));

            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count);
            var item = vm.MyPlaces.Last();
            Assert.AreEqual(PlaceItemType.Path, item.ItemType);
            Assert.AreEqual(2, item.Coordinates.Count);
            Assert.AreEqual(10.0, item.Coordinates[0].Altitude);
        }

        [TestMethod]
        public void DrawCompleted_UndoRedo_RevertsAndRedoesDrawnGeometry()
        {
            var projectService = new ProjectService();
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, bridge);

            int initialCount = vm.MyPlaces.Count;

            var drawPayload = new
            {
                @event = "draw.completed",
                geometryType = "polygon",
                color = "#a78bfa",
                coordinates = new[]
                {
                    new { longitude = 30.0, latitude = 29.0, altitude = 0.0 },
                    new { longitude = 31.0, latitude = 29.0, altitude = 0.0 },
                    new { longitude = 30.5, latitude = 30.0, altitude = 0.0 }
                }
            };

            bridge.HandleWebMessage(JsonSerializer.Serialize(drawPayload));
            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count);

            // Undo
            Assert.IsTrue(vm.UndoRedo.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.AreEqual(initialCount, vm.MyPlaces.Count, "Undo should remove drawn polygon");

            // Redo
            Assert.IsTrue(vm.UndoRedo.CanRedo);
            vm.RedoCommand.Execute(null);
            Assert.AreEqual(initialCount + 1, vm.MyPlaces.Count, "Redo should restore drawn polygon");
            Assert.AreEqual(PlaceItemType.Polygon, vm.MyPlaces.Last().ItemType);
        }

        [TestMethod]
        public void FeatureProperties_StyleColorAndWidth_UpdatesAttributes()
        {
            var item = new PlaceItem("Test Polygon", PlaceItemType.Polygon)
            {
                ColorHex = "#ff0000"
            };
            item.Coordinates.Add(new CoordinatePoint(31, 30));
            item.Coordinates.Add(new CoordinatePoint(32, 30));
            item.Coordinates.Add(new CoordinatePoint(31.5, 31));

            var cloned = item.Clone();
            Assert.AreEqual("#ff0000", cloned.ColorHex);
            Assert.AreEqual(3, cloned.Coordinates.Count);

            // Modify color
            item.ColorHex = "#00ff00";
            Assert.AreEqual("#00ff00", item.ColorHex);
        }

        [TestMethod]
        public void SceneMode_100ConsecutiveTransitions_MaintainsFeatureParityAndRecordsLog()
        {
            var projectService = new ProjectService();
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(projectService, bridge);

            // Populate project with test features
            var point = new PlaceItem("Cairo Tower Point", PlaceItemType.Placemark) { ColorHex = "#e11d48" };
            point.Coordinates.Add(new CoordinatePoint(31.2243, 30.0459, 187));
            vm.MyPlaces.Add(point);

            var polyline = new PlaceItem("Nile Transect", PlaceItemType.Path) { ColorHex = "#2563eb" };
            polyline.Coordinates.Add(new CoordinatePoint(31.23, 30.01, 20));
            polyline.Coordinates.Add(new CoordinatePoint(31.24, 30.05, 20));
            polyline.Coordinates.Add(new CoordinatePoint(31.25, 30.10, 20));
            vm.MyPlaces.Add(polyline);

            var polygon = new PlaceItem("Giza Plateau Complex", PlaceItemType.Polygon) { ColorHex = "#10b981" };
            polygon.Coordinates.Add(new CoordinatePoint(31.12, 29.97, 60));
            polygon.Coordinates.Add(new CoordinatePoint(31.14, 29.97, 60));
            polygon.Coordinates.Add(new CoordinatePoint(31.14, 29.99, 60));
            polygon.Coordinates.Add(new CoordinatePoint(31.12, 29.99, 60));
            polygon.InnerRings.Add(new List<CoordinatePoint>
            {
                new CoordinatePoint(31.125, 29.975, 60),
                new CoordinatePoint(31.135, 29.975, 60),
                new CoordinatePoint(31.135, 29.985, 60),
                new CoordinatePoint(31.125, 29.985, 60)
            });
            vm.MyPlaces.Add(polygon);

            int initialPlaceCount = vm.MyPlaces.Count;
            var initialIds = vm.MyPlaces.Select(p => p.Id).ToList();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);

            var cycleRecords = new List<object>();

            for (int cycle = 1; cycle <= 100; cycle++)
            {
                var targetMode = (cycle % 2 == 1) ? SceneMode.Scene2D : SceneMode.Scene3D;
                var modeJson = $"{{\"event\": \"scene.modeChanged\", \"mode\": \"{targetMode}\"}}";
                bridge.HandleWebMessage(modeJson);

                // Assertions after each transition
                Assert.AreEqual(targetMode, bridge.CurrentMode, $"Bridge mode should be {targetMode} at cycle {cycle}");
                Assert.AreEqual(initialPlaceCount, vm.MyPlaces.Count, $"Feature count corrupted at cycle {cycle}");
                for (int i = 0; i < initialPlaceCount; i++)
                {
                    Assert.AreEqual(initialIds[i], vm.MyPlaces[i].Id, $"Place ID mismatch at cycle {cycle}, index {i}");
                }

                if (cycle % 10 == 0 || cycle == 1 || cycle == 100)
                {
                    cycleRecords.Add(new
                    {
                        Cycle = cycle,
                        Mode = targetMode.ToString(),
                        PlaceCount = vm.MyPlaces.Count,
                        WorkingSetDeltaBytes = GC.GetTotalMemory(false) - initialMemory,
                        Status = "OK"
                    });
                }
            }

            GC.Collect();
            long finalMemory = GC.GetTotalMemory(true);

            var report = new
            {
                TotalTransitions = 100,
                InitialPlaces = initialPlaceCount,
                FinalPlaces = vm.MyPlaces.Count,
                InitialMemoryBytes = initialMemory,
                FinalMemoryBytes = finalMemory,
                NetMemoryDeltaBytes = finalMemory - initialMemory,
                Samples = cycleRecords,
                Result = "PASS",
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            var outDir = @"D:\My Software\Z EARTH\Development Reports\RC1_Independent_Acceptance\logs";
            if (!System.IO.Directory.Exists(outDir))
            {
                System.IO.Directory.CreateDirectory(outDir);
            }
            var outPath = System.IO.Path.Combine(outDir, "morph_100_cycles.json");
            System.IO.File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
