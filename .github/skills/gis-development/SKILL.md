---
name: gis-development
description: Route GIS tasks to ArcGIS, QGIS, raster, remote sensing, KML, desktop, web and API specialists with verifiable tests.
license: MIT
---

# GIS Development — Agent Router

**Free-first:** use [no-cost policy](../../references/free-first-policy.md). Disclose paid limits; prefer verified free alternatives.

**Examples:** [DEM](../../examples/terrain-slope/README.md), [STAC](../../examples/stac-offline/README.md).

**Intake:** [Global contract](../../references/repository-wide-skill-orchestration.md) → [Intent review](../prompt-intent-orchestrator/SKILL.md) → Router.

**Goal-first handoff:** the [Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) establishes the user's desired outcome and testable success criteria before this router selects skills. [Workflow contract](../../references/repository-wide-skill-orchestration.md); no direct prompt-to-execution shortcut.

**Route:** [decision tree](../../references/agent-decision-tree.md); rendering ≠ analysis.

## Choose specialists

| Task | Read first |
| --- | --- |
| ArcMap 10.x | [ArcGIS Toolbox](../arcgis-toolbox-engineering/SKILL.md) · [legacy](../../references/arcmap-legacy.md) |
| ArcGIS Pro / ArcPy | [ArcGIS Toolbox](../arcgis-toolbox-engineering/SKILL.md) |
| Global Mapper | [Specialist](../global-mapper-development/SKILL.md) |
| QGIS plugin / Processing algorithm | [QGIS Plugin Development](../qgis-plugin-development/SKILL.md) |
| QGIS engine | [Integration](../qgis-engine-integration/SKILL.md) |
| PostGIS, SpatiaLite, schema/indexes | [Spatial Databases](../spatial-databases/SKILL.md) |
| Raster/vector, GDAL, GeoPandas, spatial batch | [Geospatial Data Processing](../geospatial-data-processing/SKILL.md) |
| Remote sensing | [Pipelines](../remote-sensing-pipelines/SKILL.md) |
| MapLibre, web 2D, CesiumJS globe/3D | [Geospatial Web Mapping](../geospatial-web-mapping/SKILL.md) |
| Independent GIS desktop application / installer | [GIS Desktop Development](../gis-desktop-application-development/SKILL.md) |
| Android/iOS GIS apps, GNSS, offline sync | [GIS Mobile Application Development](../gis-mobile-application-development/SKILL.md) |
| UI command inventory, dead Ribbon/menu icons, handlers, shortcuts | [UI Command Audit](../gis-ui-command-audit/SKILL.md) |
| Real Windows desktop UI, WebView2 and installer interaction tests | [Desktop E2E Testing](../gis-desktop-e2e-testing/SKILL.md) |
| Import → engine → render → save/export provenance or missing layers | [GIS Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md) |
| GIS UI/UX redesign, accessibility, keyboard and visual regression | [UI/UX Design & Testing](../gis-ui-ux-design-testing/SKILL.md) |
| Discover/download external data | [Geospatial Data Discovery](../geospatial-data-discovery/SKILL.md) |
| Find source directories, data repositories and STAC indexes | [Geospatial Data Catalogs](../geospatial-data-catalogs/SKILL.md) |
| GIS AI agents | [Agents](../geospatial-ai-agents/SKILL.md) |
| DEM, terrain, hydrology, LiDAR / point clouds | [Terrain / Hydrology / LiDAR](../terrain-hydrology-lidar/SKILL.md) |
| Untrusted GIS inputs, secrets and security | [GIS Security Guardian](../gis-security-guardian/SKILL.md) (cross-cutting) |
| Integrate GIS HTTP APIs safely | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) |
| Geocoding | [API Integration](../geospatial-api-integration/SKILL.md) · [providers](../../references/geocoding-place-search.md) |
| Gazetteer search / scale-aware labels | [World Gazetteer](../../references/world-gazetteer-reference-layer.md) |
| WKID, projection, transformation, vertical datum | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) |
| Symbology / map layout | [Cartography](../gis-cartography-symbology/SKILL.md) |
| OS/device permissions and sensor adapters | [System Capabilities & Permissions](../system-capabilities-and-permissions/SKILL.md) |
| Device time/location/sunlight | [Device Context](../gis-device-context/SKILL.md) |
| KML/KMZ / Earth viewers | [KML](../kml-kmz-engineering/SKILL.md) · [Earth](../../references/google-earth-pro-engineering.md) · [Timeline](../../references/historical-imagery-timeline.md) |


**Wizard:** [planner](../../project-wizard.html). **Arabic:** [i18n](../gis-internationalization-arabic/SKILL.md). **QA:** [guide](../../docs/PERFORMANCE_RELIABILITY.md).

## Global rules (always apply)

Use the [repository-wide execution contract](../../references/repository-wide-skill-orchestration.md), [full guidance](../../references/gis-development-full-guidance.md), and [Security Guardian](../gis-security-guardian/SKILL.md). Protect source data, verify CRS/units/NoData, preserve provenance, check licenses/versions, measure performance and test output readback. Obtain required approval before consequential writes; host permissions remain authoritative. Use [DoneProof](../completion-evidence/SKILL.md). Record T0–T4 evidence using the [acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md) and [validator](../../scripts/validate_evidence.py). Never treat static checks as native GIS or GUI proof.

**Shared checks:** [security](../../references/geospatial-security.md) · [performance](../../references/geospatial-performance.md).

**More:** [GIS software registry](../../references/gis-software-source-registry.md) · [antipatterns](../../references/gis-antipatterns.md) · [examples](../../examples/README.md).
