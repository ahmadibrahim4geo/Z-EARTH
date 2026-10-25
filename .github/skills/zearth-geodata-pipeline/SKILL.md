---
name: zearth-geodata-pipeline
description: Use this skill when adding GIS formats, CRS conversion, elevation or file pipelines.
---
# GDAL and geodata transformation
- Treat input CRS, coordinate axis order and altitude datum as separate documented fields.
- KML coordinates are lon/lat with WGS84 semantics; project to other CRS through PROJ with explicit source/destination.
- GDAL/OGR is an interchange adapter, not the exclusive keeper of nested KML structure.
- Use cancellable streaming ingestion, caps, spatial index and provenance metadata.
- Reproject raster tiles only through approved pipeline; document pixel scale/nodata and vertical reference.
- Test coordinate round-trip at poles, International Date Line, Egypt and negative elevations.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
