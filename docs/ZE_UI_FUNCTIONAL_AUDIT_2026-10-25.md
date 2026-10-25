# Z EARTH desktop — user-reported UI and command-path audit

**Reviewed:** 2026-10-25; source: GitHub `main` at start of review, improvements on PR #9. Uses project-local skills `zearth-google-earth-pro-workflows`, `zearth-globe-bridge`, `zearth-verification`, `gis-security-guardian`, `kml-kmz-engineering` and `gis-cartography-symbology`.

## T0–T4 evidence boundaries

- **T0:** code and reports reviewed. **T1:** static XAML/JavaScript/format checks. **T2:** local unit tests and Windows CI builds. **T3:** physical Windows WPF + WebView2 + GPU + genuine external imagery/terrain requests (not available through source-only GitHub review). **T4:** clean-device portable EXE, no existing saved profile or hidden dependency.
- The developer has reproduced, on their own installed app, nonresponding terrain providers, indistinguishable basemap colors, narrow/ineffective scrolling, and incomplete theme/grid controls. Their observations are treated as real user bug reports, **not** proof of root cause until native logs and network traces are captured.
- A bound button, a passed mock, or a build is **not** proof that Cesium changes the visible globe. Log the result of every WPF → GlobeBridge → JavaScript → scene effect.

## Reported requirements, inspected source, implemented scope, remaining validation

| ID | Contract / operation | Code route / reviewed evidence | Scope of this PR | Must verify on Windows |
| --- | --- | --- | --- | --- |
| ZE-UX-01 | The EXE's actual icon is the canonical branding icon | `src/ZEarth.App/Assets/zearth_icon.ico` from `.csproj` vs `assets/branding/zearth_icon.ico` were different Git blobs | Use exact original EXE source ICO Git blob for branding ICO; also sync approved PNG with app asset | Windows Explorer, taskbar, shortcut, DPI and logo screenshots; existing SVG art remains a separate vector |
| ZE-UX-02 | Terrain icon/text show green **only** after true activation and include selected model | `MainViewModel.ExecuteToggleTerrainAsync`, `GlobeBridge.SetTerrainProviderAsync`, `TerrainManager.setProvider` | Startup now uses VM acknowledgment; explicit load/off/fallback status, no guessed ON, last real fallback reason in status + log | Each provider independently; map relief and successful elevation tile fetch, invalid credentials, offline |
| ZE-UX-03 | Basemap icons/colors distinguish source & full names readable | `BasemapRibbonItem`, `MainWindow.xaml` | Category color chips; larger ribbon label widths; tooltips retain metadata | 100–200% Windows DPI, 980px min window, RTL, light/dark |
| ZE-UX-04 | Horizontal/vertical scrollbars are usable | `DarkTheme.xaml`, `LightTheme.xaml` had 8px custom `Track` forcing vertical navigation | Restore WPF native orientation-aware template, min hit-target sizing | Mouse, touchpad, keyboard with collapsed dock, Basemaps and CRS |
| ZE-UX-05 | Format Units opens explicit format selection | Existing `ToggleCoordinateFormatCommand` cycled status-bar readout | Both ribbon Format Units buttons open DD, DMS, UTM, MGRS selection context menu | CRS X/Y and status readout, menus in light/dark/RTL |
| ZE-UX-06 | Sunlight follows real geographic position and chosen calendar date/time | Cesium globe lighting enabled but time was UTC *today* only | UTC date picker and hourly slider now send same date/time to Cesium JulianDate; geographic sun position is computed by Earth geometry | Clock/daylight on Nile equinox, polar winter, 0° longitude, repeated toggle; **not** a country legal timezone/DST provider |
| ZE-UX-07 | Show Grid toggles a persistent grid using saved options | `IsGraticulesVisible`, `GraticuleManager` | Persist visibility, green enabled state; Grid Options opens Map Grid tab | Toggle without options reset, project restart, 2D/3D and anti-meridian |
| ZE-UX-08 | Named geographic reference lines/points (equator, prime meridian, date line, tropics, polar circles, poles and origin) | `GraticuleManager` already creates named features | Preserve existing major-line behavior, separate key-line switch | Each feature visible at appropriate zoom, no double-draw or hiding by terrain |
| ZE-UX-09 | Independent latitude/longitude line toggles and spacing, optional numbered UTM zones | Options `ShowLongitudeLines`, `ShowLatitudeLines`, slider degrees and `ShowUtmZones` already wired to bridge | Route options directly and retain independent values | UTM across zone 1/60, hemisphere and labels outside viewport |
| ZE-UX-10 | Format grid coordinate labels independently of status CRS | Regular grid formerly labels absolute decimal degrees only | Add persisted DD, DM, DMS and whole-degree selection via `graticule-labels.js`, loaded before Cesium command logic and tested in Node | Validate WGS84 prime meridian, signs/hemispheres, close-zoom readability |
| ZE-UX-11 | Measurement units split Distance/Surface Area/Elevation | Existing `OptionsDialog.xaml`, `UnitService` already offer them | Preserve controls and apply changes, **not** redesign math | Compare units against reference distances/areas, settings reopen, projection accuracy |
| ZE-UX-12 | Snapping config is meaningful and applied to actual editable geometry | Existing vertex/midpoint/edge/intersection, meter/pixel tolerance, `snap.setConfig` | Preserve existing behavior, not a claim of T3 acceptance | Test actual snapped coordinates with multiple zoom levels |
| ZE-UX-13 | Light mode styles all WPF controls, dialogs and scroll | Previous views contained theme-dependent `StaticResource Brush.*` references | Switch themed brush references to `DynamicResource` across MainWindow/Options and other dialogs, preserve resources/icons | All windows, popup menus, title bars and contrasts on Windows |
| ZE-UX-14 | KML/KMZ preserve styles/icons/resources/unknown XML and folder ordering | Already covered by ZEARTH local and vendored interoperability skills; fixture tests | No changes to parser here; guard existing round-trip requirements | Open original styled KMZ, edit, export/reopen in ZEARTH and Google Earth Pro without dropped styles |

## Known terrain-provider limitations

`demTerrain` uses the app's explicit WebView2 interception/proxy to the publicly hosted AWS Terrarium elevation tiles; its success depends on real HTTP response, valid PNG decode and Cesium CustomHeightmapTerrainProvider. `worldTerrain` and `worldBathymetry` require permitted Cesium ion access; `custom` requires a valid authorized Cesium terrain service (`layer.json` + compatible tiles). WGS84 ellipsoid is a deliberate **flat/no-relief** fallback, not a numeric DEM. Neither a visible ON badge nor provider metadata alone is acceptable evidence of terrain geometry. User-reported failures are **still unconfirmed as solved on their hardware**.

## Practical next acceptance sequence

1. Download the new Actions preview of this PR's main merge, verify SHA-256 and extract with `Assets`. Use a clean, non-administrator Windows profile and confirm actual logo.
2. Enable default AWS DEM with internet; record network status and actual rendered relief, then disable. Repeat failed/no-network and licensed ion-provider paths, save WPF status and logs.
3. Test all tabs under 100/150/200% DPI with window resize, Dark/Light switching and RTL. Confirm both scroll directions and format popup.
4. Toggle Show Grid then configure DMS/DM/DD, independent 1°/2° latitude/longitude, UTM labels, major named features. Save, reopen and test 2D/3D.
5. Set date and UTC time across the equinox and compare location-dependent sunlight over Egypt and a different longitude. Confirm atmospheric switch remains independent.
6. KML/KMZ high-fidelity and snapping scientific tests are separate, not certified by this UI PR; collect fixtures and issue-specific traceability for follow-up.

**Release gate:** green CI = T1/T2. Do **not** close open issues #3, #4, #5 or claim T3/T4 until evidence from native tests is attached.
