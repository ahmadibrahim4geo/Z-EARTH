# Z EARTH Desktop — Grid, Terrain, Sunlight, CRS & UX implementation
**Date:** 2026-10-25  
**Target:** `main` (directly committed at user request)  
**Baseline:** `735f64921d1b9ee98e452b77f3686714b8d428fe`

## Implementation completed

### Coordinate grid — `src/ZEarth.Globe/Assets/cesium-engine.js`, Options
- **Show Grid** in the View ribbon alone controls visibility; Grid Options changes grid content.
- **Major Geographic Reference Lines & Points** appears as the first independent setting, separate from ordinary meridians and parallels.
- Removed the duplicate International Date Line and suppressed coincident UTM/regular meridians where the major grid has precedence.
- Meridian/parallel and visible UTM-zone labels use Cesium `CallbackPositionProperty` so their anchors follow the globe camera rather than fixed positions.
- The grid still requires native 2D/3D visual validation at the date line, poles, low/high zoom and terrain.

### Sunlight and ribbon
- The initial scene uses the Windows **current UTC instant** rather than static noon; toggling in unchanged Live/default state re-reads the clock.
- Moving the time slider/date enters user-controlled simulated time, preserving the user's choice.
- The Sunlight HUD is draggable by its header; its buttons remain independently clickable.
- Increased View ribbon height from 90 to 112 to reduce label clipping.
- The Cesium sun calculations depend on map location + UTC, **not automatic device GPS**. Device-location requests remain permission-gated via Find My Location.

### Terrain providers
- Switched Cesium ion World Terrain activation to the supported `CesiumTerrainProvider.fromIonAssetId(1)` asynchronous API.
- Kept the free/no-token AWS Terrarium numeric DEM pipeline and explicit fallback/diagnostic events.
- **Known limitation:** Cesium World Terrain and Cesium World Bathymetry still require a user's valid ion token and actual service connectivity. These are not silently replaced by invented elevation values.
- **GEBCO 2026 status:** The official GEBCO_2026 15 arc-second land/ocean dataset exists, but it is a downloadable elevation grid, **not an immediately usable Cesium quantized-mesh service**. The configured Cesium World Bathymetry option retains its honest GEBCO 2023 provenance. A true GEBCO_2026 terrain provider is **not yet implemented**; it requires licensed/credited data acquisition, raster-to-terrain conversion or an authorized hosted terrain endpoint and real tests.

### Map capture and messages
- Screenshot now captures actual Cesium/WebView2 map PNGs via `CapturePreviewAsync`, verifies a non-empty file and reports failure truthfully.
- Added a configurable screenshot directory (Options → Basemap & Display) with a default under Pictures/ZEarth.
- Replaced the screenshot success modal with a four-second, nonblocking status-row toast.
- This is scoped to screenshot; other legacy informational dialogs still exist.

### Basemap UI
- Replaced the oversized horizontal Basemap list in the ribbon with a compact gallery launcher, custom-service button, active-source readout and borders toggle.
- Added **Base Map** launcher in **View**.
- Reused the existing searchable/filterable Basemap Gallery and its two-source Swipe tool instead of building a second gallery.
- Source connectivity, provider entitlements and license notices still require verification against actual requests.

### Worldwide CRS browser
- Preserved built-in reference presets and added a full EPSG catalog loader backed by **PROJ/proj.db** when accessible.
- Added an independent fallback through the installed native GDAL/PROJ `Osr.GetCRSInfoListFromDatabase("EPSG", out count)` API so normal GDAL installations need not expose the database path.
- Grouped worldwide geographic, projected and vertical definitions under derived region/country folders; UTM systems are grouped as Zone Series. The UI explicitly distinguishes **derived regional folders** from exact Esri UI taxonomy.
- Removed the hard-coded false claim that EPSG:32636 is necessarily an active layer CRS.
- Added catalog provenance/health text and a Windows acceptance test requiring >1,000 real EPSG definitions and CRS coverage beyond Egypt.
- This remains **PROJ EPSG authority coverage**, not a claim of exact Esri-only WKID and every ArcGIS Pro proprietary folder grouping.

## Verification and quality gates
- GitHub Actions build and tests: **PASS** on `345ac06d6749058af8c33a8801966b9dd399e9f4`, run [40219469619](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/runs/40219469619).
- The Windows `.NET 10` WPF compile, MSTest unit suite, portable Preview packaging and static Node/JavaScript validators passed on that commit.
- Regression tests corrected to expect true current UTC instead of hard-coded noon. New static tests verify valid Cesium dynamic position properties and line de-duplication.
- The earlier feature commits triggered failed runs **only until** the outdated sunlight assertion and missing CRS catalog lookup were corrected. Do not use those earlier statuses as the final result.

## Remaining native acceptance work (not certified by CI)
1. **T3/T4 Windows/WebView2 real UI:** pan/zoom and rotate grid at Greenwich, 180°, Norway/Svalbard and the poles; verify line/label priority and visibility frame by frame.
2. **Terrain network/data:** test AWS Terrarium root and viewport samples; 2D/3D mesh relief around mountains and coastline; ion login/token failure/success for Copernicus and Bathymetry; confirm failures are diagnosed and terrain OFF is truthful.
3. **Sunlight:** verify Cairo after sunset, real vs simulated UTC date/time, movable HUD, persistent user-chosen time, and 3D shadow rendering.
4. **Screenshot:** inspect an actual PNG on Windows, change the save folder and verify naming and toast disappearance.
5. **Basemaps:** test gallery search/filter, legitimate sources, tile loading and Swipe; check visual layout at 100/125/150% Windows scaling.
6. **CRS:** verify database source and correct EPSG/ESRI authority distinctions, transformations/coordinate ordering, vertical datum grids and favorites; only mark exact Esri UI folder parity when confirmed against official mappings.

**Explicit release honesty:** CI success is build/static/test evidence, **not** proof that native GPU/WebView2 rendering and third-party network providers are operational on the user's computer. Do not mark GEBCO_2026 terrain as shipped, or mark native acceptance as passed, until tested.
