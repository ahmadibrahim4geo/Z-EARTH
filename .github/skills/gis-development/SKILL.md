---
name: gis-development
description: Route GIS tasks to ArcGIS, QGIS, raster, remote sensing, KML, desktop, web and API specialists with verifiable tests.
license: MIT
---

# GIS Development — Agent Router

**Free-first:** [policy](../../references/free-first-policy.md); disclose costs.

**Examples:** [DEM](../../examples/terrain-slope/README.md), [STAC](../../examples/stac-offline/README.md).

**Intake:** [Global contract](../../references/repository-wide-skill-orchestration.md) → [Intent review](../prompt-intent-orchestrator/SKILL.md) → Router.

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
| Standalone GIS desktop / installer | [GIS Desktop Development](../gis-desktop-application-development/SKILL.md) |
| Clean-room / reference parity | [Reference Implementation](../clean-room-reference-implementation/SKILL.md) |
| Android/iOS GIS apps, GNSS, offline sync | [GIS Mobile Application Development](../gis-mobile-application-development/SKILL.md) |
| Command inventory, dead controls, handlers, shortcuts | [UI Command Audit](../gis-ui-command-audit/SKILL.md) |
| Windows UI, WebView2 and installer tests | [Desktop E2E Testing](../gis-desktop-e2e-testing/SKILL.md) |
| Import → render → save/export provenance or missing layers | [GIS Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md) |
| Images, thumbnails, logos and visual resources | [Visual Assets](../visual-asset-discovery-integration/SKILL.md) |
| UI/UX, accessibility, keyboard and visual regression | [UI/UX Design & Testing](../gis-ui-ux-design-testing/SKILL.md) |
| Discover/download external data | [Geospatial Data Discovery](../geospatial-data-discovery/SKILL.md) |
| Source directories, repositories and STAC indexes | [Geospatial Data Catalogs](../geospatial-data-catalogs/SKILL.md) |
| GIS AI agents | [Agents](../geospatial-ai-agents/SKILL.md) |
| Architecture/flow explanations, design/diagnosis, structural changes | [Visualization](../gis-architecture-visualization/SKILL.md) (conditional) |
| MCP/native tools, fallback and capability preflight | [MCP Tool Orchestration](../mcp-integration-and-tool-orchestration/SKILL.md) |
| DEM, terrain, hydrology, LiDAR / point clouds | [Terrain / Hydrology / LiDAR](../terrain-hydrology-lidar/SKILL.md) |
| Untrusted GIS inputs, secrets and security | [GIS Security Guardian](../gis-security-guardian/SKILL.md) (cross-cutting) |
| Integrate GIS HTTP APIs safely | [Geospatial API Integration](../geospatial-api-integration/SKILL.md) |
| Geocoding | [API Integration](../geospatial-api-integration/SKILL.md) · [providers](../../references/geocoding-place-search.md) |
| Gazetteer search / scale-aware labels | [World Gazetteer](../../references/world-gazetteer-reference-layer.md) |
| WKID, projection, transformation, vertical datum | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) |
| Symbology / map layout | [Cartography](../gis-cartography-symbology/SKILL.md) |
| OS/device permissions and sensors | [System Capabilities & Permissions](../system-capabilities-and-permissions/SKILL.md) |
| Device time/location/sunlight | [Device Context](../gis-device-context/SKILL.md) |
| KML/KMZ / Earth viewers | [KML](../kml-kmz-engineering/SKILL.md) · [Earth](../../references/google-earth-pro-engineering.md) · [Timeline](../../references/historical-imagery-timeline.md) |


**Wizard:** [planner](../../project-wizard.html). **Arabic:** [i18n](../gis-internationalization-arabic/SKILL.md). **QA:** [guide](../../docs/PERFORMANCE_RELIABILITY.md).

## Global rules (always apply)

[MCP/native routing](../../references/mcp-integration-workflow.md) follows domain/host preflight; the [execution contract](../../references/repository-wide-skill-orchestration.md), [full guidance](../../references/gis-development-full-guidance.md) and [Security Guardian](../gis-security-guardian/SKILL.md) apply. Preserve sources/provenance; check CRS/units/NoData, licenses/versions, performance and readback. Require approval for consequential writes and host permissions. Apply [DoneProof](../completion-evidence/SKILL.md) and record T0–T4 using the [acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md) and [validator](../../scripts/validate_evidence.py). Static checks ≠ native GIS/GUI proof.

**Shared checks:** [security](../../references/geospatial-security.md) · [performance](../../references/geospatial-performance.md).

**More:** [GIS software registry](../../references/gis-software-source-registry.md) · [antipatterns](../../references/gis-antipatterns.md) · [examples](../../examples/README.md).
