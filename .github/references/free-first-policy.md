# Free-first GIS development policy

**Reviewed 2026-10-25. Default for general GIS work: no mandatory paid license, subscription, cloud credits, billed AI, or expiring trial.** The MIT-licensed skills/documentation may be used freely; external GIS host products, libraries, datasets, hosted APIs and model weights have separate legal and operational terms.

## Execution selection rules

1. Start with locally installable open-source alternatives when feasible: QGIS/PyQGIS, GDAL, PROJ, GEOS/Shapely, Rasterio, GeoPandas, PostgreSQL/PostGIS, SQLite/SpatiaLite, GRASS GIS, PDAL, MapLibre, Leaflet and CesiumJS. Confirm each license/version.
2. When the user explicitly needs ArcGIS/ArcPy or Global Mapper/.gms/SDK, do not silently replace the vendor environment: disclose the required commercial host and suggest an independent **analogous** free workflow without claiming proprietary API compatibility.
3. Free academic/noncommercial, registered, keyed, fair-use, quota-limited and evaluation offers have restrictions. Never treat free download or a public map tile/metadata search as unlimited legally reusable source data.
4. Prefer offline/no-key execution; if a registered free tier is necessary, disclose request/data quotas, attribution, caching rights, eligible users, privacy, and any later cost. Do not hide personal API keys in client bundles or require billing silently.
5. A model-running agent must have deterministic local actions without purchasing a hosted LLM. Local/GPU computation and data transfer can still incur practical expenses. Earth Engine and hosted cloud datasets may require eligible accounts or quotas.
6. If a free solution is not equivalent in accuracy/outputs to a paid GIS feature, state the limitation; do not invent feature parity.

## All 15 specialists

| Specialist | Free independent path | Optional dependency or limitation |
| --- | --- | --- |
| `gis-development` | Free decision router, offline fixtures and Python examples | Optional commercial hosts only by explicit request |
| `arcgis-toolbox-engineering` | Review toolbox source; reimplement analogous operations with QGIS/PyQGIS/GDAL | ArcPy execution requires suitably licensed ArcGIS Pro/ArcMap |
| `global-mapper-development` | Use QGIS/GDAL/Whitebox for similar raster/vector tasks | Global Mapper and SDK/DLL require commercial licenses |
| `qgis-plugin-development` | QGIS/PyQGIS and Processing with open-source licensing | Optional third-party plugins and providers may charge |
| `coordinate-reference-systems` | PROJ/pyproj and QGIS CRS metadata | Esri factory metadata does not distribute its projection engine |
| `geospatial-data-processing` | GDAL, Rasterio, Shapely, GeoPandas | External data/formats have independent terms |
| `terrain-hydrology-lidar` | GRASS GIS, PDAL, GDAL, license-permitted Whitebox | ArcGIS/Global Mapper extensions are optional |
| `remote-sensing-pipelines` | STAC/public imagery assets after license checks, GDAL/Rasterio/xarray | GEE/cloud processing or AI may have account/quota requirements |
| `spatial-databases` | Local PostgreSQL/PostGIS or SQLite/SpatiaLite | Managed hosted databases may charge |
| `geospatial-web-mapping` | MapLibre/Leaflet/CesiumJS with permitted local/self-hosted layers | Hosted imagery/terrain/maps may charge or forbid caching |
| `gis-desktop-application-development` | .NET, Tauri and free GIS engines under their licenses | ArcGIS/Global Mapper runtime DLLs need paid licensing |
| `geospatial-data-discovery` | Metadata catalogs and independently permitted free datasets | Downloading or redistributing assets may be restricted |
| `geospatial-data-catalogs` | Offline Python source directory search | A directory is not a free scientific data download |
| `geospatial-api-integration` | Locally served endpoints and verified conditional free-tier sources | Trial/billed providers not default |
| `geospatial-ai-agents` | Deterministic PyQGIS/CLI agent, optional permitted local models | Commercial LLM tokens/cloud/GPU cost not mandatory |

## API classification (catalog review, not live verification)

The 52 versioned API entries currently classify as **42 conditional free**, **1 self-hosted open source**, **3 restricted public**, **1 trial-only**, **4 commercial/metered**, and **1 temporarily unavailable**. Filter out commercial, trial, restricted and unavailable providers from free-only usage by default. Individual terms and upstream outages can change.

**Official grounding:** [QGIS free download](https://www.qgis.org/download/), [Esri ArcGIS Pro trial](https://www.esri.com/en-us/arcgis/products/arcgis-pro/trial), [Global Mapper SDK licensing](https://support.globalmapper.com/hc/en-us/articles/53166637315860-Licensing).

## Verification

- Distinguish a freely readable vendor-specific skill from free licensed execution.
- All specialists link this document and provide a distinct no-cost path in the matrix.
- Data/API catalog metadata must expose access tier without claiming live provider or commercial entitlement.
- CI validates static counts, labels, links and fallback logic. Real native GIS/browser/hosted API acceptance is a separate test tier.
