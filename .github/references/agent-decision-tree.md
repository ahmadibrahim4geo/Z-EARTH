# GIS Agent Decision Tree

For general tasks start with the [free-first policy](free-first-policy.md). For a request explicitly tied to ArcGIS or Global Mapper, keep its intended host but disclose commercial licensing and independently suitable free alternatives.

Select **the delivery target**, not just the programming language. The general [router](../skills/gis-development/SKILL.md) stays small; this file holds details.

1. Is the user building inside ArcGIS? **ArcMap 10.x:** Python 2.7 and `arcpy.mapping` where applicable. **ArcGIS Pro:** supported Python 3 / ArcPy / `arcpy.mp`; distinguish `.pyt`, script tool and Pro SDK add-in.
2. Global Mapper from Blue Marble Geographics? Route to [Global Mapper Development](../skills/global-mapper-development/SKILL.md): distinguish `.gms`, embedded Python and commercially licensed SDK/DLL; check edition and feature entitlements. Source forks on GitHub are not the Global Mapper engine.
3. Inside QGIS? **Processing algorithm:** Processing provider interface. **Plugin:** PyQGIS/Qt with real action wiring. **Independent executable:** Desktop application engineering, not a plugin.
4. Is the output interactive web mapping (including time-aware satellite globes)? Read the [Earth View design review](earth-view-architecture.md) only when 3D imagery/temporal regional inspection is relevant.  **2D/vector:** MapLibre or Leaflet according to features and licensing. **3D terrain/globe:** CesiumJS or other verified engine. For raster/geometry analysis also load the independent processing specialist.
5. Is there a database? **PostGIS/SpatiaLite:** schema, SRID, spatial index, transactional migrations, EXPLAIN plans. Don't transform coordinates by changing their SRID labels.
6. Raster/vector algorithms, massive features, reprojections? Route to **geospatial data processing**; use windowed raster reads and chunking where measurements justify them.
7. Satellite imagery/STAC/COG/Earth Engine? Route to **remote sensing pipelines** and data discovery. Verify sensor scales, per-pixel QA and target grid.
8. GIS data repositories, catalogs or provider directories? **Find source index:** data catalogs; **find individual provider:** data discovery. For STAC natural-language search add GeoAI agent engineering only when needed. GIS data or live services? **Find provider:** data discovery. **Use HTTP endpoints:** API integration, quotas, auth and provenance. Hosted WMS map tiles aren't necessarily analysis rasters.
9. GIS AI agent or automation? Route to **Geospatial AI Agents** for safe tools, confirmation and evidence. DEM, river networks, terrain or point-cloud work? Add **Terrain / Hydrology / LiDAR**. GIS desktop standalone? Use Desktop skill, with a renderer and spatial computation layer separated.
10. Coordinate systems, vertical units or datum transformation anywhere? Add CRS skill as **cross-cutting**, not an alternative.
11. All tasks use Security, Performance (when scale matters), and Evidence Contract regardless of routed skill.

## Cross-platform requests

For an app that needs ArcGIS Toolbox interoperability, a PostGIS backend and a Cesium globe, route to **Desktop + ArcGIS + Spatial Databases + Web Mapping + CRS**; integrate one end-to-end vertical slice before adding more capabilities. Document which host tests are available. Load references progressively, never every specialist automatically.

## Response and test contract

Declare target, version, schema/CRS, expected outputs, source modifications and risk, then implementation/tests. Use [GIS Acceptance Report](../templates/GIS_ACCEPTANCE_REPORT.md), [quality tiers](../docs/QUALITY_BENCHMARK.md) and [evidence validation](../scripts/validate_evidence.py) before calling a feature completed.
