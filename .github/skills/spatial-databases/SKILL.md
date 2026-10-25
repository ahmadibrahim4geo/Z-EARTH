---
name: spatial-databases
description: Engineer, validate, audit and optimize PostGIS or SpatiaLite spatial databases with schema constraints, GiST/R-Tree indexing, explain plans, safe migrations and reproducible geospatial QA.
license: MIT
---

# Spatial Databases

**Free-first default:** Follow [no-cost GIS workflow and licensing policy](../../references/free-first-policy.md). Do not silently require paid GIS execution, LLM/API credits or expiring trials. Disclose vendor-license limits and offer a separately tested analogous free route where feasible.

**Use for:** geodatabase schemas, SQL spatial joins, PostGIS/SpatiaLite indexes, spatial performance, and data integrity. Consult [the main skill](../gis-development/SKILL.md) for runtime routing and [CRS engineering](../coordinate-reference-systems/SKILL.md) for coordinate transformations.

## Required workflow
1. Establish database and spatial extension versions, table ownership, geometry type, unique IDs, SRID, data volume and read/write permissions.
2. Profile `NULL`, empty or invalid geometries, mixed types, axis order, units and actual SRIDs; do not use `ST_SetSRID` instead of a needed transformation.
3. Select indexes supported by the *actual* engine: PostgreSQL/PostGIS generally uses geometry GiST; SQLite/SpatiaLite uses its own R-Tree mechanisms. Never assume interchangeable SQL dialects.
4. Measure approved sandbox query plans with `EXPLAIN (ANALYZE, BUFFERS)`; use spatial predicates and bbox filters correctly. Evaluate performance on representative distributions.
5. Apply database changes only with authorization, backups, transactions, migration plan and rollback. Keep all QA/read-only checks non-destructive by default.
6. Report before/after measurements, source/license/provenance, assumptions and failed geometry/CRS tests. Syntax alone is **not** production acceptance.

## Realistic test cases
- Null/empty/invalid or mixed geometry, duplicate asset IDs, cross-SRID joins and geographic-distance confusion.
- False dangles from legitimate terminals, non-noded intersections, and misinterpreted 3D network endpoints.
- Query plan on indexed vs unindexed **sandbox** data with large and skewed geometries.

Start with [sample PostGIS SQL](../../examples/spatial-sql/postgis_network_index.sql), [production examples](../../examples/README.md) and [the quality benchmark](../../docs/QUALITY_BENCHMARK.md). No native database instance is exercised in generic GitHub CI.
