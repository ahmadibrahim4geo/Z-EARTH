---
name: gis-development
description: Route GIS engineering, debugging, testing and migration to specialist skills for ArcMap, ArcGIS Pro, QGIS, PostGIS, raster/vector, remote sensing, web/3D, desktop and geospatial APIs; enforce spatial validity, security and truthful evidence.
license: MIT
---

# GIS Development — Lightweight Agent Router

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

**Free proof-of-execution examples:** [DEM slope](../../examples/terrain-slope/README.md) and [STAC offline selection](../../examples/stac-offline/README.md) demonstrate tested, no-cost GIS artifacts. [License evidence audit](../../references/upstream-license-audit.md) separates checked code rights from unknowns; no runtime compatibility follows from a license.

**Do not load every reference by default.****Do not load every reference by default.** Determine task, product, runtime, format, data size, risk and intended result first. Read [the decision tree](../../references/agent-decision-tree.md), then only the appropriate specialist and its immediate references. For blended tasks, compose just the necessary specialists; do not mistake a 3D renderer for a spatial-processing engine.

## Choose specialists

| Task | Read first |
| --- | --- |
| ArcMap 10.x, Python 2.7, older `arcpy.mapping` | [ArcGIS Toolbox Engineering](../arcgis-toolbox-engineering/SKILL.md) and [legacy reference](../../references/arcmap-legacy.md) |
| ArcGIS Pro, ArcPy, `.pyt`, geoprocessing | [ArcGIS Toolbox Engineering](../arcgis-toolbox-engineering/SKILL.md) |
| Global Mapper / Blue Marble GMS, Python and licensed SDK | [Global Mapper Development](../global-mapper-development/SKILL.md) |
| QGIS plugin / Processing algorithm | [QGIS Plugin Development](../qgis-plugin-development/SKILL.md) |
| PostGIS, SpatiaLite, schema/indexes | [Spatial Databases](../spatial-databases/SKILL.md) |
| Raster/vector, GDAL, GeoPandas, spatial batch | [Geospatial Data Processing](../geospatial-data-processing/SKILL.md) |
| Landsat, Sentinel, Earth Engine, STAC, COG | [Remote Sensing Pipelines](../remote-sensing-pipelines/SKILL.md) |
| MapLibre, web 2D, CesiumJS globe/3D | [Geospatial Web Mapping](../geospatial-web-mapping/SKILL.md) |
| Independent GIS desktop application / installer | [GIS Desktop Development](../gis-desktop-application-development/SKILL.md) |
| Discover/download external data | [Geospatial Data Discovery](../geospatial-data-discovery/SKILL.md) |
| Find source directories, data repositories and STAC indexes | [Geospatial Data Catalogs](../geospatial-data-catalogs/SKILL.md) |
| Automate PyQGIS / STAC / GeoAI / Earth Engine with an agent | [Geospatial AI Agents](../geospatial-ai-agents/SKILL.md) |
| DEM, terrain, hydrology, LiDAR / point clouds | [Terrain / Hydrology / LiDAR](../terrain-hydrology-lidar/SKILL.md) |
| Integrate GIS HTTP APIs safely | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) |
| Search place names / geocode addresses / autocomplete | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) + [provider selection guide](../../references/geocoding-place-search.md) |
| WKID, projection, transformation, vertical datum | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) |

## Global rules (always apply)

1. **Do not silently modify sources:** default to new output files, explicit user approval for destructive editing and backups.
2. **Geospatial correctness:** verify CRS/axis order, true transformations, area/distance units, Z/vertical datum, invalid geometries, NoData, raster alignment and output readback.
3. **Compatibility:** never mix ArcMap Python 2 with Pro APIs; pin host/runtime and validate third-party licensing/authentication/quotas.
4. **Security:** apply [geospatial security rules](../../references/geospatial-security.md) to untrusted files, remote services, SQL and credentials.
5. **Performance:** profile representative inputs with [performance guidance](../../references/geospatial-performance.md) rather than asserting universal package choices.
6. **Evidence:** complete [the acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md) and check it with [the evidence validator](../../scripts/validate_evidence.py). Record T0–T4 honestly; generic syntax checks or UI mockups are **not** native GIS execution.
7. **References:** the curated [project catalog](../../references/project-catalog.json) and [API catalog](../../references/free-geospatial-apis.json) and [data repository catalog](../../references/geospatial-data-repositories.json) are discovery sources, not proof of runtime/entitlement. Read source licenses and verification metadata before reuse.

**Historical instructions are preserved, unabridged**, in [the full original core guidance](../../references/gis-development-full-guidance.md). Read a section there only when the routed specialist lacks necessary details. Review [known failure patterns](../../references/gis-antipatterns.md) and [production examples](../../examples/README.md) as needed.
