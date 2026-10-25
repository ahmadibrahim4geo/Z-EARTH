---
name: arcgis-toolbox-engineering
description: Design, audit, debug, port and test real ArcGIS Python Toolboxes (.pyt) and script tools for ArcMap and ArcGIS Pro, with runtime-specific ArcPy APIs, reproducible geoprocessing QA, safe geometry edits, scientific/engineering outputs and an independent Desktop migration path.
license: MIT
---

# ArcGIS Toolbox Engineering

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Apply when creating ArcGIS Pro/ArcMap geoprocessing tools, migrating ArcPy functionality into a standalone app, or verifying claims that a toolbox "works." This guidance is informed by real, separately maintained examples from Ahmad Ibrahim. **Do not edit those examples unless asked; do not silently clone their code into this skill.**

## Entry points and evidence

Read `../../references/ahmad-ibrahim-gis-case-studies.md` and the real code in:

- [MIRSAD](https://github.com/ahmadibrahim4geo/MIRSAD): `Toolboxes/*.pyt`, `mirsad_core/`, `tests/`, `MIRSAD Desktop/`, `Documents/TESTING.md`.
- [CreateFlowArrow](https://github.com/ahmadibrahim4geo/Create-FlowArrow-Tool): `CreateFlowArrow.pyt`, `CreateFlowArrow.py`, `data/`.
- [GeologicalCrossSectionToolbox](https://github.com/ahmadibrahim4geo/GeologicalCrossSectionToolbox): `GeologicalCrossSectionToolbox.pyt`, `geo_cross_section_engine.py`, `Data_Test/`.
- For CRS accuracy, load `../coordinate-reference-systems/SKILL.md`; for free imagery/climate/DEM and the current 39-service API catalog read `../geospatial-data-discovery/SKILL.md` and `../geospatial-api-integration/SKILL.md`.

## Official Esri examples, loaded selectively

- [Esri/arcpy](https://github.com/Esri/arcpy) publishes release-specific Python package distributions and debugger guidance. Use [the offline environment checker](../../references/esri-python-environment-compatibility.md), without automatically installing or upgrading packages.
- [ArcGIS Pro SDK snippets](https://github.com/Esri/arcgis-pro-sdk-snippets) are C# samples for Editing, Map Exploration, Layouts, Geodatabase and QueuedTask; they are not Python toolboxes or a standalone runtime.
- [Developer Support](https://github.com/Esri/developer-support) provides platform-specific proof-of-concept solutions; verify example age and dependencies.
- [Large network analysis tools](https://github.com/Esri/large-network-analysis-tools) demonstrate chunking/parallel OD analysis; [CAD/BIM scripts](https://github.com/Esri/cad-bim-scripts) include Civil 3D workflows; [elevation GP](https://github.com/Esri/elevation-gp-python) demonstrates viewshed/profile publishing. Record host/extension/credit requirements before adoption.

## Engineering procedure

1. **Establish versions precisely.** ArcMap 10.x and Pro 2.x/3.x have different Python runtimes and map APIs. Confirm Python executable and ArcPy version in each installation; do not infer ArcMap support from successful Python 3 source checks.
2. **Write the contract.** Every `.pyt` class should specify input layer/raster types, required/optional fields, defaults, allowed values, validation messages, output formats, CRS and units, expected side effects, input preservation and dataset limits.
3. **Separate calculation core and GIS adapter.** Put numerical/geometry processing in testable modules, isolate ArcPy (data reading/writing, environment and map integration), and keep UI/parameter declarations thin. Support file-based offline fixtures.
4. **Protect ArcPy environments.** Snapshot/restore critical `arcpy.env` settings including workspace, mask, extent, snapRaster, outputCoordinateSystem and scratch state, even on exceptions. Manage Spatial Analyst checkout and release according to host/runtime.
5. **Validate geospatial numerics.** Use a metric CRS for metric slopes/distances and buffers, explicit vertical units and datum, stable NoData handling, geometry validity and correct line digitization direction. Retain provenance and immutable original attributes.
6. **Prefer non-destructive operation.** Make read-only flow-network audit the default. `CreateFlowArrow` has an optional auto-flip that may change source geometry and invert fields: require explicit opt-in, transaction/backup and recovery evidence.
7. **Check hosted data terms.** A DEM, satellite imagery or climate source being publicly discoverable does not imply free unlimited use, guaranteed coverage or permission to cache or redistribute. Validate network/offline failures.
8. **Build realistic fixtures.** Flow: straight, curved, reversed, disconnected networks; geology: simple DEM, formation polygons, faults and reversed sections; climate: known station/time calculations, missing data, independent method outputs, Excel/GeoPackage/FileGDB parity.
9. **Separate test tiers.** T0 source; T1 static/headless ArcPy doubles; T2 native parameter load; T3 native execution and output inspection; T4 packaged Desktop/Android. Clearly record the achieved tier for every target version.
10. **Report rigorously.** List changed files, actual commands, versions/fixture hashes, failed/passed assertions, documented source licensing and unresolved runtime/format limitations. Never claim GIS-native correctness from mock tests alone.

## Standalone Desktop migration

When converting a toolbox into a standalone app (as with MIRSAD Desktop):

- Keep scientific algorithms and data models independent from Esri UI and proprietary ArcPy runtime.
- Choose a separately licensable I/O and spatial engine for GPKG, Shapefile, GeoTIFF, CSV and Excel; document any optional FileGDB writing dependencies.
- Build an isolated environment and installer/uninstaller; test clean Windows installation, real data export, translations, help and preservation of user data.
- Support offline fixtures and explicitly label which remote providers require internet, access tokens or quota.
- Separate released/accepted binaries from newer source-only changes. Verify package-to-source identity without claiming all features are stable.

## Acceptance matrix (copy per tested application)

| Evidence | ArcMap 10.x | ArcGIS Pro 2.x | ArcGIS Pro 3.x | Standalone |
| --- | --- | --- | --- | --- |
| Source/README reviewed | document | document | document | document |
| Syntax + unit tests | run in exact Python | run | run | run |
| Toolbox parameter dialog | open in native app | open | open | N/A |
| End-to-end on controlled fixture | execute + inspect | execute + inspect | execute + inspect | execute + inspect |
| Package, install, UI commands, file readback | optional | optional | optional | mandatory before stable claim |

**Do not fill untested cells with checkmarks.** Record `not tested`, `blocked`, `pass`, or `fail` with evidence links. Source project compatibility claims must be independently reproduced.

## Related references

- `../../references/arcmap-legacy.md`
- `../../references/arcgis-pro.md`
- `../../references/ahmad-ibrahim-gis-case-studies.md`
- `../../references/geospatial-foundations.md`

## Additional production-grade batch-export example

Read the [ArcGIS Pro batch PDF exporter](../../examples/batch-export/README.md). Verify actual export API compatibility and resulting PDFs inside the intended ArcGIS Pro version; passing generic Python syntax checks is not native GIS acceptance.
