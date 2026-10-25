---
name: terrain-hydrology-lidar
description: Engineer and independently validate digital elevation, geomorphology, watershed, flow accumulation, contour, bathymetry, LiDAR and point-cloud workflows with correct vertical datum, units and grid alignment.
license: MIT
---

# Terrain, Hydrology & LiDAR

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

## Free executable validation fixture

[Horn slope for a small projected ESRI ASCII DEM](../../examples/terrain-slope/README.md) runs with Python stdlib and no proprietary engine or paid credits. Its artificial east-rising DEM has 9 interior cells with 45° slope, NoData at edges, and feet-to-meters unit tests. It **requires user confirmation that cellsize is in projected linear units** and **does not validate CRS automatically**; compare with QGIS/GRASS/GDAL on real datasets before relying on scientific results.

When discussing Global Mapper contour/watershed/LiDAR automation, consult [Global Mapper Development](../global-mapper-development/SKILL.md) and the [Blue Marble forks/SDK guide](../../references/blue-marble-global-mapper.md). Do not confuse source-code forks for Cesium terrain/SfM processing with commercially licensed Global Mapper algorithms. Check vertical datum and NoData handling when producing or converting terrain tiles.

Use alongside [data catalogs](../geospatial-data-catalogs/SKILL.md), [geospatial processing](../geospatial-data-processing/SKILL.md), [CRS](../coordinate-reference-systems/SKILL.md) and optional [QGIS](../qgis-plugin-development/SKILL.md) / [ArcGIS](../arcgis-toolbox-engineering/SKILL.md) host.

1. Confirm AOI, task (DEM/HAND/streams/crest lines, slope/contours, coastal bathymetry, LiDAR classification), cell spacing, true spatial accuracy, horizontal/vertical CRS and geoid height reference. Do not treat geographic degrees as metres.
2. Choose source for geography and time; GEBCO global 15-arcsec grid spacing is **not** guaranteed terrain precision; use TID/source-grid and never for navigation. For LiDAR distinguish LAS/LAZ/COPC, classification, intensity, return number, Z reference and point density.
3. Select actual algorithm/runtime: WhiteboxTools, GDAL/GRASS, ArcPy, PyQGIS, [OpenGeoS lidar](https://github.com/opengeos/lidar), [whitebox-python](https://github.com/opengeos/whitebox-python); [terrio](https://github.com/opengeos/terrio) only after version/runtime tests (early-stage).
4. Enforce raster alignment, nodata propagation, consistent flow accumulation units and depression-fill/breach strategy. Record D8/MFD choice, cell area, stream threshold and derived runoff assumptions.
5. Crest-line extraction requires topographic evidence (slope/aspect/curvature/elevation) and actual validation on test areas; no guarantee that all steep cells are real escarpments.
6. For profiles and 3D use [GeoLibre elevation profile](https://github.com/opengeos/geolibre-elevation-profile), [MapLibre LiDAR](https://github.com/opengeos/maplibre-gl-lidar), [3D Tiles](https://github.com/opengeos/maplibre-gl-3d-tiles) as relevant **viewers**, not scientific ground-truth providers.
7. QA: compare a small DEM window to a trusted processing engine; check known points and unit conversions; test nodata/edge effects and inspect exported raster/feature metadata.
8. Preserve source raster; include provenance, operation parameters, Z units and uncertainty of outputs. If imagery/service/vertical datum is unverified, disclose it.
