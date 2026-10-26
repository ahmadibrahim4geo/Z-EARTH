# Google Earth reference — full-program second pass

**2026-10-26. Evidence:** user-provided Google Earth Pro Windows client directory on Drive, readable plain-text configuration and KML file metadata, cross-checked against ZEarth GitHub source. This is not an executable benchmark or a claim of code reuse. Clouds were just **one example**; they are NOT the entire research goal.

## New verified observations from provided installation files

| Reference file | Direct observation | Resulting ZEarth investigation |
| --- | --- | --- |
| client/qt.conf | Relative Qt plugin path and Windows DPI-awareness | Check native WPF/WebView2 high-DPI layouts and missing resources; no need to copy Qt. |
| client/drivers.ini | Texture pooling, GPU compression, mipmaps, vertex buffer objects, graphics capability fallbacks | Benchmark Cesium scene responsiveness, GPU texture allocation, offline recovery and frame rate; these are settings, not proven FPS. |
| client/PCOptimizations.ini | Bounding-volume generation and optimization settings | Examine Cesium culling and geographic grid primitive counts at extreme zoom. |
| client/res/flightsim/flightsim.ini | Flight simulation config lists F16 and SR22 aircraft, key/controller mappings, takeoff locations | Optional feature gap; native reference simulator operation NOT VERIFIED. Do not import aircraft files. |
| client/res/userpalette.kml | Legacy namespace http://earth.google.com/kml/2.1; counted 334 Style, 167 StyleMap and 304 hotSpot tags | Found and fixed namespace parsing gap in ZEarth KmlParser; regression tests use **newly created synthetic fixtures**, never the copyrighted palette. |
| client/res/ navigation, sidebar, toolbar, terrain, search, time, print, tour bundles | Categorized Qt resource packages exist | Reconstruct behavior specs only from permitted desktop UI observation; resource names do not establish feature correctness. |

## Comparison beyond clouds

| Workstream | ZEarth evidence and differentiator | Gap and action |
| --- | --- | --- |
| UI, commands and menus | WPF XAML ribbon, MVVM commands and bridge mapping | Validate every enabled icon and dialog with Windows UI Automation; mock tests not sufficient. |
| Camera navigation | Cesium 2D / 3D, optional Columbus mode and configurable compass | Preserve ZEarth extra modes, verify pole/dateline stability and camera history. |
| Grid/CRS | Per-axis intervals, named parallels and meridians, UTM zones; PROJ/GDAL | Keep distinctive grid features, fix stale labels and pole ghosting if reproducible. |
| KML/KMZ | Parser, serializer, style resolver, safe KMZ extraction | KML 2.1 namespace fixed. Next: StyleMap, icon, nested folders, NetworkLink, overlay, gx:Tour and resource roundtrips. |
| Terrain and rendering | Cesium lighting, atmosphere, sun and terrain managers, offline illustrative clouds | Real GPU performance evidence; optional legally licensed observed cloud imagery separate from illustration. |
| Search and Layers | Local name/coordinate search, remote provider fallback and layers | Benchmark offline/Arabic and multi-layer visibility, no paid Google service dependency. |
| Persisted user projects | ZEarth project and temporary-place persistence | Test restart/corruption recovery against independent fixture; do not request private real-world saved places. |
| Flight mode | No verified independent simulator in ZEarth; Google installation includes flight resources | Optional future open implementation after core GIS passes, not urgent. |
| Printing/export and language | Screenshot, KMZ/ KML, bilingual EN/AR UI | Measure print/export needs from live behavior; test localization rather than copying resources. |

## Exact additional Windows data locations

Primary user-state path: %USERPROFILE%\AppData\LocalLow\Google\GoogleEarth

Alternative path documented by Google: %LOCALAPPDATA%\Google\GoogleEarth

Ask only for a **sanitized filename/size inventory** and newly created synthetic KML/KMZ examples. Saved myplaces.kml contains location history: never upload by default. Avoid Cache, imagery tiles, passwords, account files and complete Registry dumps. The installed client folder was already supplied; do not request it again.

## Evidence classification
- CONFIRMED: user-supplied readable config and namespace/element metadata, ZEarth source files, source fix authored.
- PENDING: Windows .NET build for the new KML regression tests, full roundtrip observed execution.
- UNVERIFIED: actual Google Earth UI function, runtime speed, native parity and flight simulator behavior.
- Twelve separate GitHub Actions source-evidence jobs can run, but they are **not** independent AI research agents. An external agent host is necessary for that.

## Acceptance backlog
P0: KML legacy import and KMZ roundtrip + command/bridge audits + grid ghosting native checks.
P1: camera/compass, sunlight and search reliability.
P2: advanced tours/NetworkLink, licensed live weather overlays, optional flight simulator.
