---
name: gis-desktop-application-development
description: Architect, implement, package and verify independent Windows GIS desktop applications (Qt/PySide, Tauri/Rust, C# WPF/WebView2) with real mapping and geoprocessing tools, offline data, 2D/3D renderers, bilingual UX, installers and source-to-release QA. Use when converting an ArcGIS Python toolbox into a standalone executable or building a GIS desktop product.
license: MIT
compatibility: Requires the target desktop build toolchain and GIS runtime to run integration tests; the skill itself is documentation.
---

# GIS Desktop Application Development

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use this for **independent executable applications**, not ArcGIS Pro SDK add-ins or a QGIS plugin. Select host OS, packaging model, renderer and processing engine separately. A working toolbar mockup is not a completed GIS app.

## Global Mapper SDK is a licensed engine, not a free backend

The [Blue Marble Global Mapper guide](../../references/blue-marble-global-mapper.md) explains why proprietary Global Mapper DLLs and Pro tools cannot be embedded in our open-source GIS desktop app without explicit SDK and redistribution rights. For a stand-alone alternative, use independently licensed engines and test equivalent features instead of assuming source availability from Blue Marble's public GitHub forks.

## External Earth View desktop reference

Read [Earth View architecture](../../references/earth-view-architecture.md) for an Electron web-shell + loopback API pattern, **not** a WPF/Cesium drop-in. Upstream license not observed: no source reuse. Do not copy its plaintext desktop key storage: use OS-protected secrets, validate localhost origin/CORS and installer integrity.

## Read only what the task needs

- Engineering and release acceptance: `../../references/desktop-application-engineering.md`
- Rendering/UI architecture: `../../references/desktop-and-web.md`, `../../references/geolibre-integration.md`, `../../references/dotnet-gis.md`
- Correct map/CRS APIs: `../coordinate-reference-systems/SKILL.md`
- Scientific data and API access: `../geospatial-data-discovery/SKILL.md`
- Real MIRSAD Desktop development experience: `../../references/ahmad-ibrahim-gis-case-studies.md`
- GIS backend and I/O: `../../references/geospatial-foundations.md`
- Imported KML/KMZ exact styles, asset bundles, ExtendedData and hierarchy: [KML/KMZ Engineering](../kml-kmz-engineering/SKILL.md) and [Google interoperability contract](../../references/google-kml-kmz-compatibility.md). In Earth/Explorer-style desktop apps, keep imported user styles and Temporary/My Places separate; geometry-only conversion is not faithful rendering.

## Google Earth Pro patterns and historical imagery (reference only)

When implementing a globe explorer inspired by Google Earth Pro (such as ZEarth), load [Google Earth Pro engineering patterns](../../references/google-earth-pro-engineering.md) and, for dated satellite imagery, the [Historical Imagery Timeline contract](../../references/historical-imagery-timeline.md). These are independent implementation specifications derived from historical blog observations plus current Google/KML guidance, not reusable Google Earth source or permission to embed Google imagery.

- Keep time navigation in a small nonblocking dock/overlay, not a full-map modal; retain Cesium camera, layers, terrain and user map.
- Show **actual acquired or mosaicked scene dates**, footprints and provider credits; next/previous only visit available scenes, with explicit missing/offline/error states.
- Preserve separate Temporary Places and My Places trees, multi-select/move/undo, NetworkLink/Region/LOD lazy loading and imported KML/KMZ style fidelity.
- Sample elevation profiles only from declared DEMs (including vertical datum) and synchronize chart crosshair with map. For tours, preserve camera interpolation/stop and source gx:Tour nodes even where playback is unavailable.
- Do not claim a WPF/GlobeBridge/CesiumJS implementation exists until T3 GUI integration and T4 packaged Windows tests, including a timeline-open/globe-visible test.

## Gazetteer and reference place-name layers

A Google-Earth-style desktop product should offer **Reference Layers > World Gazetteer** as an optional scale-aware place-name overlay **independent of the Search Bar**. Implement the [World Gazetteer label/search contract](../../references/world-gazetteer-reference-layer.md) using local GeoNames data by default, with language-specific Arabic/English names, priority/decluttering, viewport/3D-horizon culling, and persistent user toggle. Never make a remote request on every camera tick; do not overwrite imported KML/KMZ icon styling or save transient labels to My Places. Prototype: [offline viewport labels](../../examples/gazetteer/README.md). Runtime WPF/Cesium integration must be separately verified.

## Esri samples: reference, not a hidden standalone engine

[Esri .NET mapping samples](https://github.com/Esri/arcgis-maps-sdk-dotnet-samples) and [Pro SDK snippets](https://github.com/Esri/arcgis-pro-sdk-snippets) can inform UI patterns. First decide whether the host is ArcGIS Pro, separately licensed ArcGIS Maps SDK, or an independent WPF/Cesium app. Pro SDK add-ins cannot be transplanted into standalone Desktop executables. Keep optional Esri REST/services adapters isolated with explicit rights; see [license matrix](../../references/esri-open-source-integration.md).

## Implementation workflow

1. Specify the supported Windows versions, 64-bit process, user profiles, input/output formats, offline constraints, languages, architecture, versioning, installer and actual target test PC.
2. Choose a **single** application shell (Qt/PySide6, Tauri+React/Rust or WPF/.NET) by requirements. Do not combine frameworks without a justified interoperability design.
3. Separate business calculations, geospatial I/O, basemap/network services, 2D/3D renderer adapters, persistent project/layers state and window/ribbon commands.
4. Create an acceptance table with **one observable, user-triggered operation per command**. Implement a complete flow: load data -> analyze -> render -> save/export -> reopen and inspect. Hide unfinished commands; do not ship clickable placeholders.
5. Define a CRS policy: detect layer CRS; explicit transformations and grids; preserve original coordinates; distinguish horizontal from vertical datum and meters from angular degrees. Test real DEM NoData and cell alignment.
6. Use independently licensed formats/engines (GeoPackage, GeoJSON, COG/GeoTIFF, GDAL/PROJ where permitted). FileGDB write support must be verified for the selected driver and deployment license; do not assume ArcPy can be redistributed.
7. Treat HTTP imagery, terrain, climate, boundary, population, hazard and routing providers as separate external dependencies. Use `../geospatial-api-integration/SKILL.md` and the current versioned API catalog; validate API keys, cache/offline permissions, request caps, retries, CORS and attribution. Never bundle free personal provider keys in redistributable desktop executables.
8. Isolate calculation workers and never block the UI event loop with network downloads or raster loops. Support cancellation, progress, large datasets and crash recovery; test worker cleanup.
9. Make Arabic/English directionality, geospatial field aliases, symbol labels, keyboard navigation, DPI scaling and dialogs functional in both languages. Preserve scientific metadata in exported files.
10. Build an installer from a clean environment, **not** the developer's machine state. Check source-to-binary identity, bundled runtime licenses, updater rollback, user-data preservation, security, uninstall and reinstallation.
11. Run unit/schema tests, I/O round trips and a separate **installed executable** test on a clean Windows machine. A CI build-only result cannot certify desktop UX or runtime data sources.
12. Deliver changed files, recorded build/source hashes, fixture/output checks, screenshots or logs from actual tests, and unresolved platform/raster/provider gaps.

## Validation contract

| Tier | Required evidence |
| --- | --- |
| Source | Entry points, dependency lock, licenses, separate executable and project working directories |
| Headless | Deterministic numerical cases and real-format readbacks with known expected values |
| UI integration | Click every advertised ribbon/menu button, inspect geoprocessing output and event/state behavior |
| Clean packaged deployment | Install/run/export/save/reopen/uninstall from a non-developer machine; retain user data |
| Publication | Explicitly accepted candidate/release hashes; versioned release notes and independent stable-vs-dev distinction |

**MIRSAD Desktop example:** the independent PySide6 desktop code lives inside the MIRSAD repository but the current published installer is a **development** build, not a validated stable release. Do not claim newer source-only features are present in that installer. Consult upstream `MIRSAD Desktop/docs/STATUS.md` for accepted vs outstanding work.

## Never claim success merely from

- A ribbon screenshot, tool name or a `QAction` signal connected to a stub.
- A native ArcGIS Pro toolbox loading (this is separate from standalone desktop acceptance).
- Successful Python imports on a developer PC without clean packaging.
- Mock ArcPy, static fixture or an existing online tile showing up once.
- An exported JSON project that cannot reopen with the same CRS, layers and symbology.
