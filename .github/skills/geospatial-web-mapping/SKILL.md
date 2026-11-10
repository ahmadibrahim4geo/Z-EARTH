---
name: geospatial-web-mapping
description: Build and evaluate web maps and desktop-embedded MapLibre/GeoLibre 2D/3D UIs, with real geometry editing, layer state, historical imagery time controls, raster/COG rendering and correctly licensed basemaps.
license: MIT
---

# Geospatial Web Mapping

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use when the deliverable is a web GIS map, browser-based spatial tool or mapping UI in a desktop application.

For verified upstream *repository availability* (not runtime readiness), choose a web application, 2D/3D renderer, native map SDK or tile server from the [GIS software source registry](../../references/gis-software-source-registry.md). The existing project catalog is the single source of truth; avoid creating a second inventory.

## Procedure

1. Clarify if it is a standalone web app, Tauri/WebView2 desktop shell or GeoLibre project.
2. Read `../../references/desktop-and-web.md`, `../../references/geolibre-integration.md` and `../../references/gishub-software.md`.
3. Choose mapping renderer (MapLibre/Cesium/GeoLibre) separately from processing engine (GDAL/Whitebox-WASM/Rust/Python).
4. Use official modules from https://github.com/opengeos/maplibre-gl-components, layer-manager, geo-editor, time-slider and plugin-template as architecture candidates **only after version checks**.
5. Implement a real user action end-to-end before adding more ribbon/toolbar commands; persist layer visibility, style, selections and camera.
6. Ensure layers use correct CRS and NoData, handle feature editing with undo, validate project save/open and loading failures.
7. Historical layers/time controls require actual dated data from legal sources; do not fake or imply imagery availability.
8. Validate remote data licensing, keys, attribution, offline limitations, provider rates and CORS. Read `../geospatial-api-integration/SKILL.md` and `../../references/free-geospatial-apis.md` for approved source adapters: geoBoundaries, NASA FIRMS/CMR/EONET, HeiGIT routing, water/air layers and data disclaimers. Do not mistake discovery metadata for imagery tiles or OSM edit dates for real-world construction dates.
9. Run in browser/desktop where possible, check event handlers and real rendering; otherwise state what remains untested.

## Acceptance

- [ ] Renderer and data dependencies separated
- [ ] Buttons work, not visual placeholders
- [ ] Export, save, reopen and undo functional
- [ ] Responsive map, long data performance, time axis accuracy
- [ ] API terms, CRS, attribution and secrets checked

## Blue Marble 3D Tiles and terrain forks

See the [Global Mapper / Blue Marble reference](../../references/blue-marble-global-mapper.md) for forked Cesium 3D Tiles Tools and an older Cesium Terrain Builder. They are independent utilities, not the proprietary Global Mapper renderer. Check terrain tiling scheme, NoData, dataset licensing, 3D Tiles metadata/transform and server support before using in CesiumJS or MapLibre.

## Earth timeline and Google Earth Pro behavior

For browser or embedded-Cesium globe UIs, consult [Google Earth Pro patterns](../../references/google-earth-pro-engineering.md) and [Historical Imagery Timeline](../../references/historical-imagery-timeline.md). Keep imagery timeline controls docked/nonblocking; query scene availability per AOI; display actual acquisition/mosaic dates and provider licensing; cancel stale requests and never blank the globe on missing imagery. Compare mode shares camera/bounds, not two independently drifting map viewpoints. This is a design reference only, not a claim of live implementation.

## Earth View: globe-to-modal architecture

Review [Earth View](../../references/earth-view-architecture.md) for typed base/overlay imagery provider registries, WMS 1.3.0 EPSG:4326 BBOX axis order, temporal images, camera controls and regional zoom/view-state restoration. Avoid copying code without an upstream license; do not infer displayed date is the satellite acquisition date or assume optional AI controls are visible.

## OpenGeoS deeper integration candidates

- [COG/GeoTIFF client](https://github.com/opengeos/maplibre-gl-raster): inspect bands, NoData, overviews and source pixels; remote access requires CORS/range support.
- [GeoParquet](https://github.com/opengeos/maplibre-gl-geoparquet) and [DuckDB spatial](https://github.com/opengeos/maplibre-gl-duckdb): verify spatial extension, security of SQL input, CRS and memory usage.
- [3D Tiles](https://github.com/opengeos/maplibre-gl-3d-tiles), [historical Esri Wayback imagery releases](https://github.com/opengeos/maplibre-gl-esri-wayback), [layer controls](https://github.com/opengeos/maplibre-gl-layer-control): all need pinned compatible MapLibre versions, genuine live click tests, and upstream service rights. Wayback release date is not automatically acquisition date.
- [OpenGeoS advanced guide](../../references/opengeos-advanced-integration.md): MapLibre plugins are not drop-in CesiumJS adapters.

## Browser and WebView device permissions

For browser geolocation, notifications, file picking, camera/microphone, WebUSB/serial/BLE or embedded host access, consult [System Capabilities & Permissions](../system-capabilities-and-permissions/SKILL.md) and Security Guardian. Enforce HTTPS/secure contexts, origin and Permissions Policy, real user gestures and restricted WebView/Electron/Tauri bridges. Keep map scene/camera position distinct from user's physical GPS location via [GIS Device Context](../gis-device-context/SKILL.md).

## Visual assets (conditional)

When the reviewed project needs thumbnails, official logos, photographs, illustrations or screenshots, compose [Visual Asset Discovery & Integration](../visual-asset-discovery-integration/SKILL.md). It owns internet/source discovery, asset suitability, source credits and prepared local variants; keep layout, target resource binding, packaging and actual UI acceptance with this platform/UX owner. Do not silently replace requested images with generic icons or claim a source URL proves local integration.
