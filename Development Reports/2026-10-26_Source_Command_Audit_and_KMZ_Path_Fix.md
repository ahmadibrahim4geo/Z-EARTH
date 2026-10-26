# Z EARTH — Source command audit and KMZ extraction containment fix
Date: 2026-10-26  
Repository: `ahmadibrahim4geo/Z-EARTH`  
Target branch: `main`  
Scope: **T0 source inspection + CI-based T1/T2 only**. Not a whole-product T3/T4 certification.

## Verified source observations
- Inspected `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.cs`, `GlobeBridge.cs` (earlier inspection) and `cesium-engine.js` (dispatch inventory).
- MainWindow has **130** matching Button/ToggleButton/RadioButton/MenuItem opening elements. This is a text inventory, not an assertion of 130 individually working UI controls.
- Found **79** distinct command binding expressions normalizing to **74** unique command names. **0** have no textual ViewModel declaration/assignment match; this is not a functional UI test.
- Found **3** named XAML click handlers, of which **0** lack a textual match in `MainWindow.xaml.cs`.
- Found **54** literal host action cases in the CesiumJS command dispatcher. Actual WPF → WebView2 → Cesium state responses require real T3 runtime validation.

## Confirmed defect — KMZ ZipSlip containment case comparison
**Location:** `src/ZEarth.Domain/Kml/KmzService.cs`, `KmzService.ExtractAndParse`.  
**Cause:** An unconditional case-insensitive path prefix comparison treated Linux paths like `Archive/` and `archive/` as equivalent. A malicious archive entry such as `../archive/escape.txt` could escape the intended `Archive/` extraction root on case-sensitive filesystems.  
**Fix:** Use `StringComparison.OrdinalIgnoreCase` on Windows and `StringComparison.Ordinal` elsewhere when verifying resolved extraction paths.  
**Regression:** `KmzExtractionRejectsCaseVariantSiblingTraversalOnLinux` creates a malicious archive and asserts rejection with no escaped output. Linux-specific regression is not executed by Windows-only CI; it requires a Linux test run to directly prove the old exploit is blocked.  
**Commits:** [implementation](https://github.com/ahmadibrahim4geo/Z-EARTH/commit/eaf2c2ad793cf3ac7cdeaa399cee7b417e7bfad7), [test](https://github.com/ahmadibrahim4geo/Z-EARTH/commit/d2e8d5aa0ae49db5129353d36094afc59dd4863a).  
**CI:** [Windows build and unit-test workflow](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/runs/40288149228). At time of preparing this report: static checks passed; Windows compile and unit test stages passed; artifact upload in progress.

## Bound command inventory — source evidence only
- `AboutCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `AddFolderCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `AddImageryLayerCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `AddPlacemarkCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `AddSavedSceneCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ClearMeasurementCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ClearSearchCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `CollapseAllSectionsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `CoordinateConverterCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DeletePlaceCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DiagnosticsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DrawCircleCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DrawLineCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DrawPlacemarkCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DrawPolygonCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `DrawRectangleCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ElevationProfileCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ExpandAllSectionsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ExportAllMyPlacesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ExportKmlCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ExportKmzCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ExportSelectedItemCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `FindMyLocationCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `FitSelectionCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `FlyToPlaceCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `FlyToSearchResultCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `FullExtentCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `GridOptionsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `HistoricalImageryCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `HomeCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ImportKmlCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ImportVectorCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `LocateCoordinatesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `MeasureAreaCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `MeasureDistanceCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `Model3DCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `MoveSavedSceneDownCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `MoveSavedSceneUpCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `NewProjectCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `NextExtentCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `OpenBasemapGalleryCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `OpenProjectCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `OpenPropertiesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `OptionsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `OverlayImageCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `PreviousExtentCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `RedoCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ResetNorthCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ResetPitchCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ResetVerticalExaggerationCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SaveAsProjectCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SaveProjectCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SaveSearchResultToMyPlacesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SaveToMyPlacesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SearchByNameCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SelectCrsCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `Set2DModeCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `Set3DModeCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SetAsStartupSceneCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SetCompassModeCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `SetVerticalExaggerationCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `TakeScreenshotCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleAtmosphereCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleCoordinateFormatCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleGraticulesCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleLanguageCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleSidebarCollapseCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleSidebarDockCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleSunlightCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleTerrainCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ToggleThemeCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `UndoCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ZoomInCommand` — declaration/assignment textually located; behavior **not runtime verified**
- `ZoomOutCommand` — declaration/assignment textually located; behavior **not runtime verified**

## Cesium host action inventory — dispatcher cases present (not runtime verified)
`app.setTheme`, `camera.fitSelection`, `camera.flyTo`, `camera.fullExtent`, `camera.get`, `camera.home`, `camera.resetNorth`, `camera.resetPitch`, `camera.setZoomFactor`, `camera.setZoomSensitivity`, `camera.zoomIn`, `camera.zoomOut`, `compass.setMode`, `credits.setConfig`, `crs.setMapCrs`, `device.requestLocation`, `draw.cancel`, `draw.finish`, `draw.start`, `environment.setAtmosphere`, `environment.setSunlight`, `environment.setTimeOfDay`, `historical.error`, `historical.loading`, `historical.show`, `layers.add`, `layers.addRasterOverlay`, `layers.registerBasemap`, `layers.remove`, `layers.removeRasterOverlay`, `layers.reorder`, `layers.setBasemap`, `layers.setBorders`, `layers.setGraticules`, `layers.setGraticulesConfig`, `layers.setOpacity`, `layers.setVisibility`, `layers.toggleHybridLabels`, `measure.clear`, `measure.finish`, `measure.start`, `placemarks.set`, `reference.setLayer`, `reference.setOpacity`, `request.cancel`, `scene.setMode`, `search.setPin`, `snap.setConfig`, `swipe.start`, `swipe.stop`, `terrain.resetExaggeration`, `terrain.setExaggeration`, `terrain.setProvider`, `units.set`

## Remaining acceptance gates (not verified here)
1. T3: Windows GPU/WebView2 interactive globe with actual map tiles and error-state handling.
2. T3: click every ribbon item, keyboard shortcut, context menu, tooltip and icon; record state changes, real map effects and response/error behavior.
3. T3: scene switching, atmosphere/sunlight/time, graticules, source availability, terrain relief and Wayback timeline.
4. T3: end-to-end KML/KMZ, selected export, nested styles/icons, project save/reopen and camera continuity.
5. T4: test portable preview on a clean Windows machine/profile with crash logs, memory responsiveness and screenshot evidence.

## Integrity statement
Green .NET tests / FakeGlobeEngine do **not** establish that buttons work on real WebView2 or that map/terrain providers serve valid tiles. No claim is made that every icon/command was behaviorally tested or repaired. No additional skill dependency has been demonstrated as missing for this limited fix; existing KMZ/security and verification skills cover it.
