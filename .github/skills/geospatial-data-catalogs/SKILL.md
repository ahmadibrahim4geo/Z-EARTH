---
name: geospatial-data-catalogs
description: Navigate geographic data repositories and metadata catalogs; identify original datasets, distinguish a directory from an API, verify data licenses, provenance, STAC assets, regional coverage and downstream GIS suitability.
license: MIT
---

# Geospatial Data Catalogs & Source Discovery

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

**Starter example:** [STAC offline metadata inspector](../../examples/stac-offline/README.md) provides safe deterministic filtering without a remote account; never promote fixture item links to actual downloadable data.

Use this specialistUse this specialist when a user needs dataset repositories, downloadable GIS layers, catalogs/portals, archives, STAC discovery or comprehensive domain coverage. It complements, but **does not replace**, [data discovery](../geospatial-data-discovery/SKILL.md) and [API integration](../geospatial-api-integration/SKILL.md).

1. Define theme, geographic coverage, era, resolution, raster/vector/point-cloud format, usage rights and intended host.
2. Search the curated [data repository index](../../references/geospatial-data-repositories.json) and [selection guide](../../references/geospatial-data-repositories.md), prioritizing the user's [GIS Data Repositories Professional 2026](https://github.com/ahmadibrahim4geo/gis-data-repositories-professional-2026) for broad exploration. Do not mirror that master index.
3. For OpenGeoS use geospatial-data-catalogs, AWS metadata indices, GEE, CMR, STAC Index and Planetary Computer as *discovery references*, **never presume hosted data, free API or zero quota**.
4. Select the **original** publisher/host; verify collection existence, current licensing, actual asset download, authorization, temporal/geographic coverage and attribution there.
5. Use only authorized requests; validate CRS, axis order, vertical datum, units, raster nodata, scale/offset and geometry topology before analysis.
6. For Earth Engine, STAC, COG and satellite processing, route to [remote-sensing pipelines](../remote-sensing-pipelines/SKILL.md). For credentials/HTTP clients read [API integration](../geospatial-api-integration/SKILL.md).
7. Use the [offline directory query example](../../examples/data-catalogs/README.md) for deterministic catalog searches. It does not test live provider availability.
8. Output source URL, provider, collection, access conditions, license verification status, AOI/period, downloadable formats, provenance and test evidence; mark unknown honestly.

Never claim a dataset is legally re-distributable because the indexing repository uses MIT. Do not scrape entire websites or copy a third-party directory into this repository without license review.
