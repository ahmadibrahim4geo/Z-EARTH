# Map Grid — WPF UI, Cesium engine, persistence and native verification

**Request:** redesign and functionally upgrade the Z EARTH geographic Map Grid settings
(Options → Map Grid) so that every control is wired to real Cesium rendering, persisted,
restart-safe, and verified on native Windows WebView2.

**Branch:** `repair/full-verification-2026-10-27`
**Working tree:** `D:\My Software\Z EARTH\canonical-main` (the ZIP copy at `D:\My Software\Z EARTH` was not touched)
**Date:** 2026-10-27
**Product name preserved:** Z EARTH (never renamed)

---

## 1. Scope

In scope — the geographic coordinate grid only:

| Area | Delivered |
|---|---|
| A. Grid visibility | Major references, meridians, parallels, UTM — independent switches |
| B. Grid colours | Five independent colours (major, longitude, latitude, labels, UTM) |
| C. Label format & size | Degrees / DM / DMS / DD + Small / Medium / Large / Extra Large |
| D. UTM | Zone boundaries, zone/GZD labels, placement, label type |
| Persistence | `settings.json` round-trip, restart, 2D and 3D |
| Ribbon | View → Show Grid is a true two-state toggle |

Out of scope (deliberately unchanged): every other Options tab, the status-bar coordinate
format, KML/Places, terrain, Wayback, and the single Preview build.

---

## 2. Behaviour contract

1. **Labels are automatic.** The old “Show coordinate labels” checkbox is gone. A label is
   produced for every enabled component, so labels can never contradict the lines.
2. **View → Show Grid is a master gate.** OFF removes every grid primitive but does not
   rewrite the component switches, so switching it back ON restores exactly the previous
   configuration. The button shows a translucent green plate only while the grid is
   genuinely enabled in Cesium, and the state is persisted.
3. **Five colours, one UTM colour value.** The UTM colour appears in two places in the
   dialog (boundary row and UTM group) but is one setting; both pickers stay in sync.
4. **Four label formats**, independent from the status-bar format:
   `Degrees` (whole degrees), `DM` (degrees + decimal minutes), `DMS` (degrees, minutes,
   seconds), `DD` (decimal degrees, 4 dp). Trailing zeros are trimmed, so a whole value is
   `10°` and never `10° 0′ 0″` or `10.0000°`.
5. **Intervals** are independent per axis, `0` = automatic, valid range `0.05°`–`60°`.
   Thinning only ever uses integer multiples of the requested interval — never an arbitrary
   value such as `7.3°`.
6. **UTM** covers zones 1–60 and latitude bands C–X (−80°…84°), including the Norway (31V,
   32V) and Svalbard (31X–37X) exceptions. Labels sit inside the zone polygon, never on a
   boundary meridian, and never on top of a major parallel.
7. **Save applies and persists; Cancel discards.**

---

## 3. The single chain

Every grid control must be traceable along one chain; nothing is visual-only:

```
OptionsDialog.xaml (Map Grid tab)
  → OptionsDialog.xaml.cs : OnSaveClick()
    → SettingsService.Instance.Settings + SettingsService.Instance.Save()   (settings.json)
    → GlobeBridge.SetGraticulesConfigAsync(... 17 parameters ...)
      → cesium-engine.js : GraticuleManager.setConfig(cfg) → buildGraticules()
        → Cesium entities (polylines, labels, points)
  → MainViewModel.IsGraticulesVisible → GridToggleBackground / GridToggleBrush
    → MainWindow.xaml ribbon button (View → Show Grid)
```

Startup replays the persisted values into the same bridge call, so a restart reproduces the
previous scene without the dialog ever being opened.

---

## 4. Implementation by layer

### 4.1 Settings model and defaults

| File | Change |
|---|---|
| `src/ZEarth.App/Services/GridSettingsDefaults.cs` | **New.** Canonical defaults, `Normalize()`, `NormalizeInterval()`, `GridColor` (opaque `#RRGGBB` normalisation and Cesium `rgba()` conversion). |
| `src/ZEarth.App/Services/SettingsService.cs` | 15 grid properties on `AppSettingsModel`, all defaulted from `GridSettingsDefaults`; `Normalize()` runs on load so a corrupt file can never poison the scene. Also honours `ZEARTH_SETTINGS_DIRECTORY` so native test runs never touch a real user profile. |

Defaults: major references ON, meridians OFF, parallels OFF, UTM OFF, format `DD`,
both intervals `10°`, label size `Large`, grid OFF.
Colours: major `#FF3B30`, longitude `#1E90FF`, latitude `#FFD60A`, labels `#FFFFFF`,
UTM `#FF00FF`.

### 4.2 WPF settings surface

| File | Change |
|---|---|
| `src/ZEarth.App/Views/OptionsDialog.xaml` | Compact Map Grid tab rebuilt into four labelled groups (Grid Visibility, Grid Colors, Label Format & Appearance, UTM Zone Boundaries & Labels) that fit a 1060×735 dialog without clipping. |
| `src/ZEarth.App/Views/OptionsDialog.xaml.cs` | Grid load/save/reset, live state pill, label-size readout, interval validation, and two-way sync between the two UTM colour pickers. |
| `src/ZEarth.App/Views/ColorPickerButton.xaml` / `.xaml.cs` | **New.** Dependency-free colour control (palette + hex entry) instead of a WinForms `ColorDialog`. |

### 4.3 Cesium rendering

| File | Change |
|---|---|
| `src/ZEarth.Globe/Assets/graticule-labels.js` | **Single** label formatter for all four formats, with trailing-zero trimming, whole-minute/second rounding (so `9°59′60″` is impossible), and hemisphere rules that never label `0°` or `±180°` with a contradictory letter. |
| `src/ZEarth.Globe/Assets/graticule-geometry.js` | UTM zone bounds with Norway/Svalbard exceptions, latitude bands (−80…84), grid-zone designations, zone label anchors inside the polygon, visible-segment/label enumeration, antimeridian windows, and the camera-horizon test. |
| `src/ZEarth.Globe/Assets/cesium-engine.js` | `GraticuleManager` rewritten: configurable colours and label size (11/13/15/19 px), fixed geographic label anchors rebuilt on `camera.moveEnd`, automatic labels, UTM labels at zone centres, screen-space label layout, 2D/3D handling, and a full rebuild on `morphComplete`. |
| `src/ZEarth.Globe/GlobeBridge.cs` | `SetGraticulesConfigAsync` extended to 17 parameters with defaults matching `GridSettingsDefaults`; payload keys verified by test. |

### 4.4 Ribbon

| File | Change |
|---|---|
| `src/ZEarth.App/ViewModels/MainViewModel.cs` | `IsGraticulesVisible` initialised from persisted settings, persists on change, exposes `GridToggleBrush` / `GridToggleBackground`. |
| `src/ZEarth.App/Views/MainWindow.xaml` | Show Grid button binds `Background` and `Foreground` to the two-state brushes. |
| `src/ZEarth.App/Views/MainWindow.xaml.cs` | Startup `SetGraticulesConfigAsync` sends every persisted grid field. |

### 4.5 Test automation hook

`src/ZEarth.App/App.xaml.cs` gained `--dialog grid`, which opens the real `OptionsDialog`
directly on the Map Grid tab. It is an automation entry point, not a product feature.

---

## 5. Defects found during native verification, and their fixes

These were **found by running the app**, not by reading code. Each one is now covered by a
regression test where a static assertion is meaningful.

| # | Symptom | Root cause | Fix |
|---|---|---|---|
| 1 | Switching to 2D made the **entire grid disappear** | Grid polylines were `clampToGround: true`. Cesium's ground-polyline primitive is 3D-only and is silently dropped in 2D / Columbus View. | `groundClamp()` returns `true` only in `SCENE3D`; height-0 polylines are used in 2D. |
| 2 | In 2D only labels near the viewport centre appeared | The spherical horizon test (`isSurfaceAnchorVisible`) was applied on a flat plane, so every off-centre anchor looked “behind the globe”. | `surfaceVisible()` returns `true` outside 3D. |
| 3 | Labels were cut in half at the map edge (`ZD 28Q`, `me Meridian 0°`) | Cesium keeps drawing a label whose anchor is outside the canvas, and view-rectangle arithmetic is unreliable in a tilted 3D view. | Project the anchor with `SceneTransforms`; skip anchors outside the canvas; flip horizontal origin near an edge. |
| 4 | The projection silently failed | The bundled Cesium is **1.146.0**, where `SceneTransforms.wgs84ToWindowCoordinates` was renamed to `worldToWindowCoordinates` (occurrences in `Cesium.js`: 0 vs 6). | Call `worldToWindowCoordinates || wgs84ToWindowCoordinates`. |
| 5 | 2D labels rendered **dim grey at 75 % size**, contradicting the Label Size control | `translucencyByDistance` / `scaleByDistance` were evaluated against the flat 2D projection distance, so white labels faded and shrank. | Both fades removed; the chosen size and colour now render exactly. `distanceDisplayCondition` still culls far labels. |
| 6 | A Tropic label could sit on a UTM zone label | Cesium draws every label independently; there is no built-in decluttering. | Screen-space greedy layout (`resetLabelLayout` / `reserveLabelSpace`) in priority order: major references → regular grid → UTM. |
| 7 | World view produced an unreadable wall of lines | Thinning was count-based, not pixel-based. | `thin(span, base, pixels, minPixels)` with 44 px for meridians and 34 px for parallels, always an integer multiple of the user interval. |
| 8 | UTM labels collided with the Tropics / Equator | Zone labels sat at the exact centre of the latitude band, which is where those major parallels are. | Label latitude moved to `band.south + bandHeight × 0.26`; longitude stays at the exception-aware zone centre. |
| 9 | UTM bands were offset by one | `(latitude + LAT_MIN)` instead of `(latitude - LAT_MIN)` in the band index. | Corrected, with a unit test on the band bounds. |
| 10 | Major reference labels ignored the chosen format (`23.44°` even in DMS) | The label strings were hardcoded. | Built through the shared `formatDegrees()` formatter, so DMS shows `23° 26′ 11.4″ N`. |
| 11 | A duplicate UTM colour property existed | Two independent colour settings. | One `GridUtmColor`; the second dialog row is a synced second entry point, asserted by test. |

---

## 6. Test evidence

### 6.1 Commands and results (this working tree, this build)

| Command | Result |
|---|---|
| `dotnet build ZEarth.sln -c Release --nologo -v:m` | **Build succeeded — 0 warnings, 0 errors** |
| `dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release --no-build` | **262 passed, 0 failed** |
| `dotnet test … --filter FullyQualifiedName~MapGridSettingsTests` | **13 passed** |
| `node --test "tests/*.test.cjs"` | **55 passed, 0 failed** |
| `node --check src/ZEarth.Globe/Assets/cesium-engine.js` | pass |
| `python scripts/validate-agent-skills.py` | pass (unchanged by this work) |

### 6.2 Grid-specific suites

| Suite | Tests | What it proves |
|---|---|---|
| `tests/graticule-labels.test.cjs` | 5 | Four formats, trailing-zero trimming, no `9°59′60″`, negatives, hemispheres, `0`, `±180`. |
| `tests/graticule-geometry.test.cjs` | 10 | Zone 1/60 edges, Norway/Svalbard exceptions, antimeridian windows, latitude bands, horizon culling, label anchors inside the zone, label latitude clear of major parallels. |
| `tests/terrain-grid-source-contract.test.cjs` | 11 | 2D invariants (`groundClamp`, `surfaceVisible`, off-canvas skip, edge origin), pixel-based label spacing, major labels use the formatter, master gate preserves components, depth-test rule. |
| `tests/ZEarth.Tests/MapGridSettingsTests.cs` | 13 | Defaults, reset never touches the global toggle, colour parsing/conversion, interval validation, `Normalize` repairs, JSON round-trip, status-bar independence, atomic store, bridge defaults, bridge payload, Save/Cancel wiring, ribbon master gate. |

### 6.3 Evidence levels

| Level | Status |
|---|---|
| **T0** code review — chain traced from XAML to Cesium entity | Done |
| **T1** static/syntax + full solution build | Done, 0 warnings |
| **T2** unit and fixture tests (Node + MSTest) | Done, 55 + 262 |
| **T3** native Windows 11 x64, WebView2, hardware WebGL | Done for rendering, 2D/3D, restart, ribbon, dialog — see §7 |
| **T4** packaged `dist/Z EARTH-Preview` deployment | **NOT RUN** — deliberately not built for this request |

---

## 7. Native evidence

All captures are real `Z EARTH.exe` windows, produced by the shipped automation flags
(`--capture`, `--dialog grid`, `--mode 2d`, `--ribbon-tab 2`, `--theme`), against a
process-scoped settings directory (`ZEARTH_SETTINGS_DIRECTORY`), so no user profile was
modified.

| File (`Development Reports/Map_Grid_Evidence/`) | Shows |
|---|---|
| `01_grid_dialog_fresh_defaults.png` | First run: grid OFF pill, major references ON, meridians/parallels/UTM OFF, DD, 10°/10°, all four groups fully visible. |
| `03_grid_dialog_final.png` | Configured state: grid ON pill, all three line types, DMS, Extra Large, 5°/5°, UTM ON with “Hemisphere + grid zone designation”, five colour settings (six pickers — two of them the same UTM value, kept in sync). |
| `15_grid_dialog_light_theme.png` | Same surface under the light theme (no invisible text or lost contrast). |
| `06_ribbon_show_grid_on.png` | View → Show Grid with the green ON plate, and the rendered grid below. |
| `07_grid_off_no_elements.png` | Same view with the grid OFF: button unhighlighted, zero grid primitives on the globe. |
| `12_grid_3d_all_components_final.png` | 3D regional (Eye 4,500 km): major references, regular meridians/parallels, three UTM labels, DMS text — no clipping, no overlap. |
| `13_grid_3d_major_references_defaults.png` | Defaults + grid ON: only major references (Prime Meridian, Tropic of Cancer), proving the component defaults. |
| `11_grid_2d_final.png` | 2D world: lines render, labels are crisp white, decluttered, and skip edge-adjacent anchors. |
| `14_label_formats_comparison.png` | Same camera in four separate launches: DD `23.4365° N`, DM `23° 26.19′ N`, DMS `23° 26′ 11.4″ N`, Degrees `23° N`. |
| `16_label_sizes_comparison.png` | Same camera at Small / Medium / Large / Extra Large, all readable and collision-free. |

Programmatic checks were also run on the final 2D capture (a pixel scan over the map area),
confirming that each configured colour is actually rasterised rather than assumed:
major red 3,700 sampled pixels, meridian blue 2,330, parallel yellow 4,630, UTM magenta
2,123. This is what caught the earlier “grid missing in 2D” defect.

Restart persistence is demonstrated by the format and size sheets themselves: each tile is a
**separate process launch** that read the persisted value from `settings.json` and rendered it.

---

## 8. Requirements traceability

| Requirement | Status | Evidence |
|---|---|---|
| Real source changes, no mockups | Met | §4 files; no design artefacts shipped |
| Groups A–D | Met | `03_grid_dialog_final.png` |
| Defaults (major ON, lon/lat/UTM OFF, DD, 10°, grid OFF) | Met | `FreshSettingsMatchTheDocumentedGridDefaults`, `01`, `13` |
| “Show coordinate labels” removed; labels automatic | Met | No such control in XAML; labels follow components (`12`, `11`) |
| Show Grid overrides all and restores components | Met | `RibbonToggleIsAMasterGateThatPreservesComponentChoices`, `global grid visibility is a master gate…`, `06`/`07` |
| Two-state ribbon with green highlight | Met | `GridToggleBackground`, `06` vs `07` |
| Five colours reach Cesium, one UTM colour | Met | `BridgePayloadCarriesEveryGridSetting`, `12` (red/blue/yellow/magenta/white) |
| Four formats, trimming, `9°59′60″` guard, 0/±180 | Met | `graticule-labels.test.cjs`, `14` |
| Grid format independent of status bar | Met | `GridLabelFormatIsIndependentFromTheStatusBarCoordinateFormat` |
| Label size 4 steps, outlines/halos | Met | `16`; `outlineWidth = max(2, px/6)`, dark outline + subtle background |
| Collision handling, hidden-hemisphere culling | Met | `reserveLabelSpace`, `surfaceVisible`, `12`/`11` |
| Major references independent, no duplicate lines | Met | `showKeyParallels` gate; regular loops skip `0`/`±180` and near-major values |
| Independent intervals, `0` = auto, validation | Met | `IntervalValidationAcceptsAutoAndRejectsOutOfRangeValues`, save-time guard |
| Antimeridian / polar correctness, no orphans | Met | `longitudeWindows`, `removeAll()` on rebuild, `11` (world view) |
| UTM 1–60, Norway/Svalbard, −80…84, labels inside zone | Met | `graticule-geometry.test.cjs`, `12` |
| Save applies + persists, Cancel discards | Met (wiring) | `SaveAppliesEveryGridFieldAndCancelWritesNothing`; native click-through **NOT RUN** (§9) |
| Survives restart and 2D/3D | Met | `14`, `16` (restart), `11` (2D), `12` (3D) |
| Honest T0–T4 evidence | Met | §6.3 |
| Single Preview folder, no build now | Respected | `dist/` untouched |

---

## 9. Limitations and things that are NOT proven

Recorded deliberately, because a green build is not a native acceptance test.

1. **Native Save/Cancel click-through is NOT RUN.** Save and Cancel are proven by source
   contract (Save is the only writer, Cancel has `IsCancel="True"` and no handler) and by the
   atomic store test, but no automation clicked the buttons in a live window. The dialog was
   captured, not operated.
2. **High-DPI scaling is NOT RUN.** Label sizes are CSS pixels and Cesium applies the device
   pixel ratio; this was not measured on a scaled display (125 %/150 %).
3. **Zoom sweep is partial.** Decluttering was verified at world scale (2D), regional scale
   (3D, Eye 4,500 km) and the defaults view. A continuous zoom sweep, including the exact
   handover between label steps, was not exhaustively captured.
4. **Google Earth Pro visual parity is not claimed.** This is a functional and cartographic
   improvement, not a pixel-parity certification.
5. **The Options dialog is not localised.** `LocalizationService.ApplyWindow` is only called
   for `MainWindow`; this pre-dates the change and affects every Options tab, so the Map Grid
   tab is English-only like its siblings. New Arabic strings were therefore not added to
   avoid a half-translated dialog.
6. **Software rendering fallback** (no hardware WebGL) was not exercised.
7. **T4 packaging** was intentionally skipped per instruction.

---

## 10. How to reproduce

```powershell
# Build and test
dotnet build ZEarth.sln -c Release --no-restore
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release --no-build
node --test "tests/*.test.cjs"

# Native capture (isolated settings, never the real profile)
$dir = Join-Path $env:TEMP "zearth-mapgrid-native"
$env:ZEARTH_SETTINGS_DIRECTORY = $dir
Start-Process -FilePath "src\ZEarth.App\bin\Release\net10.0-windows\Z EARTH.exe" `
  -ArgumentList @('--no-splash','--dialog','grid','--capture',"$PWD\Development Reports\Map_Grid_Evidence\03_grid_dialog_final.png",'--theme','dark')
```

**Important:** the engine loads `Assets/cesium-engine.js` from the build output, so
`dotnet build` must run before every native capture. Two earlier captures in this session
were invalid because the JavaScript edit had not been copied to the output folder.

---

## 11. Changed files (Map Grid only)

New:
- `src/ZEarth.App/Services/GridSettingsDefaults.cs`
- `src/ZEarth.App/Views/ColorPickerButton.xaml`, `.xaml.cs`
- `tests/ZEarth.Tests/MapGridSettingsTests.cs`
- `tests/graticule-labels.test.cjs`, `tests/graticule-geometry.test.cjs`

Modified:
- `src/ZEarth.App/Services/SettingsService.cs`
- `src/ZEarth.App/Views/OptionsDialog.xaml`, `OptionsDialog.xaml.cs`
- `src/ZEarth.App/Views/MainWindow.xaml`, `MainWindow.xaml.cs`
- `src/ZEarth.App/ViewModels/MainViewModel.cs`
- `src/ZEarth.App/App.xaml.cs`
- `src/ZEarth.Globe/GlobeBridge.cs`
- `src/ZEarth.Globe/Assets/cesium-engine.js`, `graticule-geometry.js`, `graticule-labels.js`
- `tests/terrain-grid-source-contract.test.cjs`

Unrelated modifications already present on this branch (documentation, GLB import,
Wayback, localisation) were not part of this task and were left as they were.
