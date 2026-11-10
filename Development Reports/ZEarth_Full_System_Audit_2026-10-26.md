# Z EARTH Desktop — Full System Audit & Skills Validation

**Audit window:** 26–27 October 2026 (Africa/Cairo)  
**Requested report filename:** `ZEarth_Full_System_Audit_2026-10-26.md`  
**Scope:** diagnostic audit only; no application behavior or production source was changed  
**Repository condition:** the supplied folder is a ZIP/local working copy without `.git`; branch, commit, remote synchronization, and a clean/dirty Git state therefore cannot be proved  
**Evidence convention:** T0 source review, T1 static/syntax/build, T2 automated unit/fixture test, T3 observed native Windows WPF/WebView2 behavior, T4 packaged deployment/installer acceptance

## 1. Executive Summary

Z EARTH is a substantial Windows desktop GIS application with a real WPF shell, MVVM command surface, a WebView2 bridge, and a CesiumJS globe. The audit found a buildable codebase and a functioning installed portable preview, but the project is **not release-ready** because the authoritative MSTest gate has three failures and several high-value workflows remain unverified at T3/T4.

The strongest positive evidence is:

- the pinned skills snapshot validates: 30 skills, 76 referenced resources, one canonical router, no duplicate active specialist set;
- Release restore and build completed successfully outside the restricted sandbox (0 build errors; two NU1900 warnings because NuGet vulnerability metadata was unreachable);
- all 44 Node tests passed and `cesium-engine.js` passed syntax checking;
- the native portable application displayed a real Cesium/WebView2 globe with hosted imagery, and the following commands produced observable effects: Home, Full Extent, 2D, 3D, theme toggle, and coordinate-grid toggle;
- terrain failure was truthful: the UI reported that a Cesium ion token is required rather than claiming terrain was active;
- the offline illustrative cloud command accurately labels itself as non-weather data.

The release blockers and material defects are:

1. **Three MSTest failures (240/243 passed).** One is a timezone-dependent Esri Wayback release-date parse defect. Two are dispatcher/timer test-contract failures in center/cursor telemetry.
2. **CRS status contradiction observed at T3.** The status bar displayed `EPSG:4326 (WGS 84)` while the center readout was projected Easting/Northing in metres. Source inspection proves that `UpdateTelemetryStrings()` overwrites `StatusCrs` from coordinate-display format while center/cursor formatting uses `Settings.MapCrsEpsg`.
3. **Arabic UI is incomplete.** RTL mirroring and the Arabic window title work, but most tabs, commands, descriptions, places labels, and status text remain English. Mirroring the whole window also reverses the Latin brand arrangement.
4. **3D model import is a placeholder with a success message.** It stores `ModelPath` on a placemark and calls the normal places refresh; no model-loading bridge/engine path was found.
5. **Documentation/routing drift remains.** Several current docs still link to removed `zearth-*` skills. The feature parity matrix also says no flight mode exists although a current flight command and bridge path do exist.
6. **No canonical Preview was generated.** `scripts/build-and-test.ps1` was intentionally not run after the MSTest gate failed; manufacturing a release would violate project policy.

The installed/downloaded portable preview used for T3 evidence was:

`C:\Users\ahmad\Downloads\Z-EARTH-v2.2.4-win-x64-portable-preview\Z EARTH.exe`

Because the audited workspace has no Git identity and that executable lives outside the workspace, the runtime evidence proves behavior of that installed preview, **not byte identity with the current source tree**.

## 2. Project Architecture Assessment

### 2.1 Main components

| Component | Responsibility | Audit result |
|---|---|---|
| `src/ZEarth.Domain` | domain models, CRS registry/transforms, KML/project data contracts | Real implementation; exercised substantially by MSTest, with three suite failures elsewhere |
| `src/ZEarth.Globe` | bridge contracts and packaged Cesium/Web assets | Real request/response bridge and local `https://cesium.local/` content; Node tests pass |
| `src/ZEarth.App` | WPF shell, ribbon, dialogs, view models, settings, project services | Real application; Release build passes and portable preview runs |
| `tests/ZEarth.Tests` | MSTest coverage for domain, bridge, persistence, UI contracts | 243 tests discovered; 240 pass, 3 fail |
| `tests/*.test.cjs` | Node tests for engine, repository command contracts, KML/KMZ behavior | 44/44 pass |
| `.github/skills` | pinned development-agent instruction set | 30 active skills, validated snapshot |
| `scripts` | build/test, skills validation, reference-lab and UI/static audit helpers | Useful but with noted Windows encoding/environment limitations |

### 2.2 Cross-layer command chain

The intended executable chain is present:

`MainWindow.xaml binding/event` → `MainViewModel command` → `GlobeBridge request/response` → `cesium-engine.js action` → `Cesium scene effect` → `bridge event` → `WPF state/status`.

This is a sound architecture for a hybrid WPF/Cesium application. The main weakness is not absence of layers but uneven end-to-end proof. A fake globe engine can prove message shape at T2, but cannot prove WebView2 rendering, hosted imagery, GPU behavior, file pickers, provider failure handling, or persisted user workflows.

### 2.3 State, persistence, and trust boundaries

- Settings, active CRS, language, theme, grid options, terrain state, and related preferences are persisted through `SettingsService`.
- Places/project data use a domain model and SQLite-backed project storage. KML/KMZ has dedicated import/export code and security tests.
- WebView messages cross a WPF/WebView2 trust boundary. Current tests cover command contracts, request failures, and several source restrictions, but native failure injection was not run.
- Public Esri, OSM, OpenTopo, NASA GIBS, Photon, and Cesium services remain external dependencies with availability, quota, license, and attribution constraints.
- The elevation-profile path does not invent terrain: it requests active numeric terrain and falls back only to explicitly stored vertex Z with an explanatory message.

## 3. Available Skills & Routing Validation

### 3.1 Snapshot and routing result

`scripts/validate-agent-skills.py` passed using the bundled Python runtime:

- 30 pinned skills;
- 76 manifest references;
- single `gis-development` router;
- no duplicate active `zearth-*`/`cesiumjs-*` skill folders;
- links and XAML checks completed;
- native runtime proof correctly remained outside the validator's claims.

The Windows PATH command `python` is an unusable Microsoft Store stub on this machine. Audit scripts were therefore run with the bundled Codex Python runtime. The intent router produced lexical hints, and its 16 tests passed. The development journal was initialized and a truthful audit operation was recorded.

### 3.2 Complete active skill inventory

| Skill | Primary role in this repository | Audit use/result |
|---|---|---|
| `gis-development` | single routing entry point | Used; routing model is coherent |
| `prompt-intent-orchestrator` | scope, ambiguity, risk, and review routing | Used for the multi-stage audit |
| `agent-development-journal` | local durable operation/test record | Used; journal works, one symlink test needs Windows privilege |
| `completion-evidence` | T0–T4 DoneProof discipline | Used; prevents build-only success claims |
| `gis-security-guardian` | sole security specialist | Used; local security audit passed with correct `.github` root |
| `gis-desktop-application-development` | WPF/MVVM desktop design and lifecycle | Used for architecture/build review |
| `gis-ui-command-audit` | control-to-command-to-effect trace | Used; static scanner and native UI tree inspected |
| `gis-desktop-e2e-testing` | native desktop/WebView acceptance | Used; real Windows preview exercised |
| `gis-data-flow-diagnostics` | input→processing→render→persistence tracing | Used for CRS, Wayback, places, and project flows |
| `gis-ui-ux-design-testing` | visual/interaction/usability review | Used for ribbon, themes, Arabic, states, and messages |
| `gis-internationalization-arabic` | Arabic, RTL, Unicode, locale behavior | Used; partial implementation confirmed at T3 |
| `geospatial-web-mapping` | Cesium/web mapping architecture | Used for engine and provider review |
| `geospatial-api-integration` | provider/API failure and contract handling | Used for Wayback, Photon, terrain, GIBS analysis |
| `gis-device-context` | geolocation/device context | Used to scope permission-sensitive location tests; no permission was granted |
| `system-capabilities-and-permissions` | host capability and permission gates | Used for sandbox, symlink, network, and native-test limits |
| `kml-kmz-engineering` | KML/KMZ preservation and security | Used; automated tests pass, native round trip not run |
| `coordinate-reference-systems` | CRS transforms and coordinate display | Used; exposed the CRS status contradiction |
| `geospatial-data-processing` | import/transform/export pipelines | Used for vector/overlay/data-flow review |
| `terrain-hydrology-lidar` | terrain/elevation truthfulness | Used; no fabricated DEM claim made |
| `gis-cartography-symbology` | symbols, labels, visual hierarchy | Used for KML/icon/grid review |
| `geospatial-data-catalogs` | catalog metadata and source structure | Available; indirectly relevant to provider catalogs |
| `geospatial-data-discovery` | dataset/source discovery | Available; no new provider procurement was required |
| `remote-sensing-pipelines` | imagery/remote-sensing provenance | Available; applied conceptually to Wayback/GIBS provenance |
| `spatial-databases` | SQLite/spatial persistence | Available; project storage tests reviewed |
| `geospatial-ai-agents` | agentic geospatial workflows | Available; not needed for runtime product verification |
| `arcgis-toolbox-engineering` | ArcGIS toolbox development | Available; outside current application scope |
| `global-mapper-development` | Global Mapper integrations | Available; outside current application scope |
| `qgis-engine-integration` | QGIS engine integration | Available; no QGIS runtime integration is claimed |
| `qgis-plugin-development` | QGIS plugin work | Available; outside current application scope |
| `gis-mobile-application-development` | mobile GIS | Available; outside this Windows desktop scope |

### 3.3 Skills tooling defects and drift

- `.github/scripts/security_audit.py --root .github` passed with 30 skills and zero local findings. Running it against `.` is the wrong invocation and falsely reports the registry missing.
- `.github/scripts/audit_skill_graph.py --root .github` fails three exact-string checks because `.github/AGENTS.md` uses human-readable names instead of the literal IDs `prompt-intent-orchestrator`, `gis-security-guardian`, and `agent-development-journal`. This is a graph-auditor/document wording mismatch; the checksum validator still passes.
- Full `.github` Python tests: 87 executed, 86 pass, one errors before its assertion because creating a directory symlink requires an unavailable Windows privilege (`WinError 1314`). This is an environment capability gap, not evidence that unsafe symlinks are accepted.
- Some authoritative product docs still link to removed local skills. Those links must route to the current 30-skill set and canonical references.

## 4. Previous Reports Verification

| Previous report | Current verification | Status |
|---|---|---|
| `2026-10-25_Grid_Terrain_Sunlight_CRS_UI_Implementation.md` | Grid/terrain/sunlight/CRS source paths still exist. Grid toggle was observed at T3. Terrain truthfully remained off without a token. Sunlight and detailed grid polar behavior were not exercised natively. | Partially verified |
| `2026-10-26_GIS_Single_Source_Retire_Duplicate_Skills.md` | Exactly 30 active skills and one router validate. No removed duplicate skill folders were found. Several docs still reference those removed skills. | Implementation verified; documentation cleanup incomplete |
| `2026-10-26_GIS_Skills_Upstream_Main_Sync.md` | Manifest validation passes for the pinned commit and 76 references. Full helper tests have one Windows symlink-privilege error. | Mostly verified |
| `2026-10-26_Source_Command_Audit_and_KMZ_Path_Fix.md` | Repository command-contract and Node KML tests pass. Native KML/KMZ file-dialog, rendering, save, reopen, and byte inspection were not performed. | T1/T2 verified; T3 not verified |
| `2026-10-26_Terrain_Grid_Sunlight_Repair.md` | Node tests pass and grid state visibly changes. Terrain numeric relief and sunlight/time behavior still lack current T3 evidence. | Partially verified |
| `2026-10-26_Windows_Ribbon_Icon_UIA_Execution_Report.md` | Current native audit independently confirms a live WPF/WebView2 window, 10 ribbon tabs, unique MainWindow AutomationIds, working theme and 2D/3D commands, and incomplete Arabic. The 3D-model placeholder remains. | Core observations reconfirmed |

No previous report was treated as proof by itself. The local folder has no Git history, so statements about historical PRs or GitHub `main` cannot be tied cryptographically to this copy.

## 5. Ribbon & Commands Audit Matrix

### 5.1 Inventory summary

- 10 ribbon tabs observed natively: Project, Navigation, View, Drawing, Measurement, Basemap, Historical Imagery, Terrain, Tools, Help.
- 116 unique `AutomationProperties.AutomationId` values in `MainWindow.xaml`.
- 81 unique WPF `Command` bindings across the XAML views.
- Static scanner result: 18 files, 229 controls, 161 declared bindings, 43 no-binding controls, 25 uninspected. The 43 include legitimate style elements, code-behind controls, dialog buttons, and state checkboxes; the count is not equivalent to 43 broken commands.
- The scanner initially failed under Windows code page 1256 on a Unicode arrow. Setting `PYTHONIOENCODING=utf-8` made it complete. This is a tooling portability defect.

### 5.2 Per-command classification

Only an observed effect can earn **Working**. T1/T2-only coverage is retained as evidence but remains **Not Tested** in this user-facing runtime classification.

| Area | Commands | Status | Evidence/notes |
|---|---|---|---|
| Navigation | `HomeCommand`, `FullExtentCommand` | Working | T3: camera moved from regional view to full globe (eye 22,000 km), then Home returned to Egypt/Nile region (eye 5,356 km) |
| Projection | `Set2DModeCommand`, `Set3DModeCommand` | Working | T3: Cesium visibly morphed to planar 2D and back to spherical 3D |
| Appearance | `ToggleThemeCommand` | Working | T3: dark/light resources changed visibly; dark restored |
| Grid | `ToggleGraticulesCommand` | Working | T3: status reported enabled/hidden and scene grid visibility changed; original hidden state restored |
| Language | `ToggleLanguageCommand` | Partially Working | T3: title and RTL changed, but most UI remained English and Latin branding was visually reversed; English restored |
| Clouds | `ToggleCloudsCommand` | Partially Working | T3: command/state/status worked and described an offline illustrative layer; distinct cloud rendering could not be visually established at tested altitude; disabled afterward |
| CRS | `SelectCrsCommand` | Partially Working | T3/T0: projected centre metres were shown beside `EPSG:4326`; root cause proved in telemetry status code. Dialog selection itself was not changed during audit |
| Historical imagery | `HistoricalImageryCommand` | Partially Working | T2 defect: date-only release `2023-12-07` became 02:00 under Cairo; live catalog/tile timeline not exercised |
| 3D models | `Model3DCommand` | Placeholder | T0: stores a file path on a placemark and displays success, with no bridge/engine model rendering path |
| Project/file | `NewProjectCommand`, `OpenProjectCommand`, `SaveProjectCommand`, `SaveAsProjectCommand`, `UndoCommand`, `RedoCommand`, `ImportKmlCommand`, `ImportGpxCommand`, `PlayGpsTrackCommand`, `StopGpsTrackCommand`, `ExportKmlCommand`, `ExportKmzCommand` | Not Tested | Bound to real handlers and partly T2-covered; native file/persistence effects were not exercised |
| Navigation/camera | `FitSelectionCommand`, `PreviousExtentCommand`, `NextExtentCommand`, `ResetNorthCommand`, `ResetPitchCommand`, `ZoomInCommand`, `ZoomOutCommand`, `ToggleFlightModeCommand`, `SetCompassModeCommand` | Not Tested | Present and wired; no complete native effect/recovery test in this audit |
| Drawing/overlay | `DrawPlacemarkCommand`, `DrawLineCommand`, `DrawPolygonCommand`, `DrawRectangleCommand`, `DrawCircleCommand`, `OverlayImageCommand` | Not Tested | Real handlers/bridge paths exist; geometry creation and persistence not observed |
| Measurement | `MeasureDistanceCommand`, `MeasureAreaCommand`, `ClearMeasurementCommand`, `ElevationProfileCommand` | Not Tested | Automated/source evidence exists; no native numeric comparison or profile-source verification performed |
| View/panels | `OpenBasemapGalleryCommand`, `GridOptionsCommand`, `ToggleSunlightCommand`, `ToggleAtmosphereCommand`, `ToggleCoordinateFormatCommand`, `ToggleSidebarCollapseCommand`, `ToggleSidebarDockCommand`, `ExpandAllSectionsCommand`, `CollapseAllSectionsCommand` | Not Tested | Grid options source supports labels, key lines, UTM zones, and independent lat/lon spacing; dialog behavior not tested natively |
| Imagery | `AddImageryLayerCommand`, `NasaHistoricalImageryCommand`, `ClearNasaHistoricalImageryCommand` | Not Tested | Provider/network/render behavior not tested live |
| Terrain | `ToggleTerrainCommand`, `SetVerticalExaggerationCommand`, `ResetVerticalExaggerationCommand` | Not Tested | Terrain remained truthfully unavailable without token; numeric terrain and 1x/3x relief were not available for acceptance |
| Tools | `ImportVectorCommand`, `CoordinateConverterCommand`, `TakeScreenshotCommand`, `DiagnosticsCommand`, `OptionsCommand`, `AboutCommand` | Not Tested | UI bindings and handlers exist; dialogs/results not exercised |
| Search/location | `ClearSearchCommand`, `SearchByNameCommand`, `LocateCoordinatesCommand`, `FindMyLocationCommand`, `FlyToSearchResultCommand`, `SaveSearchResultToMyPlacesCommand` | Not Tested | Photon/device permission paths were not invoked; no permission was granted and no provider success is claimed |
| Places/scenes | `AddPlacemarkCommand`, `AddFolderCommand`, `AddSavedSceneCommand`, `DeletePlaceCommand`, `OpenPropertiesCommand`, `MoveSavedSceneUpCommand`, `MoveSavedSceneDownCommand`, `SetAsStartupSceneCommand`, `ExportAllMyPlacesCommand`, `ExportSelectedItemCommand`, `FlyToPlaceCommand` | Not Tested | Real source and T2 persistence coverage exist; native create/save/reopen was not run |
| Gallery/favorites | `ToggleFavoriteCommand` | Not Tested | Bound in a view; no user-visible persistence test performed |

### 5.3 Statistical classification

| Classification | Existing unique commands | Meaning in this audit |
|---|---:|---|
| Working | 6 | Observable T3 effect confirmed |
| Partially Working | 4 | Some real behavior confirmed, but a defect or missing visual proof remains |
| Broken | 0 | No whole command was proven completely nonfunctional; confirmed defects are documented separately |
| Placeholder | 1 | UI reports completion without the required engine implementation |
| Not Tested | 70 | Source/T1/T2 evidence does not qualify as native functional proof |
| **Total existing command bindings** | **81** | Sum of the above categories |
| Missing expected command families | 3 | Separate from the 81 existing commands |

Missing command families/capabilities found against the requested audit scope:

1. separate meridian visibility and parallel visibility switches (spacing is independent, but visibility is common);
2. tour recording/playback comparable to a Places tour workflow;
3. general world-time playback; current time control is sunlight-specific and imagery timelines are provider-specific.

An intentionally conservative **runtime verification completion** is:

`(Working + 0.5 × Partially Working) / Existing Commands = (6 + 0.5 × 4) / 81 = 9.9%`.

This is not a claim that only 9.9% of the code is implemented. It means only 9.9% of the unique command surface earned positive native-runtime credit in this audit; 70 untested commands are deliberately not counted as successful.

## 6. GIS Engine Integration Assessment

### 6.1 Globe and navigation

- The portable preview loaded a real Cesium document at `https://cesium.local/index.html` inside WebView2.
- Hosted basemap imagery, labels, borders, camera controls, compass, and Cesium credits were visible.
- 2D/3D transitions completed after an animation delay of roughly 2–3 seconds.
- Home and Full Extent produced distinct, observable camera states.
- Pan, arbitrary rotate/tilt, previous/next extent, fit selection, and flight controls were not fully exercised.

### 6.2 Terrain and elevation

- Native status: `Terrain OFF — Cesium ion token is required for Cesium World Terrain`.
- This is correct negative behavior and does not pretend that the ellipsoid supplies elevation.
- No numeric DEM source was available, so mountain relief, vertical exaggeration, terrain sampling, and profile values remain T3 **Not Tested**.
- Source inspection confirms that the profile command requests active terrain and otherwise uses only explicitly stored path Z; no fictitious terrain generator was found.

### 6.3 Coordinate grid

- The grid global toggle works at T3 and updates visible status.
- The options dialog provides label toggle, major-only mode, named key lines, UTM zones, and independent latitude/longitude spacing.
- Separate on/off visibility for all meridians versus all parallels is not exposed.
- Polar behavior, antimeridian continuity, rotated-globe labels, and every option combination remain T3 Not Tested.

### 6.4 Lighting, atmosphere, clouds, and imagery time

- Sunlight and atmosphere have explicit commands and bridge paths, but no current native visual comparison was made.
- Clouds are explicitly an offline illustrative layer, not live weather. Toggle/status behavior was observed; a visually distinct cloud field was not conclusively established.
- The Esri Wayback design correctly distinguishes release dates from acquisition dates and refuses fabricated fallback releases. Its date parser is timezone-sensitive and currently fails a Cairo test.
- NASA GIBS and Wayback hosted tiles were not accepted at T3, so provider availability and attribution cannot be claimed from this audit.

### 6.5 Providers and failure handling

The application has meaningful negative-state messages, especially for terrain. However, provider acceptance remains incomplete because the audit did not force DNS, timeout, HTTP, quota, certificate, or malformed-response failures in the native application. No hosted tiles were cached or redistributed.

## 7. Functional Test Results

| Test/action | Result | Evidence level |
|---|---|---|
| Skill snapshot validation | PASS: 30 skills, 76 references | T1 |
| Intent orchestrator tests | PASS: 16 | T2 |
| Full `.github` helper tests | 87 run; 86 pass, 1 environment error (`WinError 1314` symlink privilege) | T2 partial |
| Security audit with correct root | PASS: 30 skills, 0 local findings; external scanners not run | T1/T2 |
| Skill graph audit | FAIL: three exact-ID wording checks | T1 |
| Reference lab `--all` | R01–R08 PASS as `REFERENCE_METADATA_ONLY`; Z01–Z04 PASS as `ZE_SOURCE_ONLY`; runtime explicitly unverified | T0 |
| `dotnet restore ZEarth.sln` | PASS outside sandbox; NU1900 metadata warnings | T1 |
| `dotnet build ZEarth.sln -c Release --no-restore` | PASS: 0 errors, 2 NU1900 warnings | T1 |
| MSTest Release | FAIL: 240 passed, 3 failed, total 243 | T2 FAIL |
| `node --check src/ZEarth.Globe/Assets/cesium-engine.js` | PASS | T1 |
| `node --test tests/*.test.cjs` | PASS: 44/44 | T2 |
| Canonical `build-and-test.ps1` / Preview generation | NOT RUN after test failure; no release manufactured | Release gate FAIL |
| Native WPF/WebView2 start | PASS for external portable preview | T3 |
| Theme dark/light | PASS; dark restored | T3 |
| 2D ↔ 3D | PASS; 3D restored | T3 |
| Home / Full Extent | PASS with observable altitude/extent change | T3 |
| Coordinate-grid toggle | PASS; original hidden state restored | T3 |
| Arabic/RTL | PARTIAL; RTL/title work, most strings remain English; English restored | T3 |
| Clouds | PARTIAL; state/status work, distinct rendering not established; disabled afterward | T3 partial |
| Terrain | Correct negative state only; real terrain NOT TESTED | T3 negative |
| Packaged installer/fresh clone | NOT TESTED | T4 |

The portable preview was closed after testing. No project data was created or deleted, and settings touched during the audit were returned to English, dark theme, 3D, grid hidden, and clouds disabled.

## 8. UI/UX Assessment

### Strengths

- Dark gray is the primary style and is visually consistent across the main shell.
- The ribbon groups GIS functions in understandable categories.
- Native UI Automation exposes clear names and stable IDs for a large portion of the main surface.
- Disabled Undo/Redo and Previous/Next buttons visibly communicate state.
- WebView2 exposes useful controls and a Cesium credits/sources affordance.
- Provider limitations such as missing terrain credentials are communicated truthfully.

### Defects and usability risks

- Arabic is not complete: the whole window switches RTL, while most static XAML strings remain English. The localization dictionary contains more translations than the XAML actually consumes.
- Applying RTL at the root reverses the Latin `Z EARTH` brand layout and moves the Explorer/globe regions; brand elements need explicit LTR isolation.
- The live CRS label and coordinate units can contradict each other, which is especially dangerous in GIS work.
- The 3D model command presents a success dialog even though no model is rendered.
- The status bar can retain the last action message (for example clouds disabled) while unrelated navigation occurs, reducing feedback relevance.
- Full responsive testing at 100/125/150/200% DPI, narrow windows, multi-monitor placement, keyboard-only navigation, screen reader flow, and high-contrast mode was not performed.

Google Earth Pro reference materials were used only as behavior contracts. No Google binary, shader, copyrighted tile, or extracted resource was passed into implementation analysis.

## 9. KML/KMZ & Project Persistence Assessment

Source and automated tests provide meaningful confidence in:

- KML/KMZ parsing and export structure;
- zip/path traversal defenses;
- preservation-oriented handling of folders, Unicode/Arabic, schema data, and unknown XML;
- SQLite project save/load and Saved Scenes ordering/startup metadata;
- separation between My Places and Temporary Places.

But the following acceptance chain was not executed natively:

`file picker → import bytes → domain objects → Cesium rendering → edit/style → save project → close/reopen → SQLite readback → KML/KMZ export → inspect output bytes`.

Therefore KML/KMZ and project persistence are **T2-supported but T3 Not Tested**, not Working. The current no-Git ZIP also prevents comparison with the historical commit where the KMZ path repair was reportedly introduced.

## 10. Confirmed Bugs & Root Causes

### ZE-AUD-001 — CRS label contradicts projected coordinate readout

- **Component:** status telemetry / CRS
- **Severity:** High
- **Reproduction:** start the portable preview; observe centre readout in `X/E` and `Y/N` metres while the adjacent label says `EPSG:4326 (WGS 84)`.
- **Expected:** CRS label, units, centre, and cursor use the same active `MapCrsEpsg`.
- **Actual:** projected metres appear beside EPSG:4326.
- **Root cause:** `MainViewModel.UpdateTelemetryStrings()` sets `StatusCrs` from `SelectedCoordinateFormat`; `UpdateCenterCoordinates()` and `UpdateCursorCoordinates()` independently use `SettingsService.Instance.Settings.MapCrsEpsg`.
- **Files/functions:** `src/ZEarth.App/ViewModels/MainViewModel.cs`, `UpdateTelemetryStrings`, `UpdateCenterCoordinates`, `UpdateCursorCoordinates`, `ExecuteSelectCrs`.
- **Proposed fix:** make active map CRS the single source for `StatusCrs`; keep coordinate display format separate. Recompute all readouts atomically after either setting changes.
- **Required verification:** unit test with projected EPSG + DD/DMS format, dispatcher-aware telemetry test, and T3 selection/readout comparison.
- **Confidence:** High (T3 observation + direct source proof).

### ZE-AUD-002 — Wayback date-only values shift with local timezone

- **Component:** Esri Wayback catalog parsing
- **Severity:** High
- **Reproduction:** run MSTest in Africa/Cairo; expected `2023-12-07 00:00`, actual `2023-12-07 02:00`.
- **Expected:** a release-date label remains a calendar date without local-time shift.
- **Actual:** `DateTimeStyles.AssumeUniversal` converts the date to local Cairo time.
- **Root cause:** `WaybackService.TryParseDate()` uses `DateTime.TryParse(..., DateTimeStyles.AssumeUniversal, ...)` without `AdjustToUniversal` or exact date-only semantics.
- **Files/functions:** `src/ZEarth.App/Services/WaybackService.cs:114–118`; `ReferenceGridWaybackAcceptanceTests`.
- **Proposed fix:** parse known date-only labels with `TryParseExact("yyyy-MM-dd", ..., DateTimeStyles.None)` first, or use `DateOnly`; define UTC behavior for timestamps separately.
- **Required verification:** run under Cairo and UTC, plus malformed/untrusted catalog cases and live timeline date labels.
- **Confidence:** High (deterministic T2 failure + source proof).

### ZE-AUD-003 — Telemetry tests depend on an unpumped WPF DispatcherTimer

- **Component:** center/cursor/camera telemetry
- **Severity:** Medium
- **Reproduction:** run the full MSTest project; `CRS01_CRS03...` and `CRS02...` fail when a WPF `Application.Current.Dispatcher` exists but the test does not pump it.
- **Expected:** tests deterministically observe either an immediate update or an explicitly awaited/flushable coalesced update.
- **Actual:** events are placed in `_pendingCenter`/`_pendingCursor` and a 200 ms `DispatcherTimer`, leaving synchronous assertions stale.
- **Root cause:** production coalescing is selected solely by presence of `Application.Current?.Dispatcher`; tests share WPF application state and do not advance that dispatcher.
- **Files/functions:** `MainViewModel` bridge handlers, `EnsureTelemetryTimer`, `FlushPendingTelemetry`; `RibbonNavigationAddendumTests`.
- **Proposed fix:** inject a telemetry scheduler/clock or expose an internal deterministic flush for tests; avoid global `Application.Current` as the test-mode discriminator.
- **Required verification:** isolated and full-suite runs in multiple orders, a real WPF dispatcher test, and native rapid cursor movement.
- **Confidence:** High for the test-contract cause; Medium for any end-user performance impact because live readouts did update.

### ZE-AUD-004 — Arabic localization is only partially connected to XAML

- **Component:** localization/RTL
- **Severity:** High
- **Reproduction:** Tools → العربية (AR).
- **Expected:** complete Arabic UI with coherent RTL and protected LTR brand/technical tokens.
- **Actual:** Arabic window title and RTL layout appear, but most tabs/buttons/tooltips/places/status strings remain English; `Z EARTH` is visually reversed.
- **Root cause:** most XAML uses hard-coded English; the dictionary is not bound through dynamic resources, and root-level `FlowDirection` affects Latin branding.
- **Files/functions:** `MainWindow.xaml`, dialogs, `LocalizationService`, `MainViewModel` string properties.
- **Proposed fix:** resource dictionaries for en/ar, culture-aware bindings, explicit LTR islands for brand/EPSG/numbers/paths, and string-coverage tests.
- **Required verification:** screenshot matrix for both languages, RTL focus order, keyboard access, mixed Arabic/Latin text, and persistence after restart.
- **Confidence:** High (T3 screenshot/accessibility tree + source proof).

### ZE-AUD-005 — 3D model import reports success without rendering a model

- **Component:** Drawing → Model 3D
- **Severity:** High
- **Reproduction:** select a supported model file through the command.
- **Expected:** validated model is loaded by Cesium at a defined position/orientation/scale, or a truthful unsupported/error message appears.
- **Actual:** a normal placemark with `ExtendedData["ModelPath"]` is added; the standard places update is called; a success message says the model was imported.
- **Root cause:** no `GlobeBridge` model action or `cesium-engine.js` model entity path is connected.
- **Files/functions:** `MainWindow.xaml` model button; `MainViewModel.ExecuteModel3D()`.
- **Proposed fix:** either implement an independent licensed glTF pipeline with validation/error reporting or relabel/disable the command until implemented. Do not claim DAE/OBJ support without a real converter.
- **Required verification:** valid/invalid/missing-texture/large model cases, rendered screenshot, project reopen, and path portability/security tests.
- **Confidence:** High (direct source proof; consistent with prior audit).

### ZE-AUD-006 — Skills/docs retain dead routing links and stale feature claims

- **Component:** development documentation
- **Severity:** Medium
- **Reproduction:** search canonical docs for `zearth-`; inspect reference feature matrix flight row.
- **Expected:** all active instructions route through the 30-skill pinned set and current source facts.
- **Actual:** parity, historical imagery, KML acceptance, and older UI audit docs reference removed skills; the matrix says no flight mode exists.
- **Root cause:** skill consolidation did not update every downstream document/reference row.
- **Files:** `docs/GOOGLE_EARTH_PRO_PARITY.md`, `docs/HISTORICAL_IMAGERY_TIMELINE.md`, `docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md`, `docs/ZE_UI_FUNCTIONAL_AUDIT_2026-10-25.md`, `docs/google-earth-reference/FEATURE_PARITY_MATRIX.md`.
- **Proposed fix:** replace dead skill links with `gis-development` + relevant specialists and update matrix evidence without inflating runtime claims.
- **Required verification:** link checker, snapshot validator, skill graph audit, and source-to-matrix review.
- **Confidence:** High (T0/T1 search evidence).

### ZE-AUD-007 — Audit helpers are not fully Windows-portable

- **Component:** development tooling
- **Severity:** Low
- **Reproduction:** run UI static scanner under CP1256; run journal symlink test without SeCreateSymbolicLink privilege.
- **Expected:** UTF-8-safe output and explicit skip for unavailable symlink capability.
- **Actual:** scanner throws `UnicodeEncodeError`; one helper test errors before testing product logic.
- **Root cause:** output encoding assumption and missing capability guard.
- **Proposed fix:** force UTF-8 output in the script/runner and skip symlink-specific test with a precise reason when privilege is absent.
- **Required verification:** CP1256 and UTF-8 consoles; standard and Developer Mode/admin Windows.
- **Confidence:** High.

## 11. Missing Features & Incomplete Implementations

| Feature | Current state | Classification |
|---|---|---|
| Real 3D model rendering | Button and file picker exist; no Cesium model pipeline | Placeholder |
| Complete Arabic UI | RTL/title work; most strings remain English | Incomplete |
| Independent meridian/parallel visibility | Independent spacing exists; separate visibility does not | Missing |
| Tour recording/playback | Saved Scenes and basic flight exist, but no tour authoring/playback workflow | Missing |
| General world-time playback | Sunlight time control and imagery selectors exist; no general playback | Missing |
| Real weather clouds | Intentionally not provided; current clouds are offline illustrative | Out of scope, must not be mislabeled |
| Numeric terrain without configured provider | Not available; truthful ellipsoid fallback | Configuration/provider gap, not a fake implementation |
| Full arbitrary-EPSG coverage | Registry/PROJ paths exist; unsupported/unconfigured codes can reject | Partial by design |
| Complete T3/T4 regression suite | Only a small command sample was exercised; installer/fresh-clone not run | Missing evidence infrastructure |

## 12. Prioritized Repair Roadmap

### P0 — Restore release gate integrity

1. Fix Wayback date parsing and add timezone-matrix tests.
2. Make telemetry scheduling deterministic in tests; restore 243/243 MSTest.
3. Keep `build-and-test.ps1` blocked until all mandatory tests pass.

### P1 — Correct user-visible truth and GIS correctness

1. Unify active CRS, status label, units, center, and cursor readouts.
2. Remove the 3D-model false-success path: implement the bridge/engine path or explicitly mark it unavailable.
3. Convert the main shell and dialogs to complete en/ar resources; isolate LTR technical/brand segments.
4. Update dead skill links and the stale flight row.

### P2 — Native GIS acceptance

1. With an authorized numeric terrain provider/token, execute terrain/grid acceptance at 1x/3x with sampled elevations.
2. Run native grid combinations, poles, antimeridian, rotated globe, labels, key lines, and UTM zones.
3. Run Wayback and NASA GIBS positive/negative provider tests with credits and truthful dates.
4. Run a KML/KMZ/project round trip with byte inspection and SQLite readback.
5. Exercise search, location denial/allow, drawing, measurement, elevation profile, vector import, overlays, and snapshots.

### P3 — UX, accessibility, and deployment hardening

1. Test 100–200% DPI, minimum sizes, multiple monitors, keyboard focus, screen readers, and high contrast.
2. Create stable native UI automation for each ribbon group and WebView effect.
3. Run the canonical build/publish script only after green gates, then execute the fresh-clone and packaged T4 checklist.
4. Capture logs/screenshots and provider attribution for the release evidence bundle.

## 13. Test Coverage & Unverified Items

### Verified evidence

- Buildability at T1.
- 44 Node tests at T2.
- 240 passing MSTests at T2, with three explicitly retained failures.
- Skill snapshot/routing and most helper behavior at T1/T2.
- Native start, WebView2/Cesium display, 2D/3D, theme, Home, Full Extent, grid toggle, partial clouds, and partial Arabic at T3 for the external portable preview.

### Not verified

- Byte identity between portable preview and current ZIP workspace.
- Pan/rotate/tilt acceptance, extent history, flight navigation, fit selection, tours.
- Terrain tiles, numeric elevation, 1x/3x exaggeration, terrain-backed profile.
- Full grid option matrix, polar/antimeridian/rotation behavior.
- Sunlight/time slider, atmosphere visual differences, cloud visual correctness.
- Live Wayback/NASA catalogs and tiles, network failures, provider quotas.
- Search/Photon, geolocation permissions, gazetteer labels.
- Drawing, snapping, measurements, overlays, vector import, screenshot output.
- KML/KMZ and project native save/reopen/round-trip bytes.
- All dialogs, context menus, keyboard shortcuts, error dialogs, and accessibility flows.
- Canonical Preview output, installer, fresh clone, GPU/driver matrix, and T4 deployment.
- External security scanners, dependency vulnerability lookup, penetration testing.

## 14. Final Project Readiness Assessment

| Dimension | Assessment |
|---|---|
| Architecture | Sound hybrid WPF/MVVM/WebView2/Cesium structure |
| Build | Release build passes |
| Automated gate | **Failing**: 3/243 MSTests fail |
| Native core globe | Demonstrably functional in the external portable preview |
| GIS correctness | Material CRS inconsistency; terrain acceptance unavailable |
| Localization | Incomplete Arabic/RTL implementation |
| Persistence/interchange | Good T2 evidence; insufficient T3 evidence |
| Security | No local high-confidence secret/path issue found; external scanning not run |
| Release packaging | Not performed because tests failed |
| Overall readiness | **Not ready for a stable release** |

Z EARTH has moved beyond a prototype: the application shell, globe, navigation, persistence services, tests, and provider abstractions are real. The correct next step is not broad feature expansion. It is to restore a fully green deterministic test gate, fix the CRS truth defect and false-success model command, complete localization, then run the native GIS acceptance matrix against a build produced by the canonical script. Until that is done, the honest status is **development build with a working core globe and significant unverified surface**, not a stable, fully validated Google Earth Pro alternative.
