---
name: gis-data-flow-diagnostics
description: Trace and diagnose GIS data lineage from import and format parsing through CRS transforms, analysis, GIS engine, map rendering, project persistence, export and independent readback; distinguish real semantic corruption from UI-only symptoms.
license: MIT
compatibility: Offline trace format and validator use Python standard library; real GIS engine and map renderer instrumentation require target runtime access.
---

# GIS Data Flow Diagnostics

**Purpose:** explain *where and why* GIS data failed between files, engines, view states and outputs. Use for missing layers, geometry/CRS errors, lost KMZ styles, incorrect NoData or elevation, project reopening gaps, disappearing map scenes and export parity defects.

**Free-first source and testing policy:** follow the [repository free-first and licensing policy](../../references/free-first-policy.md). Reuse free/offline tools where feasible, disclose any paid GIS host/OS dependencies, and verify each upstream license before copying code. Never silently require a commercial service.

## Workflow

1. Use the approved goal, task ID, inputs, outputs and readback criteria from [Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md). [GIS Router](../gis-development/SKILL.md) coordinates the specialists; [Security Guardian](../gis-security-guardian/SKILL.md) controls sensitive telemetry and file access.
2. Read [quality handoff contract](../../references/gis-ui-quality-coordination.md), [GIS Data Flow playbook](../../references/gis-data-flow-diagnostics.md) and [sources](../../references/gis-ui-quality-upstream-sources.md).
3. Map real component boundaries: file stream → parser/provider → feature/raster layer → CRS/datum/units → processing workers → scene/renderer → project store → export writer → independent reopen. Some operations legitimately skip phases; record which phases are required for the user's path.
4. Correlate spans with one `trace_id`, stable `dataset_id`, `task_id` and `command_id`. Capture phase status, elapsed time, feature/raster dimensions when appropriate, source and output CRS identifiers, metadata preservation, transform choice, warnings and artifact ID—not raw user coordinates or credentials.
5. Before asserting equality, declare whether the operation is **lossless**, reprojecting, generalizing, aggregating, filtering, clipped or intentionally lossy. Validate applicable invariants: CRS correctness, feature count, geometry equality/tolerance, raster NoData, statistics, field schema, KML styles/resources, time, altitude and layer order. A transformed geometry hash may legitimately differ.
6. Use [offline trace checker](../../scripts/gis_data_flow_diagnostics.py) with [synthetic JSON fixture](../../examples/data-flow-diagnostics/trace.example.json); the checker detects missing/out-of-order phases and declared lossless feature-count mismatches. It cannot prove GIS geometric fidelity or host execution.
7. Test a real fixture through the target engine; independently reopen the exported data with GDAL/OGR, QGIS or a suitable format reader. Refer to [Data Processing](../geospatial-data-processing/SKILL.md), [CRS](../coordinate-reference-systems/SKILL.md), [KML/KMZ](../kml-kmz-engineering/SKILL.md), [Terrain](../terrain-hydrology-lidar/SKILL.md) as needed.
8. Hand a failing user-visible command to [Command Audit](../gis-ui-command-audit/SKILL.md), and a full reproduction to [Desktop E2E Testing](../gis-desktop-e2e-testing/SKILL.md). Document the first verified failing boundary and recommended repair; avoid changing algorithms before confirming cause.

## Evidence and risks

Output an anonymized lineage graph/table, input/output metadata diff, actual trace spans, errors and timings, output readback report, impacted command IDs, and tests by T0–T4. Never claim a data flow works from logs alone. Logs can be incomplete; trace ingestion must fail on malformed/untrusted JSON and apply size/retention policies. The host must approve instrumentation or modifications to the app, and all original data must remain unchanged unless the user's reviewed task authorizes otherwise.
