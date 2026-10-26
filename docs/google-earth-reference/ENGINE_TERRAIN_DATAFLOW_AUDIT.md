# Engineering investigation: Google Earth Pro vs ZEarth — terrain, rendering and data flow

Date: 2026-10-26. **This is a real source-level examination, not a new skill and not a simulated set of AI agents.** The Google Earth reference is grounded in user-authorized read-only installation/configuration metadata and **public Google documentation**. ZEarth evidence comes from the actual `main` branch source. Nothing below claims the hidden Google Earth native networking protocol has been reverse engineered, or that both EXEs were exercised interactively together.

## 1. Three distinct evidence levels
- **G-DOC:** Google's published functional behavior (terrain quality, vertical exaggeration, elevation profiles, KML network links/Region/LOD). Defines externally observable contracts, not private internal code.
- **G-FILES:** Qt deployment, drivers.ini texture/mipmap/GPU support, PCOptimizations.ini bounding volumes, shaders/ and resource filenames in supplied Google Earth Pro installation. Shows *configuration categories*, not executing algorithms.
- **Z-CODE:** actual WPF/C# → WebView2 → JavaScript → open DEM/imagery provider and back, with exact source anchors listed below. Not native GPU/FPS proof.

Google public references:
- https://support.google.com/earth/answer/148134?hl=en — paths and Show Elevation Profile; elevation requires terrain layer.
- https://support.google.com/earth/answer/148070?hl=en — terrain quality, GPU/rendering mode and safe mode, exaggeration .01–3.
- https://support.google.com/earth/answer/148186?hl=en — terrain navigation, elevation exaggeration controls.
- https://developers.google.com/kml/documentation/regions — Region + LOD visibility and demand loading of large KML/overlay collections.
- https://developers.google.com/kml/documentation/kmlreference — NetworkLink view refresh and time refresh.
- https://developers.google.com/kml/documentation/altitudemode — clampToGround vs relativeToGround vs absolute and seafloor modes.

## 2. What is known — **Google Earth Pro**
Google's documentation confirms:
1. **Interactive 3D terrain**, terrain quality/LOD settings and graphics/safe mode selection; changing quality can trade detail for responsiveness.
2. **Elevation profiles sampled along paths**, plotting distance/elevation/slope; not limited to altitudes the path happened to store. Terrain must be available.
3. **KML network-on-demand:** Region/LOD and NetworkLink permit view-dependent loading: large rasters are tiled/split; higher-resolution content is selected when a geographic region occupies enough screen area.
4. KML `altitudeMode`: drape on terrain, offset from terrain, use absolute altitude, plus documented gx seafloor extensions.
5. The provided installation's `drivers.ini` lists GPU texture compression, mipmaps, VBOs, texture pooling and renderer compatibility decisions; `PCOptimizations.ini` has bounding-volume optimizations.

**Unknown and not to be invented:** Google's proprietary server endpoints, exact native message structs, terrain mesh encoding/streaming, cache eviction algorithms, private source implementation, internal engine ownership and hidden rendering pipeline. A DLL list is not sufficient evidence. Study observable UI, public docs and permitted synthetic test fixtures instead.

## 3. ZEarth terrain: proven source-level data/command path

```text
WPF View (ToggleTerrainCommand / terrain model setting)
  │ MainViewModel.ExecuteToggleTerrainAsync / ActivateConfiguredTerrainAsync
  │ reads TerrainProviderId, CustomTerrainUrl, optional CesiumIonToken
  ▼
GlobeBridge.SetTerrainProviderAsync(type, url, ionToken)
  │ SendRequestAsync correlation ID, 45s deadline
  ▼
MainWindow.PostWebMessageAsJson(bridge JSON) → WebView2
  ▼
cesium-engine.js handleHostMessage('terrain.setProvider')
  │ TerrainManager.setProvider(type, url, ionToken)
  ├── Offline ellipsoid → Cesium.EllipsoidTerrainProvider (no real relief)
  ├── AWS DEM → real root + viewport numeric tile checks
  │    │ fetch https://terrain.local/terrarium/{z}/{x}/{y}.png
  │    ▼
  │    MainWindow.TerrainWebResourceRequested → validated path/zoom
  │    │ HttpClient → public AWS Mapzen/Tilezen Terrarium PNG
  │    └── WebResourceResponse PNG + CORS to local origin
  │    ▼
  │    createImageBitmap → Canvas RGBA → RGB→metres
  │    │ height=(R*256 + G + B/256)-32768
  │    │ 256×256 PNG downsampled to 65×65 float heights
  │    ▼
  │    Cesium.CustomHeightmapTerrainProvider (WebMercatorTilingScheme)
  │    └── Cesium tiles/rendering/camera screen-space error 1.5
  ├── Cesium World Terrain → requires valid ion access
  ├── Bathymetry → requires ion; separate layer source
  └── Custom terrain URL → CesiumTerrainProvider.fromUrl
  ▼
JS terrain.changed / terrain.fallback + correlated terminal response
  ▼
WebView2.WebMessageReceived → trusted origin check
  ▼
GlobeBridge.HandleWebMessage → MainViewModel state, status, logs
```

Anchors on `main`:
- `src/ZEarth.App/ViewModels/MainViewModel.cs` lines **3801–3910**: terrain choice, status, UI command.
- `src/ZEarth.Globe/GlobeBridge.cs` lines **489–608**, **739–745**: IDs, timeouts/cancellations, terrain request.
- `src/ZEarth.App/Views/MainWindow.xaml.cs` lines **328–342**, **442–462**, **477–568**: WebView2 host messages, trusted-origin readback, terrain.local→AWS proxy.
- `src/ZEarth.Globe/Assets/cesium-engine.js` lines **756–1007**, **3595–3640**, **4000–4050**: terrain tile fetch, numerical decoding, renderer, protocol acknowledgement.
- `src/ZEarth.App/ViewModels/MainViewModel.cs` lines **1063–1100**: numeric DEM probe and fallback events.
- `src/ZEarth.Domain/Terrain/TerrainProviderConfig.cs` lines **5–85**: provider list and exaggeration bounds.

The user-facing program **does** have an engine request/response architecture and a public open-data terrain path; no Google proprietary service is required for the AWS DEM route. This trace is much more precise than inferring a Google protocol from DLL filenames.

## 4. Real technical gaps, ranked

### P0 — Elevation Profile is not derived from the terrain
`src/ZEarth.App/Views/ElevationProfileDialog.xaml.cs` lines **27–74** reads **only stored vertex altitude**. It explicitly warns: `The active DEM is not sampled by this viewer yet.` It calculates statistics using the provided vertex Z rather than DEM samples along the actual path; this cannot match Google Earth Pro's documented *terrain* elevation profile.

**Required independent feature:** densify a drawn path with defined geodesic spacing, query the **currently selected authorized terrain provider** for heights at all samples (Cesium terrain sampling API or a separate properly georeferenced DEM reader), build a real distance-versus-terrain-elevation plot, keep source attribution, units, vertical datum and NoData/error distinctions, and never pretend original Z=0 equals missing elevation. Keep current vertex-Z analysis as an explicitly labeled alternative. Add interpolation/resolution bounds and test flat, mountain, coast and failed provider cases.

### P0 — KML/KMZ Region/LOD loading has no demonstrated live implementation
`src/ZEarth.Domain/Kml/KmlParser.cs` preserves many KML constructs (NetworkLink, overlays, gx markup) as `PreservedXmlFragments`; preservation for export is **not equivalent to executing** the feature in the viewport. Google's official KML Region/LOD behavior is demand-driven streaming of large user-owned KML overlays; evidence of working Region activation, dynamic NetworkLink refresh, and SuperOverlay tile streaming in ZEarth remains absent in the inspected render path.

**Independent implementation:** for allowed local/remote KML resources, interpret Region/LOD and link refresh with URL allowlists, quotas, cancellation, inheritance, explicit attribution, protected resource loading and no background access to secret/private addresses. Don't claim parity because the XML survives roundtrip.

### P1 — Source provider quality and fallback state
- The AWS path only confirms **two** numeric tiles, not all subsequent geographic tiles. The provider can be activated while distant regions are still loading/failing. This is an honest readiness probe, **not** full global elevation coverage proof.
- Heightmap is downsampled to `65×65` while source has `256×256` pixels; needs error-vs-speed benchmark (sharp ridges, seams, zoom levels, hill shading, memory). The provider stops requesting tiles above L14. These are conscious implementation trade-offs, but final accuracy is unmeasured.
- Terrain height promises are cached up to 160 tile keys in an insertion-order Map, not a full measured GPU/cache eviction policy. Validate hit ratio and cancellation at fast zoom/pan.
- Failure `catch` paths switch back to `EllipsoidTerrainProvider`, but not all paths visibly reset `viewer.scene.globe.depthTestAgainstTerrain` to false. This is a **source-code risk** of inconsistent depth/occlusion after a terrain fallback, not a confirmed on-screen bug.
- Initial WGS84 ellipsoid is intentionally **not a DEM**. The status panel should never imply elevation availability from it.

### P1 — Bridge health and observed GPU correctness
The WebView2 protocol has correlated requests, terminal responses and optional `accepted` progress, with a 45-second terrain timeout and a trusted virtual host. That's a concrete ZEarth architectural strength. Need: real per-request latency, 95th-percentile initialization time, JS exception counters, DEM first tile/viewport tile response, rendered frame and GPU evidence, request-cancel correctness. **Successful command acknowledgement does not prove a frame changed**.

### P1 — Distinguish KML altitude semantics
Current `cesium-engine.js` `getHeightReference()` handles `absolute`, `relativeToGround`, everything else as clamp-to-ground. Google's documented `gx:relativeToSeaFloor` and `gx:clampToSeaFloor` require a separate bathymetric implementation or explicit unsupported notice. A KML file that is preserved on export may still be rendered at the wrong elevation.

### P2 — ZEarth advantages to retain
- Independent horizontal/vertical grid intervals, UTM overlay and major geographic lines.
- 2D/3D and custom compass/north controls.
- Multiple open/conditional-free terrain providers and vertical exaggeration range 0.2–10 versus Google documented .01–3.
- Clear no-token ellipsoid fallback; ability to use custom terrain under independently verified source licenses.
- WPF→WebView2 correlated/timeout/error protocol is readable and testable — a genuine engineering asset.

## 5. Precisely how to verify (native Windows acceptance, not a static check)

| Scenario | Setup | Pass evidence |
| --- | --- | --- |
| Sinai rugged terrain | same coordinates, 3D camera/tilt, AWS DEM enabled | S3 tile status, z/x/y and decoded height min/max, rendered screenshot, elapsed time |
| Plain/desert | flat area, same tilt | no invented peaks; elevation near independent DEM reference within documented source uncertainties |
| Missing network | disable internet → DEM ON | clear fallback reason; ellipsoid state and depth test reset; no UI freeze |
| Camera fast zoom | pan across zoom 2→14 in succession | bounded memory, no ghost tiles or broken request IDs, valid fallback |
| Elevation profile | path with no embedded Z values | real terrain sample chart after provider load, provenance and no-data warnings; currently expected to fail |
| KML SuperOverlay | fully user-owned tiled synthetic overlay, Region+LOD+NetworkLink | only active regions fetched and displayed, inactive data not loaded; currently not verified |
| Sea-floor altitude | synthetic gx altitude files + supported bathymetric provider | correct seafloor drape/offset or honest unsupported notification |
| Startup and shutdown | clean install/offline, repeated modes | no crash/blank globe, accurate status, no false success |

## 6. Deployment of actual AI analysts
**This document is not proof of twelve independent AI workers.** Existing CI runs separate *deterministic source-contract probes* for 8 R and 4 Z slots. A true team needs an enabled external AI-agent executor with permissions to run multiple independent model contexts, assigned scoped files/reference sources, and collect reproducible reports. Those agent-launch capabilities are **not exposed in the current connected tools**. Do not describe the GitHub Actions probes as LLM agents; do not label a source audit as operating Google Earth Pro.

**No code from Google Earth copied.** Google Earth binary install files cannot establish its proprietary network or engine internals; black-box observable operation and documentation are legitimate baseline sources.
