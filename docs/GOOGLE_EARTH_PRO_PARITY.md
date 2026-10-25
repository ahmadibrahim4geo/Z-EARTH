# Z EARTH versus Google Earth Pro — functional parity and recovery plan

**Review date:** 2026-10-25. **Product direction:** independent, bilingual, free-first Windows desktop globe/GIS inspired by Google Earth Pro's useful workflows, not a copy of Google's copyrighted source, imagery, design assets, or branding.

**Reference evidence:** official [Google Earth Pro measurement, circle and elevation profiles](https://support.google.com/earth/answer/148134?hl=en), [Google Earth Pro vector imports and Temporary Places](https://support.google.com/earth/answer/176685?hl=en), [placemarks and stored camera view](https://support.google.com/earth/answer/148142?hl=en), [coordinates / UTM and search](https://support.google.com/earth/answer/148068?hl=en), [GEBCO_2026 elevation+ocean grid](https://www.gebco.net/data-products-gridded-bathymetry-data/gebco2026-grid).

## REA engineering evidence boundary

**Identified the correct upstream:** [morluto/rea](https://github.com/morluto/rea), MIT-licensed **Reverse Engineer Anything** (REA), a local MCP/CLI analysis tool. The developer previously configured it at `D:\\My Software\\Zearth\\rea` and root `.gitignore` deliberately excludes `/rea/`; it is **not** `ahmadibrahim4geo/REA`. Official REA README, desktop-app inspection, .NET inspection and responsible-use docs were reviewed. The **local REA installation, any Google Earth Pro binary or runtime observation, and their local evidence have NOT been accessed in this cloud GitHub review**. Use [the agent's REA local workflow](../.github/skills/zearth-rea-parity/SKILL.md) on the user's authorized Windows machine, respecting the REA disclaimer and application licenses. No REA executable, third-party app binary or proprietary extracted code is copied into the Z EARTH repository.

## Feature contracts, implementation phases and truthful status

| Feature / user behavior | Current reviewed observation | Acceptance target | Skill |
| --- | --- | --- | --- |
| Explorer layout | Search panel and My Places tree with Saved Scenes + Temporary root already exist; not a final two-root Google Earth-like Layers hierarchy | Search and Layers independently expandable, My Places / Temporary as clearly labeled siblings; migrate legacy project safely | `gis-desktop-application-development` |
| Startup Scenes | Project stores Saved Scenes in child folder; first scene is startup, but reordering controls missing | User moves scenes ↑/↓, first becomes startup, persists through save, reopen and a clean app launch | `gis-desktop-application-development` |
| KML/KMZ | Parser and KMZ extractor exist; before this change imported into My Places first | Imported *whole source tree* goes into Temporary; explicit move to My Places; preserves folders, names, visibility and styles | `kml-kmz-engineering` |
| Shapefile/GeoJSON/GPKG | VectorDatasetReader and CRS dialog exist; external vectors went to My Places | Imported root into Temporary, CRS handling correctly transforms, no orphaned SHP or lost attributes | `geospatial-data-processing`, `coordinate-reference-systems` |
| Search | Local Places first, Photon online fallback, coordinate search supports EPSG | Local-first Arabic/English name search, throttling/privacy, place/coordinate results, select coordinate units, fail offline without fake results | `geospatial-api-integration` |
| Basemap gallery | Large grouped WPF ribbon, default ✓ already implemented, all icons same globe | Expressive theme icon/thumbnail, offline first, default persisted with ✓, explicit online provider state/credits | `geospatial-web-mapping`, `geospatial-api-integration` |
| Terrain sources | Ellipsoid / AWS Terrarium / Cesium ion alternatives exist; **Cesium ion is not a free permanent universal prerequisite** | Real 3D mesh/heightmap from selected provider incl. land and seabed where data supports it; coherent fallback and attribution | `terrain-hydrology-lidar` |
| GEBCO_2026 | Not found as a correctly tiled active Cesium terrain source | Download a permitted GEBCO raster subset, cache/store provenance, convert to validated Cesium terrain tiles, serve local tile metadata; test ocean **negative** heights and datum | `terrain-hydrology-lidar`, `geospatial-data-processing` |
| True elevation profile | Current popup used **synthetic terrain** for absent path or missing heights | **No fictional Z values**; interpolate and sample the **selected active DEM**, negative/zero included, pin chart to bottom resizable pane, hover crosshair syncs with globe, export metadata/CSV and later Excel | `terrain-hydrology-lidar`, `geospatial-web-mapping` |
| Draw tools | Point/path/polygon/circle exist | Add geodesic rectangle, ellipse, multi-ring polygon, precise vertex editor, snapping and undo/redo; test anti-meridian and geometry validity | `geospatial-data-processing` |
| Measure tools | Distance/area routines exist; this iteration adds larger numbered point circles and draggable HUD | Point/line/path/polygon/circle/ellipse/3D measurement; verify moving panel and vertex markers in real WebView2, table of lon/lat and chosen CRS/UTM | `coordinate-reference-systems` |
| Coordinates | CRS registry/PROJ conversions exist | Show decimal degrees and UTM with hemisphere/zone, optionally 3rd user-selected projected EPSG, explicitly distinguish X/Y order and linear units | `coordinate-reference-systems` |
| Compass | Modes already present in UI/domain | Verify all 3 modes, precise north reset, stable heading/pitch synchronization with Cesium | `gis-desktop-application-development` |
| Theme | WPF resource dictionaries and DWM title bar theming exist | Full dark neutral charcoal (no blue cast); light mode complete; white/black accessible foreground contrast including title bars/dialogs | `gis-desktop-application-development` |
| Symbol library | Placemark ColorHex and polygon/path style fragments present | Editable point symbol/icons, size and labels; line style/width/dash; polygon fill/outline/transparency; preserve styles in KML/KMZ | `kml-kmz-engineering`, `geospatial-data-processing` |
| Import/export | KML/KMZ, GeoJSON/SHP/GPKG pipeline exists | Round-trip nested geometry and attributes, known field maps, correct CRS and Z, no unannounced loss | `kml-kmz-engineering` |

### Priority and acceptance

**P0 / data integrity:** No fake elevation profile; no false active-provider claims; imported feature tree stays in intended Temporary folder and saves safely; startup-scene order preserved. First slice implemented on feature branch; verify actual app behavior separately.

**P1 / core usage:** Search (name/coords, Arabic), offline-first & online provider safety, basemap default/icons/availability, 2D/3D navigation, .kml/.kmz round-trip and project recovery. Add fixture tests including corrupted KMZ, nested styles, no-network and multi-file SHP.

**P2 / scientific 3D terrain:** GEBCO_2026 15-arc-second DEM land+seafloor, proper source vertical datum and NoData, quantized-mesh or validated compatible Cesium terrain, local serving/caching, single active terrain source, true profile sampling and chart docking. Avoid equating GEBCO WMS visualization with elevation data. GEBCO is free public-domain data *with disclaimer and requested citation*; imagery and hosted tile services have their own terms.

**P3 / GIS drawing & measuring:** Two-click angular rectangle drawing is now implemented with standalone Node math regression tests (rejects dateline spans pending split-polygon support); draw ellipse/circle/polygon/paths with stable geometry model; numbered vertices; snapping; undo/redo; UTM and arbitrary EPSG transforms; measured precision and export; robust latitude/poles/dateline rules.

**P4 / presentation & release:** Fully bilingual UI, high-contrast neutral dark/light including title bar, meaningful basemap icons, refined three-mode compass, responsive pane layout; repeatable portable build and evidence-based Windows acceptance.

## GEBCO architecture contract

- Use the *numeric raster* (2026 release: WGS84 geographic approx; global 15 arcsec, elevation meters including negative seafloor values). A **WMS image is not height data**.
- Source: official GEBCO grid/download/OPeNDAP, record version/date/DOI and TID reliability where available. Datum is generally modelled Mean Sea Level but source areas can differ; disclose.
- A global 43200 × 86400 grid is too large for per-frame direct JS loading. Convert **selected region or preprocessed tiles** using licensed GDAL/PROJ into a Cesium-compatible terrain tiling scheme, with nodata/water masks and tested negative Z. Don't claim direct GEBCO URL is a quantized-mesh server.
- Terrain / Profile / cursor heights / exports must refer to the *same selected numerical elevation source*. When terrain absent, show ellipsoid and "No terrain samples", not made-up results.
- Include tests for Cairo highland, delta near sea-level, a known sea depth, unavailable tiles, dateline and expected vertical units.

### Historical imagery time scrubber — implementation (2026-10-25)

The previous `HistoricalImageryDialog.ShowDialog()` was blocking. The ribbon now invokes a **nonmodal in-Cesium timeline overlay** with prev/next, year/date, slider, first/latest and restore-on-close. Releases come from Esri Wayback catalog's validated `itemURL` / `releaseDateLabel`; no fabricated fallback dates. The Esri release time **must never be labeled image acquisition time**; location-specific metadata and local-update filtering remain future work. See [technical guide](HISTORICAL_IMAGERY_TIMELINE.md), plus Node/C# tests and T3 Windows/browser acceptance requirement.
## Agent development loop

1. Load repository `AGENTS.md`, pinned `gis-development` router, selected task skill and local `zearth-*` / `cesiumjs-*` guide.
2. Write reproduction script/fixture **before** changing source; identify whether code path reaches Cesium and returns a verifiable result.
3. Isolate work by feature branch: data model/persistence → bridge → scene engine → WPF panel → tests → user-facing docs.
4. Run actual Windows CI for WPF & .NET and Node syntax; explicitly distinguish T2 fake-engine tests from real T3 GPU/WebView2 and T4 clean install.
5. Never replace the single `Z EARTH-Preview` until the functional gates pass. Prevent lost Places and credentials in logs.

**Scope note:** This document is a prioritized engineering acceptance specification. It does not imply that all P1–P4 features have been implemented or tested.
