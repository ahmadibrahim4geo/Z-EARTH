---
name: global-mapper-development
description: Develop and validate Global Mapper GMS automation, Python scripts, licensed SDK integrations and geospatial exports while separating commercial Blue Marble runtimes from independent GitHub forks and open-source 3D geometry tooling.
license: MIT
---

# Global Mapper Development (Blue Marble Geographics)

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use when developing `.gms` scripts, Global Mapper workspace `.gmw` automation, Python scripts inside Global Mapper, or licensed SDK extensions. Do **not** treat Global Mapper as ArcPy, QGIS, free/open-source engine or public REST API.

Read the [Blue Marble technical and licensing reference](../../references/blue-marble-global-mapper.md) first; combine with [geospatial processing](../geospatial-data-processing/SKILL.md), [terrain/hydrology](../terrain-hydrology-lidar/SKILL.md), [CRS](../coordinate-reference-systems/SKILL.md) or [web/3D](../geospatial-web-mapping/SKILL.md) only as needed.

## Environment & licensing gate

1. Ask or detect Global Mapper **Standard/Pro/Advanced**, installed application/SDK version, Windows architecture, license and Python version. Script language (`.gms`) and `import globalmapper` Python are distinct.
2. If using the Python/Pro features, consult the **version-specific official reference**; current support notes include Python 3.11.6 in the app, external 3.10 guidance and older SDK documents for Python 3.9. Never assume one interpreter works across versions.
3. SDK licensing is **commercial**. Official [license policy](https://support.globalmapper.com/hc/en-us/articles/53119789602324-Global-Mapper-SDK) requires a development SDK license and potentially per-DLL redistribution or server licensing. A trial does not imply free shipping in another executable.
4. Model open-source GitHub repos in Blue Marble's organization as *forks of third-party projects*, not as Global Mapper core; do not copy code without per-project licensing review (especially AGPL-3.0 OpenMVS and mixed-license CGAL).
5. Safe default: non-destructive script, explicitly new output, validated paths and input feature counts. Do not overwrite a user project, workspace or original elevation grid.

## Script engineering workflow

1. Define input source, `.gms` vs `.py`, output format, CRS/horizontal and vertical datums, units, record schema, DEM resolution, NoData, model/LiDAR classifications and expected changes.
2. Use official [Global Mapper Python function reference](https://support.globalmapper.com/hc/en-us/articles/53169757119636-Global-Mapper-Python-reference) or [version-specific GMS scripting reference](https://www.bluemarblegeo.com/knowledgebase/global-mapper-26/GlobalMapper_ScriptingReference.pdf) to confirm exact command signature. Avoid invented GM APIs and outdated tutorial parameters.
3. GMS may use documented import/export commands (`IMPORT`, `EXPORT_VECTOR`, `EXPORT_ELEVATION`, `EXPORT_RASTER`) and analysis commands (`GENERATE_CONTOURS`, `GENERATE_WATERSHED`, `GENERATE_RIDGE_LINES`), but not every command is available in every licensed edition.
4. For SDK/embedded Python, pin version and use documented `globalmapper` module entrypoints; isolate error handling, logging, network access, credentials and output paths.
5. Execute in **licensed, real Global Mapper** only when the host is available. Run a small representative sample, export and reopen output. For DEM/contour/flow, validate NoData and distance/elevation units; for point cloud check class labels and vertical accuracy.
6. Report success only after real host test and observable output. If the host is unavailable, deliver code with clearly marked **untested** status and execution instructions.

## GitHub engineering candidates

- 3D Tiles: [Blue Marble fork](https://github.com/Blue-Marble-Geographics/3d-tiles-tools) of the separate Cesium tools project.
- Terrain: [Cesium Terrain Builder fork](https://github.com/Blue-Marble-Geographics/cesium-terrain-builder) — check age and NoData; not a terrain web server.
- SfM to dense model: [openMVG fork](https://github.com/Blue-Marble-Geographics/openMVG-bmg) → [OpenMVS fork](https://github.com/Blue-Marble-Geographics/openMVS-bmg) with license and georeference caveats.
- Mesh/model handling: [CGAL](https://github.com/Blue-Marble-Geographics/cgal), [tinygltf](https://github.com/Blue-Marble-Geographics/tinygltf), [Assimp](https://github.com/Blue-Marble-Geographics/assimp_bmg).
- Community old examples: [GM-Scripting](https://github.com/PeterSanctus/GM-Scripting) (GPL-3.0, third-party, not official).

## Acceptance checks

- [ ] Precise version, license/feature entitlement and Python interpreter proven
- [ ] Original raster/vector layers untouched, output folder distinct
- [ ] Real tool action completes (not only a syntax check), errors and log captured
- [ ] Output re-opened, correct features/fields/CRS/NoData/Z and metadata
- [ ] Proprietary DLLs and third-party code not redistributed without permission
- [ ] Native T3/T4 status explicitly distinguished from documentation-only T0
