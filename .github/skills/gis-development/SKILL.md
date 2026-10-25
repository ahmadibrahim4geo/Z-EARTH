---
name: gis-development
description: Route GIS and mapping work to specialist ArcGIS, QGIS, spatial data, remote sensing, desktop, web/3D, KML and API skills with secure, testable outputs.
license: MIT
---

# GIS Development — Agent Router

**Free-first:** use [no-cost policy](../../references/free-first-policy.md). Disclose commercial host/API limits and offer verified free alternatives; never silently require payment.

**Offline examples:** [DEM slope](../../examples/terrain-slope/README.md) and [STAC](../../examples/stac-offline/README.md). [License evidence](../../references/upstream-license-audit.md) is not native-run proof.

**Intake:** [Plan and approve free-form requests](../prompt-intent-orchestrator/SKILL.md) before GIS routing.

**Load selectively:** determine target, environment, data and risk; then use [decision tree](../../references/agent-decision-tree.md) and only needed specialists. Rendering does not substitute for spatial analysis.

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
| Security / untrusted GIS inputs / secrets / Agent Skills / MCP / release checks | [GIS Security Guardian](../gis-security-guardian/SKILL.md) (always applicable as a cross-cutting baseline) |
| Integrate GIS HTTP APIs safely | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) |
| Search place names / geocode addresses / autocomplete | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) + [provider selection guide](../../references/geocoding-place-search.md) |
| Gazetteer search / scale-aware labels | [World Gazetteer](../../references/world-gazetteer-reference-layer.md) |
| WKID, projection, transformation, vertical datum | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) |
| Point/line/polygon/raster symbols, color ramps, map layout | [GIS Cartography & Symbology](../gis-cartography-symbology/SKILL.md) |
| Device time/date/timezone, permission-controlled location, Sunlight simulation | [GIS Device Context](../gis-device-context/SKILL.md) |
| KML/KMZ, Earth-like viewers and timeline | [KML](../kml-kmz-engineering/SKILL.md) · [Earth Pro](../../references/google-earth-pro-engineering.md) · [Historical imagery](../../references/historical-imagery-timeline.md) |


## Global rules (always apply)

1. **Do not silently modify sources:** default to new output files, explicit user approval for destructive editing and backups.
2. **Geospatial correctness:** verify CRS/axis order, true transformations, area/distance units, Z/vertical datum, invalid geometries, NoData, raster alignment and output readback.
3. **Compatibility:** never mix ArcMap Python 2 with Pro APIs; pin host/runtime and validate third-party licensing/authentication/quotas.
4. **Security:** apply [GIS Security Guardian](../gis-security-guardian/SKILL.md) at preflight, runtime and release gates, plus [geospatial input rules](../../references/geospatial-security.md) for files, APIs, SQL and credentials. A policy document does not enforce runtime sandbox permissions.
5. **Performance:** profile representative inputs with [performance guidance](../../references/geospatial-performance.md) rather than asserting universal package choices.
6. **Evidence:** complete [the acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md) and check it with [the evidence validator](../../scripts/validate_evidence.py). Record T0–T4 honestly; generic syntax checks or UI mockups are **not** native GIS execution.
7. **References:** [projects](../../references/project-catalog.json), [APIs](../../references/free-geospatial-apis.json), [data](../../references/geospatial-data-repositories.json) are discovery leads only. Verify licenses, access and provenance before use.

**More:** [full guide](../../references/gis-development-full-guidance.md) · [antipatterns](../../references/gis-antipatterns.md) · [examples](../../examples/README.md).
