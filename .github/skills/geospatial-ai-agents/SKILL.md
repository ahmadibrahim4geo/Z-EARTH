---
name: geospatial-ai-agents
description: Engineer safe auditable GIS agents that orchestrate verified PyQGIS, GeoAI, STAC and Earth Engine tools with schemas, confirmation, provenance, reproducible operations and end-to-end GIS tests.
license: MIT
---

# Geospatial AI Agents & GIS Automation

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

An LLM **chooses tools**; a deterministic GIS engine does the geometry, raster and CRS calculations. Prefer typed actions, never arbitrary code execution or raw shell commands for a user request.

## Route by host
- QGIS GUI and Processing: [QGIS specialist](../qgis-plugin-development/SKILL.md), [OpenGeoS CLI Anything QGIS](https://github.com/opengeos/cli-anything-qgis) (QGIS bindings required), [Whitebox agent](https://github.com/opengeos/qgis-whitebox-agent) (Whitebox provider required).
- Data inspection, segmentation and raster manipulation: [OpenGeoS geoai-skills](https://github.com/opengeos/geoai-skills) as *patterns*, not necessarily portable to other agent platforms; Python, PyTorch/CUDA/model-license checks.
- Natural-language STAC: [agentic-stac](https://github.com/opengeos/agentic-stac); [data catalogs](../geospatial-data-catalogs/SKILL.md); verify upstream provider and signed assets independently.
- GEE: [gee-agents](https://github.com/opengeos/gee-agents), authentication, project configuration, Earth Engine quotas and true output exports.
- Browser map: [maplibre-gl-geoagent](https://github.com/opengeos/maplibre-gl-geoagent) with explicit tool allow-list and no exposed API secrets.
- General agent architecture: [GeoAgent](https://github.com/opengeos/GeoAgent), structured tool registry, maps/context and confirmation hooks.

## Mandatory safe action contract

1. Declare `tool_id`, input JSON schema, validation limits, CRS and expected GIS output. Resolve source paths inside the authorized workspace only.
2. Inspect metadata with read-only tools first. Obtain confirmation before destructive edits, unbounded expense, large downloads, uploads, third-party sharing or code execution.
3. Run preflight (installed runtime, plugin, model entitlement, provider license, predicted data size). Disable unavailable controls, don't invent success.
4. Execute real GIS operation through PyQGIS/ArcPy/Whitebox/GDAL etc, not free-form model arithmetic; log versions, parameters, sources, hashes and output files.
5. Validate the output independently: re-open layers, feature counts, attribute schema, CRS, NoData, georeferencing, geometry validity and intended map view.
6. Report full evidence: T0 description-only, T1 syntax, T2 offline fixture, T3 real GIS runtime, T4 representative user scenario. A demo or notebook alone does not prove T3/T4.

## Starter recipes

- Inspect input -> choose appropriate QGIS processing algorithm -> validate parameter schema -> execute in disposable output GPKG -> compare before/after feature extent/CRS -> save project.
- Query STAC catalog by AOI, date and cloud constraints -> inspect item metadata -> fetch a legal sample COG -> validate raster bands/pixel QA -> log complete source.
- Automated GEE workflow -> authenticate -> bound region/time -> apply pixel-level QA -> export output -> verify actual exported file exists.

Do not import Claude-specific skill commands as if they were generic ChatGPT runtime actions. Adapt architecture, include explicit dependency and security review.
