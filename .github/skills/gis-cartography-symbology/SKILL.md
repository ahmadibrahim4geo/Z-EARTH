---
name: gis-cartography-symbology
description: Design, automate, apply and verify GIS symbols, vector renderers, raster color ramps, labels and accessible layouts in ArcMap, ArcGIS Pro, QGIS, MapLibre, Cesium and desktop GIS, with license-aware reusable style sources.
license: MIT
---

# GIS Cartography, Symbology & Styling

**Free-first default:** Follow [no-cost GIS and upstream licensing policy](../../references/free-first-policy.md); proprietary ArcGIS hosts are optional, while individual styles/fonts retain their own rights.

Use for point, line, polygon, raster, labels, legends, scientific color maps, map layouts and reusable cartographic style libraries. Load only this skill and the target host's specialist; do not load every GIS skill.

## Read first
- [Curated, license-aware catalog](../../references/cartographic-style-sources.json) and [selection guide](../../references/cartographic-style-guide.md).
- [Own ArcMap Style Collection](https://github.com/ahmadibrahim4geo/ArcMap-Style-Collection) is the primary external discovery source. It contains third-party assets with *independent* licenses. Reference assets by URL and exact path, not by copying or automatically installing.
- [Cartography example](../../examples/cartography/README.md) is a testable MapLibre vector fixture, **not** proof of ArcGIS/QGIS renderer parity.
- [Open map marker/icon libraries](../../references/map-marker-icon-library.md) cover eight icon packs plus Cesium PinBuilder, distinguishing CC0, Apache, CC BY-SA, MIT plugin, SIL OFL font and other asset rights; [mandatory imported KMZ fidelity](../../references/kml-kmz-symbol-preservation.md) takes precedence on external files.

## Arcade profile-aware expressions

For ArcGIS labeling, popup, visualization, attribute rules, dashboards and form calculation, use the [official Arcade repository](https://github.com/Esri/arcade-expressions) and [profile guide](../../references/arcgis-arcade-profiles.md). First choose the target Arcade execution profile, then validate supported globals/functions, return type, nulls, locale, field availability and native save/reopen. Arcade expressions are not interchangeable with ArcPy, SQL, MapLibre styles or imported KML/KMZ symbology.

## Action contract
1. Inspect actual geometry (Point/MultiPoint, LineString/MultiLineString, Polygon/MultiPolygon, raster bands/pixel type), coordinate system, scale, field semantics, nulls and category/numeric distribution; verify raster NoData, statistics and band definitions. Distinguish geographic units from graphic symbol units.
2. Ask for, or infer from known evidence, purpose, target map type, expected audience, background/basemap, print vs screen, software + version, language and accessibility requirements. Never mistake category labels for a continuous numerical scale.
3. Choose renderer **before** choosing a palette:
   - qualitative categories -> unique values and distinct colors/patterns;
   - ordered numeric -> sequential scale;
   - departures around meaningful neutral -> diverging with explicitly defined center;
   - exact thresholds -> classified renderer with documented cutoffs;
   - magnitude on points -> sizes or proportional symbols (check area vs radius);
   - concentration -> heatmap only with meaningful aggregation/kernel and units.
4. Design **Point** symbols: shape/SVG/marker/font, fill/stroke, size, rotation, anchor, opacity, labels, clustering only when appropriate. For **Line**: width, color, dashes, casing, arrows/flow direction, pattern and scale-aware behavior. For **Polygon**: fill, outline, hatches, pattern, transparency, multipart/hole handling. For **Raster**: single-band grayscale, stretch, min/max, percentile, classified/pseudocolor, RGB composites, scientifically suitable ramps, hillshade and NoData/alpha treatment.
5. Use compatible formats per host: ArcMap legacy `.style` and `.lyr` + `arcpy.mapping` where supported; ArcGIS Pro `.stylx`, `.lyrx`, `arcpy.mp`/CIM (version-pinned); QGIS XML/QML/QGZ + PyQGIS renderers; MapLibre Style Spec paint/layout expressions; Cesium entity/material primitives (not drop-in MapLibre styles). Do not silently equate these file formats.
6. Generate a **preview** with a legend, representative features, min/max, null/NoData, RGB and alpha channels; check collision/overlap, hierarchy, grayscales/CVD accessibility, Arabic/English text shaping, projected vs geographic display, low/high zoom and export scale.
7. Apply on a copy or reversible in-memory layer where feasible. Save explicitly in host-native format; reload and compare renderer type, fields, stops, colors, opacity, labels and artwork references. Do not modify source geometry, raster pixel values or overwrite project files without user approval.
8. Capture evidence: source license, name/version, target GIS runtime, renderer, attribute/classification method, color stops, symbology JSON/native layer file, screenshot/export if available, readback and failures. Test in the actual licensed target before claiming native success.

## Source and permission policy
- **Free-first:** prefer independently licensed QGIS, MapLibre, OS GeoDataViz, CARTOColor and scientific palettes where use terms permit; rank cost and host dependency distinctly.
- Repository MIT licenses do **not** automatically relicense included Esri/USGS/BGS files, fonts or converted styles; verify per-file rights, attribution and embedding/redistribution rules before shipping.
- Do not download/run untrusted bundled scripts, font installers, SVG scripts or external QML expressions automatically. Sanitize URLs and validate imports.

## Unified GIS Icon Manager (general-purpose first)

Treat map/icon asset catalogs as a **single reusable library for all GIS workflows**, not a KML-only library. Load the [Icon Manager architecture, user workflows and acceptance contract](../../references/unified-gis-icon-manager.md) when an application needs icon search, custom icon design, favorites, previews or reuse across ArcMap/ArcGIS Pro, QGIS, MapLibre, Leaflet, Cesium and desktop GIS.

- General core: bilingual Arabic/English searchable icon IDs/categories, user-owned assets, preview, provenance and license; shape/glyph/fill/stroke/opacity/size/anchor controls; SVG/PNG outputs or host-native renderer adapters; accessible contrast and scale.
- Source intake: [catalog](../../references/cartographic-style-sources.json) includes the new Mapular icon generator (**inspiration/reference only, do not copy site code**) and Streamline tag search (**discovery by default**). Streamline's standard free assets must **not** be made a selectable/reusable/downloadable library inside an icon-manager product; use only separately verified open-source sets with required credits and appropriate rights. Map Icons Collection now offers SVG alongside PNG; credit CC BY-SA 3.0, no hotlinking, check adaptation conditions.
- Use icon library to style **new** GIS features, new projects, and **user explicitly restyled** selected features. Export to each host's compatible icon format with license evidence; do not promise automatic conversion from CSS/web font to native GIS symbol.
- KML/KMZ is a **specialized client** of the same library. Imported document icons/styles always win, even across project saves/reopens. The [reference Icon Manager browser prototype](../../examples/icon-manager/README.md) and [new-only PNG→KMZ exporter](../../examples/icon-manager/export_new_kmz.py) demonstrate the authoring side, **not** opening/editing an imported KMZ.

## Map icons and the KML/KMZ branch

1. **New layers / new user-authored placemarks only:** search [style source catalog](../../references/cartographic-style-sources.json) by map-icon library, license, target platform and KML packaging role. Favor CC0 Pinhead, Maki and Temaki for general maps; optionally Material Symbols, licensed Map Icons Collection, Scott Map Icons or Leaflet marker generators. Cesium PinBuilder is a runtime generator, not an importer.
2. When exporting *new* KML/KMZ icons, generate portable transparent PNGs from licensed SVG/canvas pins, include safe relative `files/icons/...` paths, and author user-chosen `IconStyle` color/scale/hotSpot/heading. Browser icon fonts, SVG elements and CSS are not portable substitutes for KML images.
3. **Imported KML/KMZ must never be automatically re-styled:** source `Style`, `StyleMap` normal/highlight, `styleUrl`, all icon images, line/polygon color/width/opacity, labels and folders override default app theme/marker pack. Save original resource bytes and style links. A missing icon fallback is *display-only*, not an exported replacement.
4. Allow restyling a selected imported object only by an **explicit user command** with Undo/Reset to original and Save As. Do not modify source files, shared styles or sibling placemarks by accident. Record whether the choice modifies one feature or a shared style.
5. Test [offline KMZ resource/style comparison](../../examples/kml-style-preservation/README.md) and real host rendering independently. Follow [KML/KMZ import/export contract](../../references/kml-kmz-symbol-preservation.md) and the existing [Google/OGC fidelity guide](../../references/google-kml-kmz-compatibility.md).

## Imported KML/KMZ style exception

When symbols originate in an imported KML/KMZ, user-authored styling wins over application palettes. Apply [KML/KMZ preservation rules](../kml-kmz-engineering/SKILL.md) and [Google styling semantics](../../references/google-kml-kmz-compatibility.md) (KML ABGR color, StyleMap normal/highlight, hotspot, inline/shared styles, KMZ assets). Never replace them with a default cartographic renderer without explicit user action; record unsupported style capabilities separately.

## Cross-skill routing
- ArcGIS Pro / ArcMap -> [ArcGIS Toolbox Engineering](../arcgis-toolbox-engineering/SKILL.md); QGIS -> [QGIS Plugin Development](../qgis-plugin-development/SKILL.md).
- 2D/3D web -> [Geospatial Web Mapping](../geospatial-web-mapping/SKILL.md); independent GIS UI -> [Desktop GIS](../gis-desktop-application-development/SKILL.md).
- Spatial processing / raster stats -> [Geospatial Data Processing](../geospatial-data-processing/SKILL.md); CRS -> [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md).
- Sun position, time/location, device environment -> [Device Context](../gis-device-context/SKILL.md).

## Acceptance
- [ ] Correct variable semantics and renderer choice, legend/units/NoData, CVD check
- [ ] Tested native format and target version; original geometry/pixels unchanged
- [ ] External assets' exact license/attribution verified; fallback available if not
- [ ] Preview, apply, save/reopen and real appearance verified (or limitations explicitly recorded)
- [ ] Imported styled KML/KMZ retained original colors, StyleMaps, local image bytes and labels without automatic icons/palette overrides
- [ ] General GIS icon library shared across host adapters; all hosted/licensed sources classified; Streamline free non-open assets excluded from reusable icon picker
- [ ] New-icon SVG/PNG preview/export and new-KMZ packaging demonstrably separate from preservation-first importing
