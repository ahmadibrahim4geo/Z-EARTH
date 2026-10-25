---
name: geospatial-api-integration
description: Engineer production-safe adapters for free and public GIS REST, STAC, OGC, GeoJSON, raster, population, elevation, climate, hazard and routing APIs in ArcGIS, QGIS, Python and desktop/web software. Enforce credentials, quotas, spatial correctness, licences, error handling, validation and reproducible provenance.
license: MIT
---

# Geospatial API Integration

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Read [Earth View's external imagery API review](../../references/earth-view-architecture.md) when implementing a globe: reuse the existing NASA GIBS and Copernicus API records, validate WMS axis order and keep OAuth/API tokens server-side.

Use this distinct integration skill when the goal is to **implement working GIS data consumers**, not merely discover a source. First consult [data discovery](../geospatial-data-discovery/SKILL.md), [provider catalog](../../references/free-geospatial-apis.md) and [52-entry JSON](../../references/free-geospatial-apis.json).

For **place names, geocoding, autocomplete, and reverse lookup**, first read the ordered [Geocoding & Place Search provider guide](../../references/geocoding-place-search.md). It separates open-source engines, restricted public APIs, genuinely free quotas, test-only trials and metered commercial services. The [provider JSON catalog](../../references/free-geospatial-apis.json) uses `category=geocoding` and `pricing_group`. Do not assume an autocomplete request is equivalent to a user search or that a trial is a production allowance.

## Mandatory adapter implementation workflow

1. Choose an exact provider ID, dataset/product, geography, dates and method from the catalog. Recheck official API docs at implementation time; distinguish documentation verification from a real request test.
2. Record and enforce authentication (none, user credential, free account), usage purpose (noncommercial/commercial), request quotas, attribution, offline/caching permission and redistribution rights. Do not use shared demo credentials. Expose user-owned API-key configuration, never check personal keys into Git or logs.
3. Implement a versioned, isolated adapter per provider. Reuse shared HTTP transport only; do **not** normalize away differences between sensor observations, modeled forecasts, satellite granules, administrative boundary editions and crowdsourced observations.
4. Normalize inputs: WGS84 latitude/longitude and explicit axis order; bbox order and polygon ring validity; date/timezones; area limits, datum and units. Never treat geographic degrees as metric distances.
5. Request bounded chunks with pagination or asynchronous task polling where documented. Use timeouts, bounded concurrency, cancellation and retry only for safe temporary 429/5xx responses; respect Retry-After. Handle 401/403 and quotas without silent retry loops.
6. Validate the response against explicit schema, HTTP status and semantic checks. Empty coverage, null, NoData, partial payload and source outage must yield diagnosable states, **not synthetic zeroes**.
7. Convert real responses to GeoJSON, GeoPackage, feature classes, NetCDF, COG/GeoTIFF or in-memory features, according to the target GIS runtime; never claim an API provides these formats unless verified.
8. Preserve provenance with every output: provider_id, dataset_id, model/product/version, UTC retrieval time, native observation time and timezone, source URL without secrets, CRS, datum, cell size, units, quality flags, license, attribution and any resampling/aggregation methods.
9. Test a tiny authorized request against official examples when possible; add offline fixture/schema tests, error-path tests and repeatability checks. Report separately: documented source, tested endpoint, integration in target host, and full UI acceptance.

## Provider and data-specific guardrails

| Category | Preferred example | Main rule |
|---|---|---|
| Named locations / geocoding | Pelias self-host, GeoNames, Geoapify, Open-Meteo | Respect provider usage, autocomplete bans, storage/attribution rules, and language variants; do not silently chain prohibited public servers |
| Global bathymetry + land terrain | GEBCO_2026 WMS / CEDA OPeNDAP | WMS displays imagery only; OPeNDAP numeric grid for analysis; preserve TID source type, MSL caveats and no-marine-navigation restriction |
| Boundaries | geoBoundaries gbOpen | Respect each source boundary edition/underlying licence |
| Population | WorldPop v2, World Bank v2 | Modeled gridded estimate != census/national indicator |
| Air / climate | OpenAQ v3, Open-Meteo, NOAA CDO | Sensor observation != modeled field; normalize units and daily windows |
| Rivers / coasts | Open-Meteo GloFAS, USGS Water, NOAA CO-OPS | m3/s discharge != inundation geometry; US-only providers not Egypt defaults |
| Hazards | FIRMS, USGS Earthquake, EONET | Thermal detection != burned footprint; curated events != complete catalogs |
| Imagery | NASA CMR, Earth Search, Planetary Computer | STAC or CMR metadata != license for original assets |
| OSM mobility/history | HeiGIT openrouteservice/ohsome v2 | Free personal key; OSM edit date != construction date |
| Ecology / humanitarian | GBIF, iNaturalist, HDX HAPI | Sensitive obscured coordinates stay obscured; enforce publisher-specific licenses |

**Important migration notices (2026-10-24):** ohsome stable v2 path is https://api.heigit.org/ohsome-api/v2/ (not v1 or v2-rc); openrouteservice uses https://api.heigit.org/; USGS old WaterServices ends early 2027. ISRIC SoilGrids REST remains flagged paused in the source catalog. Public OSMF Nominatim must not be used as an auto-generated generic client.

## Host compatibility and secrets

- ArcMap 10.x: ArcPy/Python 2.7 network/TLS/library compatibility requires separate verified implementation; modern Python 3-only HTTP dependencies do not work unchanged.
- ArcGIS Pro: validate arcpy field types, feature-class names, geodatabase locks, environment and spatial reference before writes.
- QGIS: use QgsTask/network-safe asynchronous behavior; do not freeze UI or mix CRS axis semantics.
- Desktop WPF/WebView2/PySide/Tauri: never embed developer credentials into distributed code or public JS bundles; user-owned key storage and explicit provider attribution are required.
- Headless/server: use per-user accounts, bounded retries and source-aware caching only where policy allows; do not proxy personal academic keys for unlicensed third parties.

## Acceptance checklist

- [ ] Live documentation, license and rate policy reviewed for exact version
- [ ] Provider choice and data product semantics recorded
- [ ] No secrets in repository, output metadata, client-side JS or request logs
- [ ] Empty/partial/unauthorized/429/timeout responses handled and test-covered
- [ ] CRS, vertical datum, distance/area units and timestamps validated
- [ ] GeoJSON/feature/raster output read back in real target GIS where possible
- [ ] Attribution, source quality and legal reuse limits survive export
- [ ] Evidence distinguishes static tests from real endpoint and host/UI tests
