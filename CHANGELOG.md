# Changelog — Z EARTH Desktop GIS

All notable changes to **Z EARTH Desktop** are documented in this file.
This project adheres to Semantic Versioning and the **Single Canonical Project Policy**.

---

## [Unreleased] — licensing and repository transparency — 2026-10-26

### Documentation / legal
- Added the verbatim GNU GPL version 3 legal text in the root `LICENSE` for independently authored project code (SPDX `GPL-3.0-only`).
- Distinguished project GPL source from bundled MIT GIS skills, CesiumJS, third-party WebAssembly/assets, NuGet packages, hosted imagery and runtime terms.
- Corrected legacy v1.0.0 legal-notice labeling and removed unverified absolute security/privacy and release-compliance claims.
- Added an actionable third-party release-audit checklist; **no claim that redistributed Windows binaries are fully cleared**.

---

## [2.2.4] - Z EARTH Brand Name Standardization - 2026-10-25

### Changed
- Standardized the user-facing product name to `Z EARTH` across the application UI, metadata, documentation, and maintained scripts.
- Changed the Windows application assembly and executable name to `Z EARTH.exe` while preserving the stable internal `ZEarth.*` namespaces and data identifiers.
- Standardized release directories and archives as `Z EARTH-<version>-win-x64`.

## [2.2.3] - Live Terrain &amp; Vertical Exaggeration Repair - 2026-10-25

### Fixed
- Added a restricted in-app Terrarium tile proxy because the public AWS elevation bucket does not expose browser CORS headers to WebView2.
- Terrain activation now verifies and decodes a real elevation tile before reporting success; provider failures return a real failed bridge response and switch the ribbon state off.
- Reapplies vertical exaggeration whenever a terrain provider becomes active and explicitly requests a render.
- Automatically returns the scene to 3D when real terrain is enabled, because Cesium cannot display relief in planar 2D mode.
- Reduced the live height mesh from 256×256 to 65×65 vertices per tile to prevent stalls while retaining sub-10-metre source sampling through level 14.
- Prevented repeated parent heightmaps beyond the supported detail level and removed duplicate exaggeration commands from the WPF view model.
- Extended provider startup to 30 seconds so the first verified elevation tile can load on slower connections without a false bridge timeout.
- Cesium ion and custom providers now fail immediately with an actionable message when their required token or service URL is missing.

---

## [2.2.2] - Terrain Toggle &amp; Approved Brand Master - 2026-10-24

### Fixed
- Restored the ribbon Terrain control as a true on/off toggle; clicking it again returns the globe to the WGS84 ellipsoid.
- Terrain Options now choose and persist only the default elevation model instead of activating terrain unconditionally.
- Disabled vertical-exaggeration controls while terrain is off and repaired the broken Reset command binding.
- Rebuilt the application, taskbar, shortcut, and executable icon frames directly from the approved brand reference, preserving the exact white Z and cyan orbit overlap.

---

## [2.2.1] - Solid White Z Brand Fix - 2026-10-24

### Fixed
- Rebuilt the app and Windows executable icon with one uninterrupted opaque white Z.
- Corrected the Z geometry to the original artwork bounds and moved it above the globe/orbit artwork so no cyan segment cuts through the letter.
- Regenerated all ICO frames from 16×16 through 256×256 for consistent taskbar, shortcut, and executable rendering.

---

## [2.2.0] - Basemap, Terrain &amp; Historical Timeline - 2026-10-24

### Added
- Every supported basemap is now visible directly in the ribbon, grouped and separated by provider.
- Persistent default terrain selection with AWS Terrarium, WGS84 ellipsoid, Cesium World Terrain, Cesium World Bathymetry / GEBCO, and custom quantized-mesh choices.
- Compact live Esri Wayback timeline with exact release and year selection.
- Multi-size Windows icon rebuild with the approved white Z rendered as opaque artwork.

### Changed
- Removed the unreliable Reference Layers section from Explorer and its runtime view-model wiring.
- The startup basemap is chosen only in Options and marked with a green check in the ribbon.
- All WPF windows receive native dark/light caption, text, and border colors when loaded.
- Historical imagery previews immediately on the globe while the timeline moves, without a large comparison dialog.

---

## [2.1.0] - Reference Layers, Meter Snapping & Wayback - 2026-10-23

### Added
- Modern nine-category global Reference Layers catalog with Esri reference overlays, NASA GIBS, USGS earthquakes, OpenRailwayMap, Waymarked Trails, Wikimedia/Wikipedia GeoData, and regional OpenStreetMap Overpass layers.
- Dedicated Map Grid settings for independent longitude/latitude visibility and intervals, named major geographic lines, poles, and 0°/0° grid origin.
- Esri World Imagery Wayback browser with live public release catalog, dated imagery selection, and two-release Swipe comparison.
- Categorized basemap gallery grouped by provider, including additional OSM, CARTO, physical-earth, shaded-relief, ocean, and bathymetry products.

### Changed
- Moved complete geology/relief/ocean products from Reference Layers into their correct Basemap categories.
- Spatial snapping now uses WGS 84 ground distance in meters throughout the UI and Cesium engine instead of screen pixels.
- Startup basemap now persists from Settings and is marked with `✓ DEFAULT` in both the ribbon and gallery.
- Removed the unofficial Google tile endpoint from the built-in basemap registry.

---

## [0.2.0-alpha] - CesiumJS Engine Integration - 2026-10-20

### Added & Replaced
- **Genuine CesiumJS Geographic Viewport Integration:**
  - Integrated official CesiumJS distribution (`cesium@1.146.0`) with WGS84 Ellipsoid (`Cesium.EllipsoidTerrainProvider`).
  - Completely replaced the temporary Canvas renderer from the primary runtime path.
  - Deployed full Cesium assets: `Cesium.js`, `Widgets/widgets.css`, `Workers/`, `Assets/`, and `ThirdParty/`.
  - Configured `CESIUM_BASE_URL = 'cesium/'` and mapped local assets via WebView2's `SetVirtualHostNameToFolderMapping` (`https://cesium.local/index.html`), providing secure Web Worker and WebAssembly execution without local HTTP development servers.
  - Initialized without mandatory Cesium ion token (`Cesium.Ion.defaultAccessToken = ''`).

- **Independent Imagery Provider Manager:**
  - Integrated OpenStreetMap tile imagery (`Cesium.OpenStreetMapImageryProvider`) with mandatory attribution (`Map data © OpenStreetMap contributors`).
  - Integrated offline WGS84 geographic grid basemap (`Cesium.GridImageryProvider`) for zero-network environments.
  - Provided extensible architecture for future WMTS/WMS/XYZ and terrain elevation layers.

- **Cesium Camera Navigation & Controls:**
  - Configured initial view showing Egypt in its surrounding continental context (`lon: 30.8025, lat: 26.8206, height: 4,500 km`).
  - Implemented 3D Globe and 2D Planar Map morphing (`scene.morphTo2D` / `scene.morphTo3D`).
  - Added full navigation commands: Home, Full Extent, Zoom In, Zoom Out, Reset North (heading to 0°), and Reset Pitch (nadir -90°).
  - Configured safe camera limits: `minimumZoomDistance = 50m`, `maximumZoomDistance = 60,000 km`, and collision detection.
  - Real-time camera listener feeding latitude, longitude, altitude, heading, and pitch back to the WPF host status bar.

- **Official CesiumJS Agent Skills:**
  - Extracted official `CesiumGS/cesiumjs-skills` to `.github/skills/` covering viewer setup, camera navigation, imagery, terrain, entities, and spatial mathematics.

---

## [0.1.5-alpha] - Sprint 0.5 Critical Quality Correction - 2026-10-20

### Changed & Redesigned
- **Complete GIS Desktop Interface Redesign:**
  - Implemented the professional neutral dark-gray palette inspired by established desktop GIS software:
    - Main Window: `#252629`
    - Ribbon: `#303236` (Border: `#3F4247`)
    - Sidebar Panels: `#393B40` (Border: `#474A50`)
    - Controls: `#45484D` (Hover: `#54575E`, Pressed: `#2A2B2E`)
    - Accent: `#22A8DE` (Hover: `#38BAEE`)
    - Text: Crisp light-gray (`#D0D3D6`, `#9EA3A8`) and white (`#FFFFFF`)
  - Integrated native Windows 10/11 DWM immersive dark title bar via `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE` and caption color `0x00292625`), ensuring seamless OS chrome integration.
  - Replaced all excessive navy-blue backgrounds across main shell, dialogs, splash, and HUD overlays.
  - Created centralized global WPF resource dictionary at `Themes/DarkTheme.xaml`.

- **Structured GIS Ribbon Interface:**
  - Reorganized into 8 distinct categories: **Project**, **Navigation**, **Drawing**, **Measurement**, **Layers**, **Terrain**, **Tools**, and **Help**.
  - Added Quick Access Toolbar: New, Open, Save, Undo, Redo, Separator, Home, Full Extent, Previous Extent, and Next Extent.
  - Clearly marked all unimplemented commands with explicit roadmap notices (`(Planned - Sprint 1/2)`) and disabled states (`IsEnabled="False"`).

- **Google Earth Pro Places & Layers Hierarchy:**
  - Designed dual-tab sidebar (`📍 Places` and `🗺 Layers`).
  - Added Google Earth Pro search bar supporting placemark names and decimal coordinate fly-to (`lat, lon`).
  - Implemented Places tree view with `My Places`, `Temporary Places`, and `Saved Scenes`.
  - Added Layers panel with Basemap selection gallery, political borders, graticules, and future data source toggles.

- **Globe Viewport & Architecture Assessment:**
  - Formally evaluated CesiumJS offline requirements and investigated why the lightweight prototype was initially used.
  - Added prominent engine identification watermark: `Native GIS Viewport • CesiumJS Architecture Bridge • WGS 84`.
  - Added real-time layer visibility toggles (`layers.setBorders`, `layers.setGraticules`) over WebView2 JSON bridge.
  - Refined camera navigation bounds, smooth mouse wheel zooming, and Nadir/North-up orientation.

---

## [0.1.0-alpha] - Sprint 0 Foundation & Startup Repair - 2026-10-20

### Fixed
- **Critical Startup Stall / Crash:**
  - Resolved `InvalidOperationException` triggered at `mainWindow.Show()` caused by `RadioButton.IsChecked` attempting a TwoWay binding to the getter-only properties `Is3DMode` and `Is2DMode` on `MainViewModel`. Implemented full property setters with automatic mode change dispatch to `GlobeBridge`.
  - Hardened application shutdown lifecycle in `App.xaml.cs`: configured explicit shutdown (`ShutdownMode = ShutdownMode.OnExplicitShutdown`) during the splash-to-main-window transition to prevent premature process termination.
  - Added global unhandled exception hooks (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) with detailed logging to `%LocalAppData%\ZEarth\logs\zearth.log`.
  - Improved `GlobeBridge.GetAssetsDirectory()` fallback resolution to check output directory assets before unpacking embedded resources.

### Changed & Improved
- **Approved Branding Faithfulness:**
  - Directly extracted pixel-accurate assets from the approved reference image (`Z_Earth_Approved_Logo_Layout_Reference.png`):
    - Left emblem: Rounded-square (squircle) gradient from sky blue (`#0087E7`) to royal navy (`#00368D`) with bold geometric white **Z**.
    - Wordmark: Vibrant cyan-blue **Z** (`#009BFF`) and crisp white **EARTH** with cyan-blue apex triangle inside the letter **A**.
    - Subtitle: Letter-spaced `DESKTOP GIS`.
    - Excluded duplicate right circular icon.
  - Re-generated multi-resolution `.ico` (`16`, `32`, `48`, `64`, `128`, `256` px) and dark-theme high-DPI splash and about dialog logo banners.
  - Promoted verified build to the canonical single test preview directory (`dist\Z EARTH-Preview\Z EARTH.exe`).

### Added
- **Canonical Architecture Skeleton:**
  - Initialized single authoritative .NET 10 solution (`ZEarth.slnx` & `ZEarth.sln`) at `D:\My Software\Z EARTH`.
  - Created core domain model (`ZEarth.Domain`) with `CameraState`, `SceneMode`, `PlaceItem`, and `ProjectDocument`.
  - Created web globe bridge (`ZEarth.Globe`) with typed JSON request/response schema (`BridgeRequest`, `BridgeResponse`).
  - Created WPF MVVM application (`ZEarth.App`) targeting `net10.0-windows`.
- **Approved Branding & Visual Identity:**
  - Designed vector SVG assets in `assets/branding/` (`zearth_logo.svg`, `zearth_icon.svg`, `zearth_icon.ico`).
  - Implemented left rounded-square emblem with globe sphere, graticules, and stylized glowing Z.
  - Implemented `Z EARTH` bold wordmark with approved **blue triangle inside letter A**.
  - Subtitle `DESKTOP GIS` and developer credit: **Developed by Ahmad Ibrahim**.
  - Excluded duplicate circular icon as mandated by approved brand reference.
- **Splash Screen:**
  - Borderless, DPI-adaptive splash window with animated progress bar and stage reporting (Loading settings, Initializing WebView2, Loading local globe, Restoring places, Ready).
- **Main GIS Shell & Ribbon UI:**
  - Full GIS Ribbon interface with tabs: File, Home, View, Insert, Measure, Tools, Help.
  - Dockable Places panel with search, My Places, Temporary Places, and Saved Scenes.
  - Dockable Layers panel with Basemap, Terrain, and Overlays controls.
  - Resizable splitter (220–500 DIP range).
  - Bottom Status bar with live geographic coordinates (DD), altitude, heading, active CRS (EPSG:4326), provider attribution, and developer credit.
- **Globe Viewport & Navigation:**
  - Microsoft Edge WebView2 hosting offline 3D/2D geographic engine.
  - Spherical 3D globe projection and Plate Carrée 2D flat map mode.
  - Bundled offline vector continents and graticules (zero network required, no paid API keys).
  - Navigation controls: Orbit, Pan, Zoom In (+), Zoom Out (-), North Up (N), Reset Pitch, Home, Full Extent.
  - Extent history stack with Previous / Next navigation.
- **Project Persistence & Auto-Save:**
  - `ProjectService` supporting `.zearth` and `.zproj` format saving, loading, and recovery.
- **Diagnostics & Logging:**
  - `LoggerService` logging to `%LocalAppData%\ZEarth\logs\zearth.log` and in-app diagnostics.
- **Automated Testing Suite:**
  - 11 unit tests in `tests/ZEarth.Tests` covering Camera limits and sanitization, Bridge JSON contracts, Project serialization round-trip, and KML test fixtures.
- **Preview Build:**
  - Published and validated single preview build at `D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe`.
- **REA Integration Document:**
  - Authored `REA_LOCAL_INTEGRATION.md` in `Z_Earth_Desktop_Complete_Development_Plan`.
