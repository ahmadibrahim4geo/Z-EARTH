---
name: zearth-google-earth-pro-workflows
description: Apply pinned Google Earth Pro feature research to Z EARTH WPF/Cesium development, including real Esri Wayback timelines, Places/KML fidelity, network links, reference labels, profiles, and reliable QA.
---

# Z EARTH — Google Earth Pro workflows (agent skill)

This is a **project-local adapter**. Read it when modifying the Historical Imagery ribbon, the Cesium timeline/imagery providers, Places/Layers/KML/KMZ, tours, overlays, elevation profiles or scale-dependent labels. It does **not** grant the application runtime automatic access to the skill text. `AGENTS.md` routes development agents here.

## Mandatory inputs — load in this order

1. `../../../AGENTS.md`, `../../../docs/GOOGLE_EARTH_PRO_PARITY.md`, `../../../docs/HISTORICAL_IMAGERY_TIMELINE.md` (actual Z EARTH behavior wins over generic design examples).
2. `../../references/google-earth-pro-engineering.md` (research and acceptance patterns from Google Earth Blog; **historical** source).
3. `../../references/historical-imagery-timeline.md` (multi-provider imagery contract; a **future design**, not necessarily implemented).
4. On demand: `../zearth-globe-bridge/SKILL.md`, `../zearth-kml-integrity/SKILL.md`, `../kml-kmz-engineering/SKILL.md`, `../gis-security-guardian/SKILL.md`, `../../references/world-gazetteer-reference-layer.md`.

## Historical imagery: existing product vs future options

**Existing implemented-by-source path (needs native T3 verification):**

`HistoricalImageryCommand → WaybackService.GetReleasesAsync → GlobeBridge historical.loading/show/error → historical-timeline.js → ImageryManager.registerDynamicBasemap`.

- Esri Wayback catalog dates are **published basemap release dates**, not necessarily image acquisition dates. Show `Esri Wayback release` with real `releaseId`, release-date label, Esri attribution and coverage limitations. **Never rewrite** the label as `satellite acquisition date` or claim Google Earth coverage.
- The ribbon opens a **small, nonmodal slider inside Cesium**; no blocking WPF HistoricalImageryDialog and no blanked globe. Preserve camera, zoom, user overlays and previous basemap on close/Esc.
- Snap to genuine Wayback release records; rejected URLs (host allowlist), bad catalog, empty catalog, offline and 401/429/timeout must show a visible error. Abort/debounce scrub requests; never silently invent dates or fake imagery.
- Distinguish the app's Wayback release timeline from user KML `TimeStamp/TimeSpan/gx:Track` and the Cesium sunlight scene clock.

**Future real scene-acquisition browser (not shipped):** obtain source-specific scene IDs, acquisition timestamps/footprints and usage rights through a lawful provider registry; only then enable two-date comparison and acquisition-date labels. Do not promise Sentinel, Landsat, NASA GIBS, or Esri provider features merely because a reference describes them.

## Other Earth-style commands

| Request | Skill + acceptance |
| --- | --- |
| Imported Places, folders and symbols | `zearth-kml-integrity` + `kml-kmz-engineering`: preserve source icon bytes, styles, order, visibility, altitude and ExtendedData through save/reopen, including unknown XML |
| NetworkLink / Region / LOD | Local KML contract; respect parent visibility, allowlisted opt-in requests, refresh throttling, safe KML balloons and offline state |
| World Gazetteer labels | `../../references/world-gazetteer-reference-layer.md`: standalone Reference Layers toggle, viewport/zoom priority, Arabic/English labels; never overwrite KML icons |
| Tours / camera navigation | `zearth-camera-navigation` + Cesium viewer/time skills: WPF command → bridge acknowledgment → JS effect → persistent state |
| Elevation profile | Real numeric DEM samples and correct Z datum + NoData; no fake profile heights or assumption that rendered imagery is a DEM |
| Terrain + vertical exaggeration | `cesiumjs-terrain-environment` + `zearth-globe-bridge`: verify both DEM root and current viewport tile, distinguish ellipsoid fallback, apply an acknowledged multiplier only to loaded numeric terrain, visually compare terrain in native Windows 3D |
| Geographic grid / major parallels | `gis-cartography-symbology` + `coordinate-reference-systems`: separate key lines, lat/lon intervals and labels, ground-clamped terrain and adaptive camera updates; expose true independent options |
| UTM zone overlay | `coordinate-reference-systems` + `zearth-verification`: respect UTM latitude extent, exceptional Norway/Svalbard bounds and date line, label the visible zone near the camera instead of permanently at the Equator |
| Overlays | Safe KML/KMZ raster bundle paths, georeferencing, alpha, zoom-aware loading and authorized tile provider |

## Engineering and evidence gates

Before code changes identify the real WPF command, bridge message, JS receiver, data service, rendering side effects and unit/native test. Preserve single portable Preview output. Record T0/T1/T2/T3/T4 separately; Node tests and .NET mocks do **not** certify native GPU/WebView2 functionality. For legal use, do not copy Google Earth code, icons, trademarks, or imagery; follow Esri/other source terms. This skill is instructions for a coding agent, not an embedded AI engine or runtime workflow.
