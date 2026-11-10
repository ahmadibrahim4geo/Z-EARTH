# Options & GIS Settings — five-tab design compliance and implementation

**Date:** 2026-10-28  
**Branch / base:** `repair/full-verification-2026-10-27` / `6696a8e`  
**Scope:** Measurement Units, Spatial Snapping, Basemap & Display, Terrain Models & Bathymetry, and Map Grid  
**Implementation status:** complete in source  
**Release acceptance:** partial; no Preview was produced because mandatory MSTest/native click-through gates are not fully green

## Executive result

The five approved Options interfaces are now one canonical WPF window with a shared dark-gray card system, cyan selection accents, responsive sizing, a common footer, dark selectors, keyboard focus states, source-specific help, and actual illustrative raster thumbnails for basemap, terrain/bathymetry and map-grid previews. The functional audit also corrected two real data-flow defects below the UI: saving a basemap now applies it to Cesium, and the Intersections snapping preference is now evaluated by the engine rather than being a cosmetic checkbox.

Native Windows capture exposed an oversized-window defect: the original 1580×940 design was centered beyond the edges of a smaller owner monitor. The dialog now preserves that composition on large displays but constrains and recenters itself to the actual owner window on smaller displays. All five final native captures show the title bar, tabs and Save/Cancel footer without horizontal clipping; content uses vertical scrolling where the reduced height requires it.

## Initial comparison and implementation checklist

| Tab | Initial gap against approved reference | Implemented response |
|---|---|---|
| Measurement Units | Compact generic selectors, weak hierarchy, no trustworthy live examples | Three reference-aligned cards, complete existing unit lists, real `UnitService` previews, WGS84/canonical-SI explanation |
| Spatial Snapping | Preferences were visually primitive; intersection checkbox was not honored by Cesium | Two-column behavior/targets layout, toggle, validated 0.1–100 m slider + edit, four target cards, local-tangent-plane intersection candidates |
| Basemap & Display | Primitive selector and incomplete provider/status presentation; Save persisted without applying to the globe | Selectable provider gallery with provider-neutral map thumbnails, status and attribution guidance, screenshot/theme/language/zoom controls, `SetBasemapAsync` on Save, truthful disabled CARTO legacy entries |
| Terrain Models | Needed the approved three-column workspace and source-specific truth | Seven-source gallery with distinct terrain/bathymetry imagery, dynamic configuration/help, live engine test path, token show/hide, local DEM inspection, GEBCO workflow, Cancel rollback, direct Ribbon command |
| Map Grid | Strong prior implementation but defaults/colors and compact-screen behavior differed from the reference | Reference color defaults, four visible format cards, independent switches, validated intervals, UTM controls, grid-free world-map background with live color-bound WPF overlays, owner-aware sizing |

## Applied project skills and constraints

The implementation followed the repository router and the relevant pinned specialists: Prompt Intent Orchestrator, GIS Development, GIS Desktop Application Development, GIS UI/UX Design & Testing, UI Command Audit, Desktop E2E Testing, GIS Data Flow Diagnostics, Coordinate Reference Systems, Arabic Internationalization, GIS Security Guardian, Completion Evidence, and Agent Development Journal.

Material consequences of those instructions:

- UI controls were traced through settings/domain and `GlobeBridge` into Cesium before being described as functional.
- GEBCO 2026 remains a numeric dataset workflow, never a fictitious ready TerrainServer or 2D WMS-as-terrain claim.
- Cesium tokens remain process-memory/environment only and are excluded from plaintext settings/logs. Persistent credential storage was not introduced; therefore no false “remember securely” option is shown.
- Provider claims were checked against current official documentation. CARTO hosted basemaps now require provider-authorized access/API keys, so the three legacy CARTO entries remain for settings compatibility but are visibly disabled instead of being mislabeled “No key”.
- Native captures are T3 visual evidence only; they are not promoted into full interaction or deployment evidence.

## Source changes in this work package

| File | Purpose |
|---|---|
| `src/ZEarth.App/Views/OptionsDialog.xaml` | Shared design system and all five approved tab compositions |
| `src/ZEarth.App/Views/OptionsDialog.xaml.cs` | Live previews, validation, save/apply/cancel behavior, terrain source workflow, responsive owner sizing |
| `src/ZEarth.App/Services/LocalizationService.cs` | Arabic/RTL coverage for the redesigned labels, guidance and statuses |
| `src/ZEarth.App/Services/GridSettingsDefaults.cs` | Approved fresh/reset grid colors while preserving valid stored user colors |
| `src/ZEarth.App/ViewModels/MainViewModel.cs` | Direct Terrain Options command and selected-provider integration |
| `src/ZEarth.App/Views/MainWindow.xaml` | Ribbon entry for Terrain Options |
| `src/ZEarth.App/App.xaml.cs` | Deterministic native capture routes for all five tabs |
| `src/ZEarth.App/Assets/Options/*.png` | Original provider-neutral basemap, terrain/bathymetry and map-grid illustrative raster assets |
| `src/ZEarth.App/Assets/Options/README.md` | Asset purpose, prompt-set summary and provider/licensing guardrails |
| `src/ZEarth.Globe/Assets/cesium-engine.js` | Real intersection snapping and preference round-trip |
| `tests/options-window-redesign.test.cjs` | Five-tab, provider-truth, integration, security and capture contracts |

The checkout contained substantial pre-existing uncommitted work. Unrelated changes were preserved and were not reset, staged, merged or published.

## Functional integration corrections

### Measurement units

The selectors retain every enum value already supported by the domain. Examples are generated by `UnitService.FormatDistance`, `FormatArea` and `FormatElevation`; they are not hardcoded mock numbers. Save writes the existing settings model, after which supported measurement/readout/export paths consume those units.

### Spatial snapping

Tolerance entry and slider synchronize with range validation. The WPF settings already crossed the bridge, but `snapIntersections` was previously ignored by the JavaScript engine. The engine now constructs real segment-intersection candidates in a cursor-local ENU tangent plane and includes the preference in its response state. Vertices, midpoints, edges and intersections compete by distance/priority inside the configured ground tolerance.

### Basemap & display

The gallery preserves the registered providers and replaces the primitive dropdown. On Save, the selected provider is persisted and `GlobeBridge.SetBasemapAsync` must acknowledge the live change; failure is surfaced instead of being reported as success. Attribution remains mandatory even when optional identity marks are hidden.

Provider review:

- [OpenStreetMap tile policy](https://operations.osmfoundation.org/policies/tiles/) requires visible attribution, identifiable requests, cache compliance, and prohibits bulk/offline prefetch from the public tile service.
- [Esri basemap documentation](https://developers.arcgis.com/documentation/mapping-and-location-services/mapping/basemaps/introduction-static-basemap-tiles-service/) requires Esri and data attribution; newer static-basemap APIs have token/pricing conditions.
- [Natural Earth terms](https://www.naturalearthdata.com/about/terms-of-use/) place its raster/vector data in the public domain; the bundled offline option remains independent of hosted-tile promises.
- [CARTO's current basemap license notice](https://github.com/CartoDB/basemap-styles/blob/master/LICENSE.md) states hosted basemap access is restricted; legacy entries are therefore disabled pending an authorized credential integration.

### Terrain and bathymetry

The dialog distinguishes AWS Terrarium, Cesium World Terrain, Cesium World Bathymetry, GEBCO 2026, WGS84 ellipsoid, local raster preparation and custom HTTPS quantized-mesh. Live provider tests go through `SetTerrainProviderAsync`; local/GEBCO choices refuse to masquerade as directly streamable terrain. Temporary terrain previews are restored on Cancel/close.

- [Cesium ion token guidance](https://cesium.com/learn/ion/cesium-ion-access-tokens/) supports least-privilege `assets:read` tokens and recommends per-application restrictions.
- [GEBCO 2026 documentation](https://www.gebco.net/data-products-gridded-bathymetry-data) describes a 15 arc-second numeric grid available in raster/netCDF forms, not a ready Cesium quantized-mesh endpoint.

### Map grid

Fresh/reset colors now match the approved reference: `#FF5252`, `#3BA9FF`, `#3AD17B`, `#E8E8E8`, `#FFC107`. Existing valid user colors are normalized and retained. Major references default ON; meridians, parallels and UTM default OFF. The world-map bitmap contains no baked coordinate or UTM lines. Major references, meridians, parallels, labels and UTM preview boundaries are separate WPF overlays whose visibility and brush values bind directly to the current switches and color-picker dependency properties, so changes appear immediately in the preview. The preview remains illustrative; real globe rendering continues to use the tested engine geometry for configured intervals, 6° UTM zones, Norway/Svalbard exceptions, latitude bands and antimeridian windows.

### Illustrative thumbnail assets

The new raster assets were produced with the built-in image-generation tool from three provider-neutral prompt specifications: a seven-scene basemap atlas, a seven-scene terrain/bathymetry atlas, and a wide world-map background. Every prompt prohibited words, numbers, logos, trademarks, provider branding and watermarks. A follow-up precise edit removed every grid line and marker from the world bitmap and reconstructed the underlying land/ocean surface. WPF crops the atlas cells with relative `ImageBrush.Viewbox` regions and draws the grid preview as live vector overlays, so every gallery item has a real visual preview without redistributing any hosted provider tile or freezing user-selectable colors into a PNG. Runtime source status, licensing truth and attribution stay as accessible WPF text.

## Native before/after evidence

The first native pass exposed clipping; the final pass used the application's own `--capture` route and an isolated settings directory.

| Surface | Before viewport fix | Final native Windows capture |
|---|---|---|
| Measurement Units | [before](../native-evidence/options-five-tabs/options.png) | [final](../native-evidence/options-five-tabs-final/options.png) |
| Spatial Snapping | [before](../native-evidence/options-five-tabs/snapping.png) | [final](../native-evidence/options-five-tabs-final/snapping.png) |
| Basemap & Display | [before](../native-evidence/options-five-tabs/basemap.png) | [final](../native-evidence/options-five-tabs-final/basemap.png) |
| Terrain Models | [before](../native-evidence/options-five-tabs/terrain.png) | [final](../native-evidence/options-five-tabs-final/terrain.png) |
| Map Grid | [before](../native-evidence/options-five-tabs/grid.png) | [final](../native-evidence/options-five-tabs-final/grid.png) |

The approved reference screenshots remain the user-supplied comparison source; they were not copied or altered in the repository.

Post-thumbnail native evidence:

| Surface | Native Windows capture with illustrative images |
|---|---|
| Basemap & Display | [thumbnail evidence](../native-evidence/options-thumbnail-evidence/basemap.png) |
| Terrain Models | [thumbnail evidence](../native-evidence/options-thumbnail-evidence/terrain.png) |
| Map Grid | [thumbnail evidence](../native-evidence/options-thumbnail-evidence/grid.png) |

## Five-row acceptance matrix

| Tab | Reference matched | Controls functional | Native tested | Result |
|---|---|---|---|---|
| Measurement Units | Substantial composition match | T1/T2 conversion, save and contract evidence | T3 render captured; dropdown click-through not completed | Implemented; native interaction partial |
| Spatial Snapping | Substantial two-column/card match | T2 bridge/engine contract including intersections | T3 render captured; live drawing gesture not rerun | Implemented; native interaction partial |
| Basemap & Display | Substantial gallery/right-panel match | T2 Save→bridge application and provider-truth tests | T3 render captured; hosted provider switching not rerun | Implemented; native interaction partial |
| Terrain Models | Substantial three-column/source-help match | T2 provider validation, rollback and command-chain evidence | T3 render captured; credentialed ion assets not tested | Implemented; credentialed native test pending |
| Map Grid | Substantial grouped-panel/format-card match | Existing T2 geometry and settings coverage | Fresh T3 render plus prior project T3 grid evidence | Implemented; current click-through partial |

No row is marked an unconditional PASS merely because controls are present.

## Test and audit evidence

| Check | Result |
|---|---|
| `dotnet build ZEarth.sln -c Release --no-restore -m:1` (isolated output) | **PASS** — 0 warnings, 0 errors |
| `node --test tests/*.test.cjs` | **PASS** — 63/63 |
| `node --check src/ZEarth.Globe/Assets/cesium-engine.js` | **PASS** |
| Options focused contract suite | **PASS** (included in 63/63) |
| Thumbnail asset + command contract suites | **PASS** — 17/17; verifies bundled PNGs, brush bindings, no legacy letter tiles and bridge contracts |
| Pinned GIS skill snapshot validation | **PASS** — 30 skills, 76 references |
| Prompt Intent Orchestrator unit tests | **PASS** — 16/16 |
| GIS Security Guardian audit | **PASS** — 0 findings; external scanners not run |
| UI command audit | **PASS as inventory** — 29 files, 332 controls, no duplicate IDs or missing icon assets; tool explicitly reports no native execution claim |
| `git diff --check` | **PASS** |
| MSTest | **BLOCKED** — `vstest.console` could not connect to `testhost` after 90 seconds; no test-result claim made |
| Agent Development Journal tests | **BLOCKED by host filesystem policy** — Windows `os.link` returned access denied; 4 errors, 1 skipped |

## Remaining limitations and release decision

- A complete native click-through of every visible control, dark ComboBox popup, keyboard navigation at 100/125/150/200% DPI, Save/restart persistence, live drawing snap gestures, and live hosted-provider failures was not completed in this host session.
- No real Cesium ion credential was supplied or transmitted, so World Terrain/Bathymetry asset authorization remains unverified.
- Persistent Windows credential storage was intentionally not implemented. The current model is safer and truthful: session/environment only, no plaintext persistence.
- MSTest is blocked by local testhost IPC, not reported as green.
- T4 packaged deployment was not run. Because release gates are incomplete, `scripts/build-and-test.ps1` was not used to overwrite the canonical Preview folder.
- No branch merge, commit, push or PR was performed.

## Final acceptance status

**Source implementation: COMPLETE.**  
**Five-tab native visual compliance: VERIFIED at T3 by final captures.**  
**Full functional/native/release acceptance: PARTIAL / BLOCKED by the explicit items above.**

This is the highest truthful completion state supported by the available evidence. A release candidate should be produced only after MSTest can connect to `testhost` and the remaining native interaction/DPI checklist is executed on a stable Windows WebView2 host.
