---
name: kml-kmz-engineering
description: Preserve original KML/KMZ styles, icons, hierarchy, assets and Google Earth semantics during secure import, rendering, editing and round-trip export in desktop, web and GIS hosts.
---

# KML & KMZ Engineering

**Free-first default:** Follow [GIS free-first and licensing policy](../../references/free-first-policy.md); test open-source parsers where suitable, and disclose licensed host dependencies separately.

Use this skill when a GIS application or toolbox reads, writes, converts, renders or packages KML (OGC KML 2.2/2.3) or KMZ (ZIP package containing KML and resources). Load only the sections relevant to the task.

**Mandatory implementation reference:** [Google KML/KMZ document fidelity and compatibility contract](../../references/google-kml-kmz-compatibility.md), grounded in [Google's documentation](https://developers.google.com/kml) and the [OGC standard](https://www.ogc.org/standards/kml/). Use the [self-contained XML fixture](../../examples/kml-kmz/README.md) for offline regression checks. Google Earth `gx:` extensions are not core KML and need independent feature-level support.

## Earth explorer compatibility and dynamic KML

For a Google-Earth-like globe UI, apply the [Google Earth Pro feature reference](../../references/google-earth-pro-engineering.md) alongside the [dated imagery UI contract](../../references/historical-imagery-timeline.md). Imported KML `TimeStamp` / `TimeSpan` / `gx:Track` control document content time; imagery-provider acquisition dates are **not** KML feature time and must not be overwritten by the application's timeline selection.

- For `NetworkLink`, `Link` and `Region/Lod`, preserve source XML, refresh policy, visibility overrides and regions; only fetch within an explicit trust/allowlist, time/size limits and user opt-in.
- Keep `gx:Tour`, `gx:FlyTo`, `gx:Wait`, camera and source document nodes for round-trip export even if the renderer does not support tour playback.
- Show remote link health and stale/offline state without erasing imported folders or defaulting unsupported icons to a destructive new style.
- Test **KML import -> displayed hierarchy/time and styles -> user edit -> exported KMZ -> reopen** independently from date-aware imagery loading.

## Primary rule: protect the imported author's intent

- **Import must preserve, not restyle.** Never replace original KML/KMZ icons, colors, opacity, line widths, label styles, folder hierarchy, popup descriptions or highlight behavior with program defaults. Do not auto-assign one symbology to every imported placemark.
- **Display and conversion are separate operations.** Open KML as a source-retaining document, not merely a GeoJSON/FeatureClass table. Keep unrenderable/unknown nodes, style references and local resource bytes available for export. For geometry-only conversion, show a loss report before irreversible steps.
- **Non-destructive:** original files and immutable bytes remain intact, edits apply to a new copy, and unchanged items retain native `Style` / `StyleMap` / `styleUrl` bindings. Track the source hash and changed nodes.
- **Declare capabilities:** parsing, on-screen rendering and exporting have separate support matrices. If a feature cannot be rendered in Cesium/MapLibre/ArcGIS/QGIS, warn and retain the source element rather than claiming fidelity.
- **Acceptance begins with reopen:** imported KML/KMZ -> displayed styles/hierarchy -> copy-on-write edit -> save/export -> reopen, compare geometry, styles, icons, extended data, altitude and assets. Only actual Google Earth and target-host tests prove visual parity.

## Official Google implementation details

- **Colors:** KML hex `aabbggrr`, not CSS `rrggbb`; `ff0000ff` is opaque red. Preserve alpha, `colorMode`, `normal`/`highlight` StyleMap pairs, icon `hotSpot`, label scale and polygon fill/outline.
- **Resources:** `href` can reference icons, images, overlays, COLLADA models and remote KML. For portable KMZ, prefer a top-level `doc.kml` plus relative asset paths; preserve model texture relative-resolution semantics and include local assets on export.
- **Temporal/3D:** honor `TimeStamp`, `TimeSpan`, `gx:Track`, `gx:Tour`, `Camera`, `LookAt`, `Region`/`Lod`, extrusion and KML/gx altitude modes; list extensions not supported by the runtime.
- **Explorer / Places:** preserve `Document`/`Folder` ordering, `visibility`, `open` and feature IDs. Imported Temporary Places and explicitly saved My Places are different state transitions. Keep camera state and persistent scene/project state distinct from standalone exported KML/KMZ.
- **Security first:** local/remote KML and KMZ are untrusted. Harden XML, archive paths/limits, network link fetch opt-in, HTML descriptions and URL schemes, while keeping original text in nonexecuting storage.



## Requirements discovery
- Identify target runtimes (ArcGIS Pro, legacy ArcMap, QGIS, GDAL/OGR, Cesium, Google Earth, MapLibre), versions and licensing.
- Identify whether **geometry-only conversion** or **faithful KML document rendering** is needed. Do not claim GDAL/OGR or ArcPy feature conversion preserves all KML constructs.
- Record required feature support: Placemark, Point, LineString, Polygon with holes, MultiGeometry, folders, ExtendedData, Style/StyleMap, icon and label styles, GroundOverlay, NetworkLink, Region/LOD, Camera/LookAt, TimeStamp/TimeSpan, altitudeMode, extrude, gx:Track, gx:Tour and COLLADA Model. Mark unsupported features explicitly.

## Coordinate systems and geometry
- KML geographic coordinates are longitude, latitude, optional altitude in WGS 84; never silently treat them as projected coordinates. Validate coordinate order, range, geometry rings, dateline crossings and altitude interpretation.
- Transform incoming/outgoing project CRS explicitly, using tested axis-order settings and datum transformations. Preserve Z where supported; distinguish clampToGround, relativeToGround and absolute altitude.
- Validate XML namespace handling, missing geometry, malformed XML, encoding, and empty files.

## Safe KML/KMZ loading
- Parse XML with secure settings: disable external entities, DTD resolution and external fetches; prefer defusedxml for untrusted Python inputs.
- Treat KMZ as untrusted ZIP: reject absolute paths, ../ traversal, symlink escapes, encrypted/unsupported members, excessive member count, oversized expanded payloads and extreme compression ratios. Enforce configurable total uncompressed size and per-entry limits *before* extraction; extract only into a controlled temporary directory.
- Do not automatically fetch NetworkLinks, remote icons, models or overlays. Require explicit user consent, restrict schemes to approved protocols, apply timeouts and size limits, and document privacy/network behavior.
- Resolve relative resource paths within the package without allowing escape. Avoid unbounded in-memory XML trees or decompression; use streaming/chunking where practical.

## Shared Icon Manager integration — KML/KMZ-specific adapter

The [Unified GIS Icon Manager](../../references/unified-gis-icon-manager.md) is the shared icon library for **all GIS hosts**; KML/KMZ is a specialized consumer, not an independent library. Expose `Use Original Icon` (import default), `Preview New Icon`, `Choose New Icon` (explicit), and `Reset to Original` in a host-specific UI; selected edits should be reversible and saved to a new output unless user authorizes overwrite. Keep source document/style data separate from icon-catalog choices.

For newly created placemarks only, the [offline SVG/PNG icon demo](../../examples/icon-manager/README.md) and [PNG→new KMZ example](../../examples/icon-manager/export_new_kmz.py) demonstrate generating a portable PNG and embedding a relative `IconStyle/Icon/href`, preserving source bytes and optional attribution. Default tint **white `ffffffff`** to avoid accidentally multiplying/darkening a colored PNG; apply explicit tint only when asked. If icon shape has a non-bottom hotspot, export its correct `hotSpot` rather than assuming all shapes use a pin-tip anchor. Export does not automatically change earlier imported KML/KMZ. Color values use KML aabbggrr.

External candidates include CC0 Pinhead/Maki/Temaki, CC BY-SA Map Icons Collection, selective *actually open-source* Streamline sets (not standard free asset library), and independently built Mapular-inspired generator. See [license/source catalog](../../references/cartographic-style-sources.json). Never copy Mapular's site code or Streamline's non-open-source free icon inventory.

## Icon-library separation: imported source vs new placemark

Treat these separately (see [preservation-first KML/KMZ contract](../../references/kml-kmz-symbol-preservation.md)):

- `imported_source_style`: immutable original `Style` / `StyleMap` / `styleUrl`, colors `aabbggrr`, icon href/hotSpot/scale, label, line/poly styling, media bytes and hierarchy from KMZ. The original style takes priority on every import, scene restoration and no-edit export.
- `presentation_fallback`: *temporary*, clearly warned, and never written to source or KMZ if the renderer cannot show an external/unsupported icon.
- `explicit_user_override`: user opts into selecting an alternate symbol, color or line pattern, with original saved for Undo/Reset; export only after chosen edit.
- For **new, user-authored** features, [8 open/free icon libraries and Cesium PinBuilder](../../references/map-marker-icon-library.md) may be used after license review; include portable PNG icons and safe relative hrefs when exporting new KMZ. **Never replace imported icons with these packs by default.**
- Use [offline KMZ style/assets checker](../../examples/kml-style-preservation/README.md) to detect unintended color, icon bytes and StyleMap reference changes; separately validate full KML/OGC semantics, renderer output, Google Earth and target GIS host compatibility. Do not claim pixel-perfect rendering from ZIP integrity tests.

## Import and rendering
- Separate semantic document model (folder hierarchy, styles, resources, times, metadata) from GIS feature layers and renderer. Preserve original KML for features not representable in the target GIS schema.
- Support icons, labels, line/polygon styles and overlays where the renderer allows; document Google Earth extension gaps rather than silently dropping them.
- Keep a provenance and conversion-loss report with counts, unsupported nodes and warnings. A successful geometry import is **not** proof of faithful styling, temporal behavior or portable asset references.

## Export and round trip
- Produce valid XML with correct namespaces, escaped text, geometry coordinate order and explicit style/resource references.
- Package a portable KMZ with the main KML document and all local referenced resources using safe relative paths. Avoid leaking machine-local paths or credentials.
- Test KML -> import -> export -> reimport and KMZ -> unpack -> repack -> reopen. Compare geometry/topology, CRS, feature counts, folder hierarchy, ExtendedData, time, styles, assets and altitude semantics. Classify expected versus unintended loss.
- Include compatibility fixtures for Google Earth, ArcGIS Pro, ArcMap (legacy), QGIS and Cesium; label tests as unverified until executed in real hosts.

**Repository interoperability inventory:** [KML/KMZ open-source project review](../../references/kml-kmz-repository-review.md), including Google libkml, GDAL/OGR LIBKML, fastkml, PyKML, Cesium and QGIS. These are review candidates, not claims of feature parity or successful host testing. See the [generic reliability guide](../../docs/PERFORMANCE_RELIABILITY.md).

## Performance, stability and crash prevention
- Benchmark increasing feature counts, deeply nested folders, many icons, large overlays and large KMZ archives. Measure peak RAM, elapsed time, UI responsiveness and recovery after cancellation.
- Apply memory budgets, lazy loading, background workers, progress reporting, cancellation and resource cleanup. Do not block the UI thread for large parsing or rendering tasks.
- Test corrupted ZIP/XML, zip bombs, missing icons, invalid coordinate strings, broken links and repeated open/close cycles. Record crash logs and memory trends.
- Never advertise a universal maximum feature count: publish measured environment-specific limits and graceful degradation behavior.

## Suggested implementations
- Python: defusedxml + zipfile with explicit resource limits; GDAL/OGR (KML or LIBKML driver when available) for feature conversion; PyQGIS for QGIS-specific layers.
- ArcGIS Pro: validate supported arcpy conversion tools in the installed release and test actual outputs. ArcMap: isolate Python 2.7/ArcPy compatibility code and test in its legacy host; do not assume shared Python 3 dependencies work.
- Web/desktop 3D: use a KML-capable renderer such as Cesium where supported, and explicitly test extensions. MapLibre-based applications generally require conversion or a separate KML parser and renderer.
- Check component licenses, bundled binaries, data rights and runtime availability; prioritize free/open-source paths.

## Acceptance checklist
- [ ] Explicit fidelity mode selected: read-only view, document edit or lossy feature conversion
- [ ] KML color order, StyleMap states, icon anchors and shared style references verified
- [ ] Import never automatically overrides external source styles or changes original bytes
- [ ] Maki/Pinhead/other open icons offered **only for new/user-restyled** objects; imported original assets persist
- [ ] Save/reopen compares style XML/StyleMap and original icon resource hash; temporary fallback not persisted
- [ ] KML/KMZ adapter reuses general GIS Icon Manager; `Use Original Icon` is default for imports
- [ ] New PNG→KMZ generated with safe relative href, alpha/ABGR, hotspot and license provenance, without touching existing imported KMZ
- [ ] Google KML official references consulted, gx extension support verified by host version
- [ ] Explorer folder/visibility state, ExtendedData and Temporary/My Places save semantics preserved
- [ ] Target host and supported KML constructs documented
- [ ] Malformed and malicious inputs rejected safely
- [ ] Coordinate/altitude semantics verified
- [ ] Local KMZ assets and folder/style metadata retained where promised
- [ ] Round-trip and cross-host fixtures executed
- [ ] Performance/stress/cancellation tests reported with measured limits
- [ ] Unsupported constructs and conversion losses disclosed
- [ ] Tests distinguished from unverified recommendations
