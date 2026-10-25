---
name: geospatial-data-discovery
description: Find appropriate, legally usable GIS datasets and working geospatial APIs using verified public metadata catalogs, STAC, NASA, Earth Engine, elevation, weather and basemap providers. Choose by location, time, resolution and license; distinguish a metadata index from an actual API.
license: MIT
---

# Geospatial Data Discovery

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use for DEM, climate, satellite images, environmental rasters, OSM/POIs and geospatial downloads.

**Geocoding / Gazetteers:** open [the cost-ranked provider guide](../../references/geocoding-place-search.md) for forward/reverse search, POI, Arabic place names, suggestions and allowed fallbacks. Never equate OpenCage's trial quota with a production plan.

## Procedure

1. Ask for or infer domain, area, period, resolution, coordinate system, output type and intended commercial/noncommercial use.
2. Read `../../references/free-geospatial-apis.md` and its JSON catalog (52 service entries across bathymetry and boundaries, demographic, climate, water, routing, hazards, satellite and more), and use the [24-entry data repositories directory](../../references/geospatial-data-repositories.json) via the [catalog specialist](../geospatial-data-catalogs/SKILL.md) for broader portals. For supplementary discovery, read `../../references/gishub-software.md` and `../../references/project-catalog.json`.
3. Browse catalogs like `opengeos/geospatial-data-catalogs`, `opengeos/stac-index-catalogs`, `opengeos/NASA-CMR-STAC` as **indexes**, never assume an indexed API is available.
4. Verify the *underlying* provider's official page for current access, key requirements, geographic/time coverage, quotas, TOS, attribution and CORS.
5. Try a small legally accessible sample if tools/environment permit. Confirm CRS, pixel size, date, DEM vertical datum, NoData and geolocation.
6. Provide a reproducible download workflow, metadata/provenance fields, failure/retry handling, and license caveats.
7. Don't fabricate endpoints, keys, imagery dates or missing measurements. Never embed users' API tokens in source.
8. Disclose untested endpoints and mark current live access as unverified if it was not checked.

## GEBCO_2026 global bathymetry and terrain

Use GEBCO_2026 for global ocean depths plus land elevation. It provides 15 arc-second grid **spacing**, not assured 463 m measurement accuracy. Use GEBCO WMS only for visual shaded-relief/flat-map layers, not numerical terrain analysis. Use numeric CEDA OPeNDAP/NetCDF or official user-subset raster downloads for elevation calculations; verify CEDA subset URL/access. Keep the **TID grid** to distinguish measured and predicted/interpolated seafloor cells, record approximate mean sea level and source datum uncertainties, and distinguish under-ice from ice-surface polar variants. **Never use for navigation/safety at sea.** See `../../references/free-geospatial-apis.md` for citation and method details.

## Expanded provider routing

- For **Egyptian administrative boundaries**, use geoBoundaries metadata with explicit ADM level, year and layer licence; do not claim historic editions are current legal limits.
- For **population**, choose WorldPop modeled cells/polygon estimates separately from World Bank country indicators. Respect WorldPop 50,000 km² maximum no-key polygons.
- For **hazards**, distinguish FIRMS thermal hotspot points, USGS earthquake catalog and EONET curated events.
- For **air quality**, distinguish OpenAQ measured sensors from Open-Meteo CAMS modeled fields; compare only matching units, averaging intervals and sources.
- For **river discharge**, GloFAS modeled m³/s is not flooded-area mapping. USGS Water and NOAA CO-OPS are US-focused services, not defaults for Egypt.
- For **routes/history**, HeiGIT openrouteservice and ohsome v2 require keys, current URL versions and OSM ODbL attribution.
- For **satellite scenes**, CMR/STAC search metadata is separate from original scientific asset download permission.
- For **coastal/climate data**, NOAA CDO/CO-OPS/ERDDAP require dataset/station selection and separate temporal/spatial datum checks.
- For **biodiversity**, GBIF and iNaturalist are different catalogs; do not reveal privacy-obscured locations or bulk query shared public APIs.

For creating actual adapters and GIS output pipelines, route to `../geospatial-api-integration/SKILL.md`. Never assume the reference catalog confirms endpoint uptime.

## Prefer

- Climate/time series: verified Open-Meteo and NASA POWER provider documentation.
- Elevation/terrain: authoritative DEM source with verified grid, datum and coverage; access quota may need a key.
- Satellite: STAC-compatible sources or provider catalogs with independently validated scenes/assets.
- Basemaps: verify per-provider commercial terms, caching permissions and attribution.
- ArcGIS Pro/ArcMap/QGIS downstream: convert to supported formats and honor each application's runtime.

## Acceptance criteria

- [ ] True provider/source URL, not only a GitHub metadata catalog
- [ ] Authentication, rate limits, license and attribution documented
- [ ] Tested—or clearly not tested—availability/extent/epoch/resolution
- [ ] GIS-safe CRS, NoData, units and output formats
- [ ] Reproducible processing and failure handling
