---
name: remote-sensing-pipelines
description: Plan, implement and verify reproducible STAC, Cloud Optimized GeoTIFF, Google Earth Engine and satellite/climate raster pipelines with acquisition metadata, quality masks, correct scaling and aligned output grids.
license: MIT
---

# Remote Sensing Pipelines

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use for satellite scenes, Earth Engine, STAC/COG collections, cloud masking and climate rasters. Consult [data discovery](../geospatial-data-discovery/SKILL.md), [CRS engineering](../coordinate-reference-systems/SKILL.md), and [GIS development](../gis-development/SKILL.md).

## Offline STAC discovery fixture

The [free STAC ItemCollection metadata test](../../examples/stac-offline/README.md) filters synthetic items by UTC acquisition date, approximate bbox and cloud fraction, without contacting or downloading assets. A bbox intersection is not a polygon intersection and sample fixture URLs are deliberately invalid. Real satellite imagery requires an authenticated/authorized source STAC API, pagination and asset-level verification.

## Required workflow
1. Confirm product/sensor/collection/processing level, acquisition dates, AOI, coverage, spatial resolution, data license, access and expected data semantics.
2. Verify reflectance scaling, offsets, band order, radiometric processing, sun angle, saturation, nodata and **per-pixel** cloud/shadow QA; scene `CLOUD_COVER` alone is insufficient.
3. Pin target CRS and pixel grid: transform, origin, resolution, extent, resampling algorithm and nodata handling.
4. Preserve STAC item IDs, source assets, timestamps, version, QA choices, license and the complete compositing/reprojection recipe.
5. Test COG range requests and real partial reads; distinguish preview tiles from numerical analysis products. Manage Earth Engine auth, server/client semantics, quotas and export.
6. Validate against known reference pixels and imagery footprints, including missing scenes, time-zone boundaries, antimeridian and uneven observation counts.
7. Report independent computational evidence and failures; do not assert native Earth Engine/GDAL correctness from documentation-only checks.

Read [STAC/COG workflow](../../examples/remote-sensing/STAC_COG_WORKFLOW.md), [production workflows](../../references/production-workflows.md) and [quality benchmark](../../docs/QUALITY_BENCHMARK.md).

## NASA GIBS and Copernicus globe reference

[Earth View architecture](../../references/earth-view-architecture.md) separates public NASA GIBS image **visualizations** from authenticated Sentinel Process/Catalog products, dated layer previews and time-lapse mosaics. Preserve real acquisition dates and scientific band/cloud QA instead of treating screen pixels as calibrated source data.

## Agent-assisted source search, not invented Earth observations

Use [agentic-stac](https://github.com/opengeos/agentic-stac), [gee-agents](https://github.com/opengeos/gee-agents) and [geoai-skills](https://github.com/opengeos/geoai-skills) as independent implementation candidates only after host/auth/license checks. Query the [data catalog specialist](../geospatial-data-catalogs/SKILL.md) to find verified sources; ask [Geospatial AI Agents](../geospatial-ai-agents/SKILL.md) for schema validation, cost confirmations and reproducible provenance. Do not trust an LLM generated STAC asset URL without independently reading real item metadata.
