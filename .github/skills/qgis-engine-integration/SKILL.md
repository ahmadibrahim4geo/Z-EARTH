---
name: qgis-engine-integration
description: Integrate QGIS Core, GUI, Analysis, Processing, PyQGIS and QGIS Server into independent GIS software with cross-platform architecture selection, safe data interchange, license review and native acceptance tests.
license: MIT
compatibility: Native tests and deployment require a separately installed version-matched QGIS runtime.
---

# QGIS Engine Integration

Use this skill for **QGIS as a reusable GIS engine**, rather than an extension *inside* QGIS. Consult the [official architecture and repositories guide](../../references/qgis-engine-integration.md), [free-first policy](../../references/free-first-policy.md), and [GIS Security Guardian](../gis-security-guardian/SKILL.md).

## Route by delivery model

1. **QGIS-hosted plugin / Processing provider**: route to [QGIS Plugin Development](../qgis-plugin-development/SKILL.md) instead of shipping a standalone runtime.
2. **Native Qt/C++ or PyQt independent desktop application**: choose an exact QGIS release; use QGIS Core for spatial I/O/CRS, GUI (`QgsMapCanvas`) for 2D maps, Analysis/Processing for operations and QGIS 3D only for validated native 3D requirements. Creating a canvas alone does not recreate all QGIS desktop features.
3. **Headless worker or batch process**: initialize `QgsApplication([], False)` in the *matching* installed PyQGIS environment, register required providers, set explicit Processing context/feedback, use `qgis_process` only where available and version-tested.
4. **Existing independent application (e.g., .NET/WPF, web mapping, Rust, desktop Qt or a 3D globe)**: retain the project's verified rendering architecture unless a migration is explicitly required. Add QGIS as an **optional** version-matched processing/data service using GeoPackage/GeoJSON/COG, stable command-line calls or validated IPC. Do not assume Qt GUI widgets can be embedded in unrelated UI frameworks. The engine must be selectable, not forced on every project.
5. **Remote GIS maps**: choose QGIS Server for suitable OGC services; do not confuse a web-service backend with a native renderer. For jobs needing only reprojection or file transformation, assess GDAL/PROJ directly before bundling QGIS.

## Engineering procedure

1. Record exact QGIS LTR/release, OS/CPU architecture, Qt, Python, PyQt/SIP, GDAL/OGR, PROJ, GEOS, platform plugins and provider paths. Never assume an arbitrary `pip install qgis` supplies the native application libraries.
2. Separate the UI shell, 2D/3D renderer, GIS engine, data/service providers, project state and packaging model.
3. Define observable journeys: open vector/raster -> validate CRS/data -> transform/analyze -> save/export -> reopen/read back, plus restart, cancellation, failure and offline recovery.
4. Treat datum/axis order, geodesic versus projected units, Z/vertical CRS, NoData, geometry validity, Unicode/field schemas and output provenance as first-class correctness constraints.
5. Do **not** equate KML/KMZ geometry import with Google Earth style fidelity. For folders, StyleMaps, gx extensions, timestamps, altitude, icon assets and source-preserving export, compose [KML/KMZ Engineering](../kml-kmz-engineering/SKILL.md).
6. Implement a bounded worker with validated file paths, least permissions, explicit cancellation, progress, safe temp/atomic writes, startup/teardown error handling and no open/unprotected localhost listener. QGIS project/GUI objects must not be mutated from unsafe background threads.
7. Perform a dependency and upstream license review. QGIS core source is **GPL-2.0-or-later**; distributing a modified/linked GIS product may trigger reciprocal obligations. A separate process does not automatically resolve copyright analysis. Check Qt, plugins, binaries, icons and data rights separately; the MIT license of this documentation **does not relicense QGIS**.
8. Produce the [acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md): T0 source review, T1 offline tests, T2 QGIS-native headless, T3 interactive desktop, T4 clean-machine packaged run. Never claim T2-T4 based on source checks or an unexecuted example.

## Compatibility with the other GIS skills

| Need | Compose with | Integration contract |
| --- | --- | --- |
| QGIS-hosted plugins | [QGIS Plugin Development](../qgis-plugin-development/SKILL.md) | Do not duplicate host lifecycle/providers |
| Standalone desktop GIS | [GIS Desktop Development](../gis-desktop-application-development/SKILL.md) | Runtime isolation, installer, GUI/worker tests |
| Raster/vector I/O and scientific analysis | [Geospatial Data Processing](../geospatial-data-processing/SKILL.md) | Explicit CRS, type, NoData, output readback |
| Coordinate operations | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) | EPSG and transformations, units, datum/grids |
| Web/3D client mapping | [Geospatial Web Mapping](../geospatial-web-mapping/SKILL.md) | Rendering remains independent of processing |
| Spatial databases | [Spatial Databases](../spatial-databases/SKILL.md) | Transactional writes, schema, SRID, security |
| KML/KMZ interchange | [KML/KMZ Engineering](../kml-kmz-engineering/SKILL.md) | Preserve semantics, styles and assets separately |
| APIs, discovery and data catalogs | [Geospatial API Integration](../geospatial-api-integration/SKILL.md), [Data Discovery](../geospatial-data-discovery/SKILL.md) | Auth, quotas, terms, provenance |
| Agents, localization, security | [Geospatial AI Agents](../geospatial-ai-agents/SKILL.md), [GIS Internationalization](../gis-internationalization-arabic/SKILL.md), [GIS Security Guardian](../gis-security-guardian/SKILL.md) | Least privilege, validated outputs, bilingual/RTL coverage |

**No hidden activation:** loading this skill must not install QGIS, modify the host UI, replace a renderer, overwrite files, or introduce a paid dependency. In all stacks, unsupported commands remain explicitly unavailable rather than appearing functional.

## Deliverables and examples

- Architecture decision record, dependency/version matrix, license assessment and data/IPC contract.
- [Native-only PyQGIS GeoPackage round-trip example](../../examples/qgis-engine/README.md) and [existing native smoke test](../../native_tests/qgis_acceptance.py).
- Independent desktop route: [GIS Desktop Development](../gis-desktop-application-development/SKILL.md); plugin route: [QGIS Plugin Development](../qgis-plugin-development/SKILL.md).

## Acceptance checklist

- [ ] Exact runtime, providers, ABI and deployment prerequisites verified
- [ ] CRS, units, transform grids, data format and save/readback tests recorded
- [ ] User input, worker permissions, timeouts, cancellation and crash recovery tested
- [ ] Styling and KML/KMZ round-trip parity assessed explicitly where relevant
- [ ] QGIS GPL and every redistributed library/asset license assessed
- [ ] Native T2-T4 host runs observed or clearly recorded as not run
