---
name: coordinate-reference-systems
description: Discover, audit, compare and implement GIS coordinate reference systems using the official Esri Projection Engine database, SpatialReference.org and PROJ; preserve ArcGIS Pro's Favorites/Layers/Geographic/Projected/Vertical organization, WKID aliases, extents, units, datum transformations, WKT and reproducible CRS tests.
license: MIT
---

# Coordinate Reference Systems & Projection Intelligence

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

Use for CRS catalogs, spatial reference dialogs, projection suggestions, ArcPy/PyQGIS transformations, WKT/.prj interoperability, vertical datums, map design and standalone GIS development.

## Mandatory sources (distinct responsibilities)

- **Esri Projection Engine database:** https://github.com/Esri/projection-engine-db-doc — published factory object metadata (CSV/JSON), WKID/latestWkid, version/deprecation, source WKT/WKT2, geographic extent, transformation method/accuracy. It is NOT the full proprietary Projection Engine implementation or an exported ArcGIS folder tree.
- **ArcGIS Pro official folder structure:** https://doc.esri.com/en/arcgis-pro/latest/help/mapping/properties/coordinate-system-folder-structure.html — normative UI terminology and geographic-folder organization.
- **SpatialReference.org:** https://spatialreference.org/ — authority-based lookup, ESRI/EPSG/other registries, WKT1/WKT2/Esri WKT/PROJJSON. Managed by PROJ, not Esri.
- **PROJ:** https://proj.org/ — actual transformation engine; use pyproj, GDAL/OSR or installed PROJ database to execute conversions. CRS definition alone does not supply missing datum grids.

Read `../../references/esri-crs-and-spatialreference.md` and the integration audit `../../references/esri-open-source-integration.md` before any implementation.

## Preserve Esri terminology and presentation

### Horizontal systems
- **Favorites** — persistent, user-selected CRSs (both geographic and projected). Favorites are preference state, **not** a separate official CRS authority or database object type.
- **Layers** — CRSs currently used by the map/scene's layers, not global definitions.
- **Geographic Coordinate Systems** — organized geographically by continent/region/country.
- **Projected Coordinate Systems** — geographically organized, including **Zone Series** (such as UTM/Gauss–Krüger) and United States State Plane exceptions.
- **Custom** — user-defined CRSs created/imported in the session, persisted only when saved.

### Vertical systems
- **Favorites**, **Layers**, **Vertical Coordinate Systems** (ellipsoidal-based and gravity-related), and **Custom**, where supported by installed ArcGIS.
- Do not intermingle horizontal and vertical favorites or infer a vertical datum from an elevation field.

Respect the separate `Horizontal systems` and `Vertical systems` tabs. Geographic folder paths should preserve original Esri case, names and nesting **only when a verified official folder mapping is available**. A derived grouping by `areaname` or extent must be flagged `derived`, not called an exact Esri hierarchy. Do not assert a downloadable Esri UI tree exists in the PE object JSON.

## Mandatory developer workflow

1. Identify target host/version: ArcMap 10.8.2 vs Pro 3.x, QGIS, PROJ/pyproj, standalone; establish CRS database vintage.
2. Find CRS by **authority + code** (for example `EPSG:4326`, `ESRI:102100`) and also check Esri's original `wkid` and `latestWkid`. Never assume a bare number uniquely identifies every object family.
3. Separate **definition**, **map display reprojection**, **geometry transformation**, and **Define Projection metadata changes**. The last does not change coordinates.
4. Read axis order and units from WKT2; normalize x/y order explicitly when a user interface expects lon/lat. Do not treat EPSG:4326 angular units as meters.
5. Filter candidate CRS by genuine area-of-use bounds, deprecation, dataset location, intended distance/area/shape preservation and grid availability. Handle dateline-wrapping extents.
6. When transforming, enumerate viable transformations for the geographic and vertical datum, required grids, declared accuracy, and bounds; don't choose an unavailable grid or make up an accuracy.
7. Persist Favorites as a user-scoped setting referencing authority+code; populate Layers from the live document; import/export WKT2 and Esri WKT faithfully and label lossy legacy PROJ strings.
8. Keep a PROJ/proj.db-backed offline lookup path when designing desktop tools. Treat SpatialReference.org as a reference/optional online lookup, not a mandatory network dependency.
9. Test round-trip, axis order, unit safety, WKID aliases, deprecated cases, multiple transformations and extent crossing. A catalog lookup alone does not prove correct reprojection.
10. Report tested versions, data provenance, unresolved mappings, and exactly which UI folders are verified vs approximated.

## Usable standard-library catalog inspector

`python scripts/crs_catalog.py --source ./esri-projection-engine-db-doc/csv --tree`

`python scripts/crs_catalog.py --source ./esri-projection-engine-db-doc/json --kind projcs --query Egypt --limit 20`

`python scripts/crs_catalog.py --source ./esri-projection-engine-db-doc/csv --kind projcs --bbox 24 22 37 32 --format json`

`python scripts/crs_catalog.py --source ./esri-projection-engine-db-doc/json --favorite-add EPSG:32636 --favorites ./favorites.json`

The script does **not** ship a full external database or transform coordinate arrays; callers supply downloaded official files. Its folder view preserves Esri's top-level labels; it never claims to reproduce every underlying proprietary UI folder path.

## Routing

- ArcGIS Pro/ArcMap: `../../references/arcgis-pro.md`, `../../references/arcmap-legacy.md`; validate .prj/export compatibility.
- QGIS: `../../references/qgis.md`; QgsCoordinateReferenceSystem, QgsCoordinateTransform and context/grids.
- Desktop: `../../references/desktop-and-web.md`; PROJ/OSR/pyproj or supported native bindings; separate catalog from rendering engine.
- Data services: `../geospatial-data-discovery/SKILL.md`; distinguish reference catalog metadata from downloadable geodata.

## Completion criteria

- [ ] Official Esri labels and Horizontal/Vertical separation maintained
- [ ] Favorites persisted; Layers derived from current user data, never hard-coded
- [ ] `wkid`/`latestWkid`, authority, deprecation, epoch/version provenance handled
- [ ] Full geographic area and transformation constraints validated, not name-only guesses
- [ ] Known correct control-point round-trip and datum-grid availability tests
- [ ] Source/engine licenses and target runtime recorded; untested UI claims marked
