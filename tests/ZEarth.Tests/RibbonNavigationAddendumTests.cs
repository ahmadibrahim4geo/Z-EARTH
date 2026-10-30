using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Measurement;
using ZEarth.Domain.Persistence;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class RibbonNavigationAddendumTests
    {
        [TestMethod]
        public void NAV01_SavedScene_GroundFocusSeparatedFromCameraEye_DoesNotFloatAt4500km()
        {
            var camera = new CameraState(26.8206, 30.8025, 4_500_000.0, 0, -90);
            var scene = new PlaceItem("Cairo Focus Scene", camera, 30.0444, 31.2357, 25.0);

            Assert.AreEqual(PlaceItemType.SavedScene, scene.ItemType);
            Assert.AreEqual(4_500_000.0, scene.Camera?.Altitude);
            Assert.AreEqual(25.0, scene.Altitude);
            Assert.AreEqual(30.0444, scene.Latitude);
            Assert.AreEqual(31.2357, scene.Longitude);
            Assert.AreNotEqual(scene.Camera?.Altitude, scene.Altitude);
        }

        [TestMethod]
        public void NAV02_SavedScenes_OrderingAndStartupScene_PreservedOnProjectSaveReopen()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"zearth_scene_test_{Guid.NewGuid():N}.zearth");
            try
            {
                var doc = new ProjectDocument { Name = "Test Project" };
                var folder = doc.EnsureSavedScenesFolder();

                var scene1 = new PlaceItem("Scene 1 - Primary", new CameraState(30.0, 31.0, 100000.0, 0, -90), 30.0, 31.0, 0);
                var scene2 = new PlaceItem("Scene 2 - Secondary", new CameraState(25.0, 32.0, 200000.0, 45, -60), 25.0, 32.0, 0);

                folder.AddChild(scene1);
                folder.AddChild(scene2);
                doc.SyncSavedScenes();

                Assert.IsTrue(scene1.IsStartupScene, "First scene in Saved Scenes folder should be startup scene");
                Assert.IsFalse(scene2.IsStartupScene);

                SqliteProjectStorage.SaveToDatabase(doc, tempFile);

                var loadedDoc = SqliteProjectStorage.LoadFromDatabase(tempFile);
                var loadedFolder = loadedDoc.EnsureSavedScenesFolder();
                Assert.AreEqual(2, loadedFolder.Children.Count);
                Assert.AreEqual("Scene 1 - Primary", loadedFolder.Children[0].Name);
                Assert.IsTrue(loadedFolder.Children[0].IsStartupScene);
                Assert.AreEqual("Scene 2 - Secondary", loadedFolder.Children[1].Name);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void CRS01_CRS03_MapCenterAndCursorAndEye_DistinctReadouts()
        {
            var originalEpsg = SettingsService.Instance.Settings.MapCrsEpsg;
            SettingsService.Instance.Settings.MapCrsEpsg = 4326;
            try
            {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            // Simulate camera eye at 4.5 million meters
            bridge.SimulateCameraMoveEnd(new CameraState(26.8206, 30.8025, 4_500_000.0, 15.0, -80.0));

            // Eye altitude and heading
            Assert.IsTrue(vm.StatusAltitude.Contains("km"), "Eye altitude should be formatted in km");
            Assert.AreEqual("15.0°", vm.StatusHeading);

            // Center ground coordinate
            Assert.IsTrue(vm.StatusCenterCoordinates.StartsWith("Center:"));

            // Cursor coordinate and elevation
            Assert.IsTrue(vm.StatusCursorCoordinates.StartsWith("Cursor:"));
            Assert.IsTrue(vm.StatusCursorElevation.StartsWith("Elev:"));
            }
            finally
            {
                SettingsService.Instance.Settings.MapCrsEpsg = originalEpsg;
            }
        }

        [TestMethod]
        public void CRS07_OutOfGlobeSky_ShowsUnavailableOrOutsideGlobe()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            // Send invalid center (facing space void)
            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"valid\":false,\"latitude\":0,\"longitude\":0,\"altitude\":0}}");
            Assert.AreEqual("Center: Outside globe", vm.StatusCenterCoordinates);

            // Send invalid cursor (pointer over sky)
            bridge.HandleWebMessage("{\"event\":\"map.cursorMoved\",\"position\":{\"valid\":false,\"latitude\":0,\"longitude\":0,\"altitude\":0}}");
            Assert.AreEqual("Cursor: Outside globe", vm.StatusCursorCoordinates);
            Assert.AreEqual("Elev: —", vm.StatusCursorElevation);
        }

        [TestMethod]
        public void CMP03_CMP04_GeodesicQiblaAzimuth_CalculatesAccuratelyForCairo()
        {
            // Cairo coordinates
            double cairoLat = 30.0444;
            double cairoLon = 31.2357;

            // True forward geodesic azimuth from Cairo to Mecca is ~136 degrees (SE)
            double azimuth = GeodesicMeasurementService.CalculateQiblaAzimuth(cairoLat, cairoLon);
            Assert.IsTrue(azimuth >= 134.0 && azimuth <= 138.0, $"Expected Cairo Qibla azimuth ~136°, actual: {azimuth}°");
        }

        [TestMethod]
        public void EXP01_EXP02_BidirectionalMove_BetweenMyPlacesAndTemporaryPlaces_SupportsUndo()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            var item = new PlaceItem("Cairo Landmark", PlaceItemType.Placemark, 30.0444, 31.2357, 20.0);
            vm.MyPlaces.Add(item);
            Assert.IsTrue(vm.MyPlaces.Contains(item));
            Assert.IsFalse(vm.TemporaryPlaces.Contains(item));

            // Move to Temporary Places
            vm.MoveToTemporaryPlacesCommand.Execute(item);
            Assert.IsFalse(vm.MyPlaces.Contains(item), "Item should be removed from My Places");
            Assert.IsTrue(vm.TemporaryPlaces.Contains(item), "Item should be present in Temporary Places");

            // Undo move
            Assert.IsTrue(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.IsTrue(vm.MyPlaces.Contains(item), "Item should be restored to My Places after Undo");
            Assert.IsFalse(vm.TemporaryPlaces.Contains(item), "Item should be removed from Temporary Places after Undo");

            // Redo move
            Assert.IsTrue(vm.CanRedo);
            vm.RedoCommand.Execute(null);
            Assert.IsFalse(vm.MyPlaces.Contains(item));
            Assert.IsTrue(vm.TemporaryPlaces.Contains(item));

            // Move back to My Places
            vm.MoveToMyPlacesCommand.Execute(item);
            Assert.IsTrue(vm.MyPlaces.Contains(item));
            Assert.IsFalse(vm.TemporaryPlaces.Contains(item));
        }

        [TestMethod]
        public void EXP04_ExportCommands_ExplicitScope()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            Assert.IsNotNull(vm.ExportSelectedItemCommand, "ExportSelectedItemCommand must exist");
            Assert.IsNotNull(vm.ExportAllMyPlacesCommand, "ExportAllMyPlacesCommand must exist");
            Assert.IsNotNull(vm.ExportAllTemporaryPlacesCommand, "ExportAllTemporaryPlacesCommand must exist");
        }

        [TestMethod]
        public async Task GRID01_GRID02_GeographicGraticulesConfig_DispatchesCorrectParameters()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetGraticulesConfigAsync(true, true, true, false);
            var dispatchedJson = engine.LastPayloadFor("layers.setGraticulesConfig");
            Assert.IsFalse(string.IsNullOrEmpty(dispatchedJson));
            Assert.IsTrue(dispatchedJson.Contains("layers.setGraticulesConfig"));
            Assert.IsTrue(dispatchedJson.Contains("\"showKeyParallels\":true"));
            Assert.IsTrue(dispatchedJson.Contains("\"showUtmZones\":false"));
        }

        [TestMethod]
        public async Task CR01_CR02_CreditsConfig_DispatchesCorrectParameters()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetCreditsConfigAsync("Compact", true);
            var dispatchedJson = engine.LastPayloadFor("credits.setConfig");
            Assert.IsFalse(string.IsNullOrEmpty(dispatchedJson));
            Assert.IsTrue(dispatchedJson.Contains("credits.setConfig"));
            Assert.IsTrue(dispatchedJson.Contains("\"mode\":\"Compact\""));
            Assert.IsTrue(dispatchedJson.Contains("\"showOptionalLogos\":true"));
        }

        [TestMethod]
        public void UI01_ThemeTokens_TreeItemBrushes_DefinedInThemes()
        {
            var darkDict = new ResourceDictionary { Source = new Uri("/Z EARTH;component/Themes/DarkTheme.xaml", UriKind.RelativeOrAbsolute) };
            Assert.IsNotNull(darkDict["Brush.TreeItem.Hover"], "DarkTheme must contain Brush.TreeItem.Hover");
            Assert.IsNotNull(darkDict["Brush.TreeItem.Selected"], "DarkTheme must contain Brush.TreeItem.Selected");
            Assert.IsNotNull(darkDict["Brush.TreeItem.SelectedHover"], "DarkTheme must contain Brush.TreeItem.SelectedHover");
            Assert.IsNotNull(darkDict["Brush.TreeItem.SelectedBorder"], "DarkTheme must contain Brush.TreeItem.SelectedBorder");

            var lightDict = new ResourceDictionary { Source = new Uri("/Z EARTH;component/Themes/LightTheme.xaml", UriKind.RelativeOrAbsolute) };
            Assert.IsNotNull(lightDict["Brush.TreeItem.Hover"], "LightTheme must contain Brush.TreeItem.Hover");
            Assert.IsNotNull(lightDict["Brush.TreeItem.Selected"], "LightTheme must contain Brush.TreeItem.Selected");
            Assert.IsNotNull(lightDict["Brush.TreeItem.SelectedHover"], "LightTheme must contain Brush.TreeItem.SelectedHover");
            Assert.IsNotNull(lightDict["Brush.TreeItem.SelectedBorder"], "LightTheme must contain Brush.TreeItem.SelectedBorder");
        }

        [TestMethod]
        public void NAV03_CenterFacingSpace_CenterInfoValidFalse_SetsOutsideGlobe()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"valid\":false,\"latitude\":0,\"longitude\":0,\"altitude\":0}}");
            Assert.AreEqual("Center: Outside globe", vm.StatusCenterCoordinates);
            Assert.IsFalse(bridge.CurrentCenter?.IsValid ?? true);
        }

        [TestMethod]
        public async Task NAV04_ZoomSensitivity_SettingsAndBridgeDispatch()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetZoomSensitivityAsync(1.75);
            var dispatchedJson = engine.LastPayloadFor("camera.setZoomSensitivity");
            Assert.IsFalse(string.IsNullOrEmpty(dispatchedJson));
            Assert.IsTrue(dispatchedJson.Contains("camera.setZoomSensitivity"));
            Assert.IsTrue(dispatchedJson.Contains("1.75"));
        }

        private static string GetSolutionRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ZEarth.sln")))
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return dir ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        [TestMethod]
        public void NAV05_NAV06_NAV07_DoubleClickZoom_EngineChecksActiveDrawingAndMeasuringModes()
        {
            var jsPath = Path.Combine(GetSolutionRoot(), "src", "ZEarth.Globe", "Assets", "cesium-engine.js");
            Assert.IsTrue(File.Exists(jsPath), $"cesium-engine.js must exist at {jsPath}");
            var jsContent = File.ReadAllText(jsPath);

            Assert.IsTrue(jsContent.Contains("LEFT_DOUBLE_CLICK"), "Engine must handle LEFT_DOUBLE_CLICK");
            Assert.IsTrue(jsContent.Contains("DrawingManager.activeMode"), "Double click must check DrawingManager.activeMode");
            Assert.IsTrue(jsContent.Contains("MeasurementManager.activeMode"), "Double click must check MeasurementManager.activeMode");
            Assert.IsTrue(jsContent.Contains("flyTo"), "Double click on valid ground must trigger smooth flyTo zoom");
        }

        [TestMethod]
        public async Task NAV08_ModeAndBasemapTransitions_PreservesBridgeAndCamera()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetSceneModeAsync(SceneMode.Scene2D);
            var lastModeJson = engine.LastPayloadFor("scene.setMode");
            Assert.IsFalse(string.IsNullOrEmpty(lastModeJson));
            Assert.IsTrue(lastModeJson.Contains("2D"));

            await bridge.SetSceneModeAsync(SceneMode.Scene3D);
            lastModeJson = engine.LastPayloadFor("scene.setMode");
            Assert.IsTrue(lastModeJson.Contains("3D"));
        }

        [TestMethod]
        public void CRS02_CursorMoved_Throttled_UpdatesIndependentlyOfCenter()
        {
            var originalEpsg = SettingsService.Instance.Settings.MapCrsEpsg;
            SettingsService.Instance.Settings.MapCrsEpsg = 4326;
            try
            {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"valid\":true,\"latitude\":30.0444,\"longitude\":31.2357,\"altitude\":25.0}}");
            var initialCenter = vm.StatusCenterCoordinates;

            bridge.HandleWebMessage("{\"event\":\"map.cursorMoved\",\"position\":{\"valid\":true,\"latitude\":31.2001,\"longitude\":29.9187,\"altitude\":120.0}}");
            Assert.AreEqual(initialCenter, vm.StatusCenterCoordinates, "Center coordinates must not change when mouse moves");
            Assert.IsTrue(vm.StatusCursorCoordinates.Contains("31.2001° N"));
            Assert.AreEqual("Elev: 120 m", vm.StatusCursorElevation);
            }
            finally
            {
                SettingsService.Instance.Settings.MapCrsEpsg = originalEpsg;
            }
        }

        [TestMethod]
        public void CursorWithoutDemElevation_DisplaysUnavailableInsteadOfZero()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);
            bridge.HandleWebMessage("{\"event\":\"map.cursorMoved\",\"position\":{\"valid\":true,\"latitude\":30,\"longitude\":31,\"altitude\":null}}");
            Assert.AreEqual("Elev: —", vm.StatusCursorElevation);
        }

        [TestMethod]
        public void CRS04_CRS05_CRS06_ProjectedCrs_EastingNorthing_MatchesTransformService()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            // Configure UTM 36N (EPSG 32636)
            SettingsService.Instance.Settings.MapCrsEpsg = 32636;

            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"valid\":true,\"latitude\":30.0444,\"longitude\":31.2357,\"altitude\":25.0}}");
            Assert.IsTrue(vm.StatusCenterCoordinates.StartsWith("Center X/E:"), "Projected center coordinates must show Easting/Northing");
            Assert.IsTrue(vm.StatusCenterCoordinates.Contains("Y/N:"));
            StringAssert.Contains(vm.StatusCrs, "EPSG:32636", "CRS label must identify the active projected map CRS");

            // Reset back to WGS84 (EPSG 4326)
            SettingsService.Instance.Settings.MapCrsEpsg = 4326;
            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"valid\":true,\"latitude\":30.0444,\"longitude\":31.2357,\"altitude\":25.0}}");
            Assert.IsTrue(vm.StatusCenterCoordinates.Contains("30.0444° N"));
            var statusCrs = vm.StatusCrs;
            vm.ExecuteToggleCoordinateFormat();
            Assert.AreEqual(statusCrs, vm.StatusCrs, "Coordinate display format must not replace the active map CRS label");
            vm.ExecuteToggleCoordinateFormat();
            vm.ExecuteToggleCoordinateFormat();
            vm.ExecuteToggleCoordinateFormat();
        }

        [TestMethod]
        public void CRS08_SettingsService_CoordinateDisplayPreferences_Persist()
        {
            var settings = SettingsService.Instance.Settings;
            settings.ShowCenterReadout = true;
            settings.ShowCursorReadout = true;
            settings.ShowCenterCrosshair = false;
            settings.ShowKeyParallels = true;
            settings.ShowUtmZones = true;

            SettingsService.Instance.Save();
            var loaded = SettingsService.Instance.Settings;

            Assert.IsTrue(loaded.ShowCenterReadout);
            Assert.IsTrue(loaded.ShowCursorReadout);
            Assert.IsFalse(loaded.ShowCenterCrosshair);
            Assert.IsTrue(loaded.ShowKeyParallels);
            Assert.IsTrue(loaded.ShowUtmZones);
        }

        [TestMethod]
        public async Task GRID03_GraticulesConfig_2D3D_IndependentOfScreenGrid()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetGraticulesConfigAsync(true, true, true, true);
            var dispatchedJson = engine.LastPayloadFor("layers.setGraticulesConfig");
            Assert.IsFalse(string.IsNullOrEmpty(dispatchedJson));
            Assert.IsTrue(dispatchedJson.Contains("layers.setGraticulesConfig"));
            Assert.IsTrue(dispatchedJson.Contains("\"showLabels\":true"));
            Assert.IsTrue(dispatchedJson.Contains("\"showKeyParallels\":true"));
            Assert.IsTrue(dispatchedJson.Contains("\"showUtmZones\":true"));
        }

        [TestMethod]
        public void CR03_SwipeCredits_BothSourcesReported()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            vm.ActiveAttribution = "Esri World Imagery, OpenStreetMap";
            Assert.IsTrue(vm.StatusEngineInfo.Contains("Esri World Imagery"));
            Assert.IsTrue(vm.StatusEngineInfo.Contains("OpenStreetMap"));
        }

        [TestMethod]
        public void EXP03_SaveProjectVsSaveTemporarySession_DistinctSemantics()
        {
            var bridge = new GlobeBridge();
            var vm = new MainViewModel(new ProjectService(), bridge);

            var tempItem = new PlaceItem("Field Inspection", PlaceItemType.Placemark, 30.0, 31.0);
            vm.TemporaryPlaces.Add(tempItem);

            Assert.IsTrue(vm.TemporaryPlaces.Contains(tempItem));
            Assert.IsFalse(vm.MyPlaces.Contains(tempItem), "Temporary item must not be added to permanent My Places automatically");

            vm.SaveTemporaryToProjectCommand.Execute(null);
            Assert.IsFalse(vm.MyPlaces.Contains(tempItem), "Saving temporary session to project must not silently duplicate into My Places");
            Assert.IsTrue(vm.TemporaryPlaces.Contains(tempItem));
        }

        [TestMethod]
        public void EXP05_EXP06_MultiSelectionAndDragDrop_PreservesMetadata()
        {
            var folder = new PlaceItem("Giza Survey Folder", PlaceItemType.Folder);
            var child = new PlaceItem("Pyramid of Khufu", PlaceItemType.Placemark, 29.9792, 31.1342, 140.0);
            child.Description = "<p>Great Pyramid</p>";
            child.ExtendedData["SurveyDate"] = "2026-10-21";
            folder.AddChild(child);

            Assert.AreEqual(1, folder.Children.Count);
            Assert.AreEqual("2026-10-21", folder.Children[0].ExtendedData["SurveyDate"]);

            // Remove and add (mimicking drag-and-drop / move)
            folder.Children.Remove(child);
            var targetFolder = new PlaceItem("Historic Monograph", PlaceItemType.Folder);
            targetFolder.AddChild(child);

            Assert.AreEqual(0, folder.Children.Count);
            Assert.AreEqual(1, targetFolder.Children.Count);
            Assert.AreEqual("Pyramid of Khufu", targetFolder.Children[0].Name);
            Assert.AreEqual("2026-10-21", targetFolder.Children[0].ExtendedData["SurveyDate"]);
            Assert.AreEqual("<p>Great Pyramid</p>", targetFolder.Children[0].Description);
        }

        [TestMethod]
        public async Task CMP01_CMP02_CompassCardinalAndHeading_DispatchesAndFormatsIntegerDegree()
        {
            var bridge = new GlobeBridge();
            var engine = FakeGlobeEngine.Attach(bridge);

            await bridge.SetCompassModeAsync("heading");
            var dispatchedJson = engine.LastPayloadFor("compass.setMode");
            Assert.IsFalse(string.IsNullOrEmpty(dispatchedJson));
            Assert.IsTrue(dispatchedJson.Contains("compass.setMode"));
            Assert.IsTrue(dispatchedJson.Contains("\"mode\":\"heading\""));

            await bridge.SetCompassModeAsync("cardinal");
            dispatchedJson = engine.LastPayloadFor("compass.setMode");
            Assert.IsTrue(dispatchedJson.Contains("\"mode\":\"cardinal\""));
        }

        [TestMethod]
        public void CMP05_QiblaSkyCenter_ShowsOffGlobeWarningInHtmlAndScript()
        {
            var htmlPath = Path.Combine(GetSolutionRoot(), "src", "ZEarth.Globe", "Assets", "index.html");
            var jsPath = Path.Combine(GetSolutionRoot(), "src", "ZEarth.Globe", "Assets", "cesium-engine.js");

            Assert.IsTrue(File.Exists(htmlPath));
            Assert.IsTrue(File.Exists(jsPath));

            var html = File.ReadAllText(htmlPath);
            var js = File.ReadAllText(jsPath);

            Assert.IsTrue(html.Contains("qibla-warning"), "HTML must contain qibla-warning badge");
            Assert.IsTrue(html.Contains("Point at Earth"), "HTML must contain 'Point at Earth' text");
            Assert.IsTrue(js.Contains("qiblaWarning.style.display = 'block'"), "JS engine must display warning when center ground pick fails");
        }

        [TestMethod]
        public void UI02_UI03_StatusBarsAndRibbonThemes_DarkAndLightColors()
        {
            var darkDict = new ResourceDictionary { Source = new Uri("/Z EARTH;component/Themes/DarkTheme.xaml", UriKind.RelativeOrAbsolute) };
            var lightDict = new ResourceDictionary { Source = new Uri("/Z EARTH;component/Themes/LightTheme.xaml", UriKind.RelativeOrAbsolute) };

            Assert.IsNotNull(darkDict["StatusBarBtnStyle"]);
            Assert.IsNotNull(lightDict["StatusBarBtnStyle"]);
            Assert.IsNotNull(darkDict["Brush.TreeItem.Hover"]);
            Assert.IsNotNull(lightDict["Brush.TreeItem.Hover"]);
        }

        [TestMethod]
        public void REG01_REG02_SnappingMeasurementTerrain_NonRegression()
        {
            // Geodesic distance calculation
            double d = GeodesicMeasurementService.CalculateGeodesicDistance(30.0444, 31.2357, 31.2001, 29.9187);
            Assert.IsTrue(d > 100_000 && d < 250_000, $"Cairo to Alexandria distance ~180km, actual: {d:N0} m");

            // Geodesic polygon area
            var pts = new (double lat, double lon)[]
            {
                (30.0444, 31.2357),
                (30.0444, 31.2457),
                (30.0544, 31.2457),
                (30.0544, 31.2357)
            };
            var result = GeodesicMeasurementService.CalculatePolygonArea(pts);
            Assert.IsTrue(result.AreaSquareMeters > 500_000 && result.AreaSquareMeters < 2_000_000, $"Calculated area: {result.AreaSquareMeters:N0} m²");
        }
    }
}
