# GIS Production Workflow Examples

These **additive** examples supplement the existing `templates/` and installed skills. All datasets under `fixtures/` are invented solely for deterministic tests, not real measurements.

| Workflow | Entry | Dependencies | Status |
| --- | --- | --- | --- |
| Project source code licensing evidence | [License audit](../references/upstream-license-audit.md) · [scanner](../scripts/audit_license_catalog.py) | Python 3.11+ stdlib | 8 original license files inspected; 130 remain unverified |
| DEM slope (Horn 3×3; ASCII raster) | [Free raster example](terrain-slope/README.md) | Python 3.11+ stdlib | T2 synthetic 45-degree planar test; **input CRS not auto-verified** |
| STAC item metadata cloud/date/bbox filtering | [Free metadata example](stac-offline/README.md) | Python 3.11+ stdlib | T2 synthetic STAC-like metadata; **not a live search or asset download** |
| Network line duplicates / endpoint dangles | [Network README](network-topology/README.md) | Python 3.11+ standard library | Offline synthetic test (T1); not full topology |
| Hourly weather response → GeoJSON | [API README](api-ingestion/README.md) | Python 3.11+ standard library | Offline fixture test (T1); live API untested |
| Multiple layout PDFs | [ArcGIS Pro README](batch-export/README.md) | ArcGIS Pro/ArcPy | Source only; native execution not verified |
| Spatial index and SQL | [PostGIS SQL](spatial-sql/postgis_network_index.sql) | PostgreSQL/PostGIS | Sandbox illustration, not executed |
| Local GIS data source search | [Source search example](data-catalogs/README.md) | Python 3.11+ stdlib | Offline metadata only; no endpoint availability verified |
| Satellite STAC/COG processing | [Workflow checklist](remote-sensing/STAC_COG_WORKFLOW.md) | Depends on source | Documentation only |

See [quality benchmark](../docs/QUALITY_BENCHMARK.md). All commands should write to **new output locations** and never silently modify input datasets.

## Related quality resources

- [Synthetic GIS fixture pack and expected outcomes](../fixtures/README.md)
- [Native ArcGIS Pro and QGIS acceptance harnesses](../native_tests/README.md)
- [Security standard](../references/geospatial-security.md), [performance guidance](../references/geospatial-performance.md), [before/after anti-patterns](../references/gis-antipatterns.md)
- [Mandatory acceptance report](../templates/GIS_ACCEPTANCE_REPORT.md)
