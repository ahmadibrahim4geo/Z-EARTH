# Standalone desktop GIS: architecture, implementation and acceptance

This guide is an independently authored implementation framework. Consult [GeoLibre](https://github.com/opengeos/GeoLibre), [RustGIS](https://github.com/opengeos/RustGIS) and [MIRSAD Desktop](https://github.com/ahmadibrahim4geo/MIRSAD/tree/main/MIRSAD%20Desktop) for different design approaches, **not** as interchangeable engines or automatically redistributable code.

## Architecture boundaries

| Component | Responsibility | Contract |
| --- | --- | --- |
| UI shell (PySide6 / WPF / Tauri) | Ribbon/menu, dock panels, dialogs, language/theme and accessibility | Every visible command has a checked action or an explicit disabled explanation |
| Project model | Save/reload datasets, order, visibility, style, view and source provenance | Serializable and versioned; corruption/old versions handled safely |
| 2D/3D renderers | Show raster/vector/terrain data with camera and selection state | Capability-check each engine; do not confuse imagery tiles with an analysis backend |
| Analysis engine | Climate, hydrology, geometry, statistics or scientific algorithms | Deterministic, headless and independent of window events |
| Data adapters | Read/write GeoPackage, GeoJSON, GeoTIFF/COG, tables and other verified formats | CRS/datum, encoding, NoData, format fidelity and license checks |
| Web providers | Weather, elevations, satellite, basemaps, geocoding | Provider key/rights/quotas, provenance, timeout, authentication and retry policy |
| Workers | Long jobs, progress, cancellation, cache and errors | No blocked UI thread; partial work cleanup and recovery |
| Packaging | Installer, runtime files, license notices, updates | Clean-build test, complete dependencies and user-file preservation |

## A vertical slice that must work before UI expansion

1. Create/open a project from a user-selected path, without hardcoded developer locations.
2. Load a sample GeoPackage vector with known projected CRS and a small raster/DEM with known NoData.
3. Render both; zoom to extent, toggle layer, style feature, inspect attributes and test a second language.
4. Run one meaningful spatial tool; assert numerical output and persist a new layer using a valid CRS.
5. Save project, exit, relaunch installed build, reopen and confirm layer order, symbology, view and underlying source access.
6. Export meaningful CSV/GeoPackage/GeoTIFF where supported and read back output with an independent implementation.
7. Unplug network: verify local datasets work, provider failures are visible and never converted to fabricated defaults.

## ArcPy toolbox vs Desktop application

- **Toolbox:** hosted by ArcMap/ArcGIS Pro, depends on licensed ArcPy and host map/ArcGIS UI. Cross-version Python 2.7 / 3 requires careful isolation.
- **Standalone Desktop:** must use a legal standalone engine and bundle its *own* dependencies; do not depend on a local licensed ArcGIS installation unless the entire application expressly requires it.
- **Migration:** keep a pure scientific core; wrap GIS I/O and UI with adapters. Record differences in raster resampling, transformation pipeline, Z units, NoData, masks, output identifiers and metadata.
- **Acceptance:** numerical match on controlled fixtures with tolerances and sample output parity is required; visual resemblance is not parity.

## Critical performance and reliability tests

- Large raster memory estimates and cache eviction rather than loading all pixels at once.
- Cancel a processing job while it is writing output: no corrupted registered layer.
- Switch 2D/3D renderer repeatedly: saved model remains consistent, unsupported controls are disabled.
- Arabic paths, RTL labels, decimal formatting, missing fonts, Windows DPI 100/150/200% and high contrast mode.
- External source 401/403/429/500 errors, short network outage, invalid tile URL, missing datum grid, expired key.
- Confirm installation on clean x64 Windows and user-file preservation on update/removal.

## Required release report fields

Application version and commit SHA; tested Windows build, Python/.NET/Rust version, dependencies and exact licenses; test fixture/expected values; headless test count and UI click matrix; clean install/export/readback outcomes; provider/offline tests; known gaps; release decision (**development candidate** vs **approved stable**).

See also `references/ahmad-ibrahim-gis-case-studies.md` for example projects, and `skills/arcgis-toolbox-engineering/SKILL.md` for ArcGIS-hosted toolboxes.
