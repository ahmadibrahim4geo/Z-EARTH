# Architecture Decision Record: ADR-012
## Evaluation of GeoLibre as Secondary 2D/3D Map Adapter

- **Status:** **APPROVED (Documented & Preserved for Future Phases)**
- **Date:** 2026-10-20
- **Author & Lead Architect:** Ahmad Ibrahim
- **Task ID:** ZE-057 (Sprint 8: Advanced GIS & Localization)
- **Reference Repository:** [opengeos/GeoLibre](https://github.com/opengeos/GeoLibre)

---

### 1. Context and Problem Statement

Z EARTH Desktop utilizes **CesiumJS 1.146.0** running natively in an embedded Microsoft Edge WebView2 virtual host (`https://cesium.local/`) as its primary 3D globe and 2D planar map rendering engine. CesiumJS provides robust WGS84 ellipsoidal mathematics, 6DOF camera dynamics, and 3D geospatial entities. 

The task is to evaluate **GeoLibre** (an open-source map ecosystem integrating MapLibre GL JS and OpenLayers) as a candidate dedicated 2D rendering engine or secondary viewport adapter for Z EARTH Desktop:
1. Does GeoLibre offer superior vector tile rendering performance in 2D?
2. What are the packaging, offline licensing, and bundle size implications?
3. How feasible is bi-directional interop between .NET 10 WPF and GeoLibre?

---

### 2. Comparative Technical Analysis

| Architectural Dimension | Current Architecture (CesiumJS 1.146) | Candidate: GeoLibre (MapLibre GL JS) |
| :--- | :--- | :--- |
| **3D Spherical Globe** | Native WGS84 ellipsoid with horizon guards and depth clamping. | Experimental globe projection; primarily optimized for 2D Web Mercator (EPSG:3857). |
| **2D Planar Map** | Built-in seamless morphing (`SceneMode.SCENE2D` Columbus View / Mercator). | Native high-performance WebGL 2D vector tile rendering with Mapbox Style Spec. |
| **Offline Distribution** | Completely bundled locally in `GlobeAssets` without Node.js dev server. | Requires bundling MapLibre JS + MapLibre GL CSS + offline sprite/glyph packs. |
| **Snapping & Geodesics** | Shared `SpatialSnapIndex` and WGS84 Vincenty/Excess geodetic calculations. | Screen-space snapping supported; planar turf.js calculations differ slightly from ellipsoidal. |
| **C# WPF Interop** | Typed JSON message bridge via `window.chrome.webview.postMessage`. | Identical WebView2 message routing pattern. |
| **Bundle Footprint** | ~35 MB uncompressed (Cesium JS, Assets, Workers, Widgets). | ~4.5 MB for MapLibre core; additional 20 MB for offline fonts/glyphs. |
| **Open Source License** | Apache 2.0 (compatible with MIT / commercial distribution). | BSD-3-Clause (fully permissive). |

---

### 3. Gap Analysis & Key Findings

1. **2D Vector Tile Performance:**
   - GeoLibre/MapLibre GL JS exhibits exceptional vector tile rendering speeds (MVT / Protobuf) and client-side styling (Mapbox Style Spec) in 2D mode, utilizing GPU shaders for line extrusion and symbol clustering.
   - For datasets exceeding 100,000 dense polygonal vertices in 2D flat mode, MapLibre provides higher frame rates than CesiumJS Columbus view.

2. **3D Globe Parity Limitations:**
   - MapLibre GL JS globe projection does not match CesiumJS's sub-millimeter WGS84 ellipsoidal camera controls, terrain mesh water masks, or true geodesic distance rendering over continental extents.
   - Dual-engine maintenance (Cesium for 3D + MapLibre for 2D) would create synchronization overhead and risk state divergence across mode transitions.

3. **Offline Font & Glyph Dependency:**
   - MapLibre requires PBF font glyphs (`fonts/{fontstack}/{range}.pbf`) and JSON sprite sheets for offline symbol rendering. Shipping a fully offline MapLibre bundle would increase offline deployment payload by ~25 MB.

---

### 4. Architectural Decision

**Decision:** **Retain CesiumJS 1.146 as the Unified 2D/3D Geographic Engine for Z EARTH Desktop Core.**

- **Rationale:** CesiumJS already achieves 60 FPS in both 3D and 2D modes with full coordinate parity, surviving the rigorous 100-cycle morph stress test (AC-013) with zero context crashes. Maintaining a single rendering engine minimizes memory footprint and eliminates cross-engine state desynchronization.
- **Future Integration Vector:** The neutral `IMapViewport` and `GlobeBridge` interfaces designed in Sprint 0 and Sprint 1 cleanly decouple WPF MVVM from the underlying web engine. A GeoLibre 2D adapter can be provided as an optional plug-in or secondary viewport panel in post-MVP releases for high-density MVT vector tile streaming workflows without altering core domain persistence (`.zearth`).
