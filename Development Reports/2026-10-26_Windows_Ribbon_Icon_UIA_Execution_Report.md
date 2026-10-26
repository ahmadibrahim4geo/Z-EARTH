# Z EARTH — Native Windows Ribbon/Icon UI Automation Execution Report
Date: 2026-10-26
Branch: `main`
Build: Windows .NET 10 WPF portable preview (GitHub Actions)
Scope: MainWindow WPF ribbon/toolbar/button/radio controls; not full dialog or HTML Cesium HUD certification.

## 1. Reproducible execution evidence
**Latest CI run:** https://github.com/ahmadibrahim4geo/Z-EARTH/actions/runs/40291062257
**UIA evidence (JSON + 23 real 1024×720 PNGs):** https://github.com/ahmadibrahim4geo/Z-EARTH/actions/runs/40291062257/artifacts/12635873762

The latest run reports: static checks **passed**; Windows .NET WPF compile **passed**; .NET tests **passed**; portable preview built **passed**; real Windows UI Automation harness **completed successfully**. The script uses `UIAutomationClient` to enumerate and invoke controls against the running `Z EARTH.exe`; these are NOT simulated mock-only unit tests.

| Native UIA measurement | Value |
| --- | ---: |
| MainWindow identified WPF Button/ToggleButton/RadioButton sources | 109 |
| MainWindow UIA controls discovered across all 10 ribbon tabs and sidebar | 102 |
| Controls successfully invoked via Windows UIA | 37 |
| Controls discovered but not invoked (modal, unsafe, fixture-gated, etc.) | 65 |
| Controls not discovered in UIA tree in current session | 7 |
| Actual per-command functional outcomes automatically confirmed | **0** |
| Captured screenshots | 23 |
| Main window inside physical Windows work area | **true**, both work area and window [0,0,1024,720] |
| Windows UIA script outcome | `UIA_COMPLETED_NOT_FULL_FUNCTIONAL_CERTIFICATION` |

The 37 invoked controls are not 37 functionally certified controls. UIA InvokePattern/SelectionItemPattern proves a native gesture was dispatched, but verifying camera, scene mode, view state, imagery provider, saved project, dialogs, or KML outcomes requires state/visual assertions and fixtures. **Do not equate 37 invocations, green CI, or presence of screenshots with all 109 fully working.**

### 7 UIA controls not discovered
- `ZE.Main.079.ToggleSidebarCollapseCommand`
- `ZE.Main.088.LocateCoordinatesCommand`
- `ZE.Main.089.FindMyLocationCommand`
- `ZE.Main.091.FlyToSearchResultCommand`
- `ZE.Main.092.SaveSearchResultToMyPlacesCommand`
- `ZE.Main.106.FlyToPlaceCommand`
- `ZE.Main.107.ToggleSidebarCollapseCommand`

These are **not automatically failed**: visibility, selected ribbon tab, conditional templates, virtualization and collapsed sidebar can hide controls. They need scenario-specific native tests. No UIA exception is reported for the 37 successful invocations.

### 10 native ribbon tabs traversed
Project, Navigation, View, Drawing, Measurement, Basemap, Historical Imagery, Terrain, Tools, Help.

## 2. Observed UI/UX issues and implemented main-branch fixes
1. **Window larger than small Windows desktop (confirmed, fixed)** — the initial run showed part of the title/ribbon outside 1024×720; `MainWindow.FitInitialWindowToWorkArea` now clamps the launch geometry. Subsequent native UIA report confirms `WindowFullyVisible=true`.
2. **Ribbon controls unreadable in Light theme (confirmed, fixed)** — source theme resources used static foreground resources; replacing relevant button/text `StaticResource` with `DynamicResource` allowed foreground updates after theme changes. Screenshots visibly show readable controls on Light UI.
3. **Missing accessible names for icon buttons (confirmed, fixed)** — 109 stable `AutomationProperties.AutomationId` and readable `AutomationProperties.Name` added to the main-window interactive controls; on a separate native Windows run 103 discovered controls all had accessible names.
4. **Far-right ribbon buttons clipped on narrow windows (confirmed, fix deployed)** — each of 10 ribbon tab contents wrapped in a horizontal ScrollViewer. Latest Windows build/tests and UIA completed; screenshot `tab-02.png` shows horizontal ribbon scroll bar. Manual scroll-to-end control hit testing is still a follow-up test.

## 3. Runtime visual findings — NOT universal functional pass
- Native Windows screenshots show running Cesium WebView2, real 2D map / globe, grid overlays, light/dark switching, relocated RTL sidebar and Sunlight HUD. These verify that the renderer/individual visual states appeared in at least one captured session, **not** that each of 37 commands produced its expected response.
- **Partial/false success: Import 3D Model:** `ExecuteModel3D` records a placemark with `ModelPath` then shows success but the examined execution path does not prove an actual Cesium 3D model is loaded. Tracked at https://github.com/ahmadibrahim4geo/Z-EARTH/issues/15.
- **Partial: Arabic language:** RTL and Arabic window title appear, but screenshots show English ribbon tabs, buttons, HUD, status and explorer text remaining. Tracked at https://github.com/ahmadibrahim4geo/Z-EARTH/issues/16.
- Some popovers (Sunlight HUD) can overlap compass controls on 1024px test viewport; follow-up layout responsiveness checks should exercise alternate DPI and widths before assigning defect status.

## 4. Changes deployed directly to `main` for this audit
- `src/ZEarth.App/Views/MainWindow.xaml`: stable UIA IDs and names for 109 controls; all ten ribbon tab content presenters horizontally scrollable.
- `src/ZEarth.App/Views/MainWindow.xaml.cs`: work-area aware launch window fit.
- `src/ZEarth.App/Themes/DarkTheme.xaml` and `LightTheme.xaml`: dynamic button/text brush updates.
- `scripts/audit-ribbon-icons.ps1`: real WPF UI Automation enumerator, safe invocation, error reasons, physical window bounds, evidence images and machine-readable JSON.
- `.github/workflows/build-and-test.yml`: runs UIA after WPF build and packages evidence artifact. UIA currently `continue-on-error:true` because headless/interactive CI availability varies; inspect JSON before claiming completion.

## 5. Still unverified
- 65 discoverable but deliberately not invoked buttons (dialogs, destructive commands, flows requiring KML/KMZ fixtures and selection state).
- 7 contextual/hidden items not discovered in the UIA session.
- Per-command WebView2 → Cesium request acknowledgement **and observable scene-effect** assertions.
- 25 HTML HUD buttons plus controls in dialogs/options/properties, context menu, icons' visual contrast across all languages/themes.
- Real data sources, actual elevations, on-disk save/load, file format integrity, crash/recovery, multi-DPI and packaged device acceptance (T4).

## 6. Repeat
Use a Windows graphical desktop with .NET 10/WPF/WebView2:
```powershell
dotnet restore ZEarth.sln
dotnet build ZEarth.sln -c Release --no-restore
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release --no-build
./scripts/create-portable-preview.ps1 -TestsAlreadyPassed
./scripts/audit-ribbon-icons.ps1 -ExePath 'dist/Z EARTH-Preview/Z EARTH.exe' -OutputDir 'ui-icon-evidence' -InvokeSafe -CaptureScreenshots
```
Read `icon-uia-results.json`; screenshot presence, UIA invocation, and fully verified GIS functionality are distinct outcomes. No T4 acceptance claimed.

## 7. Follow-up skill assessment
The project already bundles `zearth-verification`, `zearth-desktop-ux`, `zearth-globe-bridge`, `gis-security-guardian`, and desktop GIS skills. No new skill is required to **identify** the issues, but GIS Development Skills would benefit from a reusable **Windows Desktop UIA → WebView2/Cesium action-outcome fixture harness** that binds each button ID to a deterministic expected bridge response, scene state, screenshot delta and negative test. That skill enhancement itself is a future task, not a completed implementation in GIS Development Skills.
