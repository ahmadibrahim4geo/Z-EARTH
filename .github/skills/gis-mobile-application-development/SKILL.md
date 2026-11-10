---
name: gis-mobile-application-development
description: Plan, engineer, validate and release genuine offline-first Android and iOS GIS field applications with GNSS, spatial data, secure sign-in, synchronization, touch UX and real-device verification.
license: MIT
compatibility: Cross-platform engineering guidance; Android/iOS builds and permissions require SDKs, simulators or physical devices. Documentation and offline fixtures do not prove native behavior.
---

# GIS Mobile Application Development

**Use this specialist for native or cross-platform mobile GIS applications** (Android, iOS, tablets, rugged field devices). A responsive website, a desktop WebView, an APK screenshot or a static map mockup is not a completed mobile GIS application. An existing field app (QField/Mergin Maps) is a reference, not a preinstalled engine.

## Mandatory route and ownership

Follow [Prompt Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) -> [GIS Development Router](../gis-development/SKILL.md) -> this mobile specialist. Apply [repository orchestration contract](../../references/repository-wide-skill-orchestration.md), [GIS Security Guardian](../gis-security-guardian/SKILL.md), and the [free-first policy](../../references/free-first-policy.md) at preflight, implementation and release. Only approved work may execute; reading this document grants no device privileges.

| Responsibility | Owner and handoff |
| --- | --- |
| Mobile application navigation, lifecycle, packaging and platform integration | This specialist |
| Spatial CRS, datum and units | [Coordinate Reference Systems](../coordinate-reference-systems/SKILL.md) |
| Vector/raster operations, deterministic GIS outputs | [Geospatial Data Processing](../geospatial-data-processing/SKILL.md) |
| KML/KMZ import/export, original symbology and safe archives | [KML/KMZ Engineering](../kml-kmz-engineering/SKILL.md) |
| Renderer, vector tiles, terrain and map sources | [Geospatial Web Mapping](../geospatial-web-mapping/SKILL.md), only if needed |
| GNSS/time semantics and accuracy provenance | [GIS Device Context](../gis-device-context/SKILL.md) |
| OS permission and sensor adapters | [System Capabilities and Permissions](../system-capabilities-and-permissions/SKILL.md) |
| Authentication threat model and protected secrets | [GIS Security Guardian](../gis-security-guardian/SKILL.md) |
| Touch UX, readability, Arabic/English and accessibility | [GIS UI/UX](../gis-ui-ux-design-testing/SKILL.md) and [GIS Internationalization](../gis-internationalization-arabic/SKILL.md) |
| Import -> map -> edit -> queue -> sync -> export trace | [GIS Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md) |
| Source-code upstream catalog | [GIS Software Registry](../../references/gis-software-source-registry.md); do not duplicate it |

Never automatically route mobile work to Desktop E2E/Windows UIA. Use a dedicated native mobile device test matrix. QGIS-based field projects may additionally use [QGIS Engine Integration](../qgis-engine-integration/SKILL.md) after actual ABI/platform and licensing checks.

## Architecture selection, not one mandatory engine

Use [mobile architecture and engines guide](../../references/mobile-gis-architecture.md). Record device OS/API range, architecture, geodata formats, GPS accuracy, memory/battery/storage constraints, offline use, map rights and signed package ownership. Choose:
- QGIS-centric field editing: evaluate QField/Mergin Maps code and actual mobile Qt/QGIS builds, GPL obligations, versions and field project constraints.
- Lightweight independent 2D map: evaluate MapLibre Native (renderer) plus explicit GIS storage/processing/CRS layers. Rendering is not geoprocessing.
- Android native: Kotlin/Java with suitable GIS adapters; iOS native: Swift with suitable GIS adapters.
- Shared UI: Flutter or .NET MAUI with version-tested mobile map SDKs and native permission bridges; do not assume mobile map SDK parity.
- Optional ArcGIS Maps SDK only when the user's licensed context authorizes it; no silent paid requirement.

## End-to-end mobile GIS contract

1. **Open/create project:** show local workspace, stable IDs, schema/CRS, layers, provenance, file permissions and dataset version. Do not silently upload user data.
2. **Map and data:** pan/zoom/rotate/identify/select/edit point-line-polygon, read supported GeoPackage/GeoJSON/GPX/KML/KMZ and raster/tile sources. Explicitly declare per-driver read/write limits and unsupported styles. Preserve feature IDs, timestamps, original CRS and UTF-8 Arabic attributes through round-trip tests.
3. **GNSS:** foreground consent; show horizontal accuracy, fix time, coordinate system, altitude source and stale status; record manual or external GNSS provenance separately. Background tracking needs a second explicit platform policy review.
4. **Field forms:** validation, required fields, media links, attachments, transaction rollback, undo, crash recovery and image EXIF/location minimization. Never let photo capture, microphone or location be a prerequisite unless the actual workflow needs it.
5. **Offline-first:** local durable transactions, download scope, storage estimates, basemap license/attribution, data version, delta queue and outbox before remote acknowledgment. Follow [offline sync protocol](../../references/mobile-gis-offline-sync.md).
6. **Sign-in and session:** allow local-only guest use where appropriate; distinguish offline unlocked state from online authenticated state; never claim login succeeded without server verification. Follow [sign-in and privacy contract](../../references/mobile-gis-authentication.md).
7. **Sync:** explicit upload/download triggers or documented background policy; immutable operation IDs, retries, idempotency, conflict detection, partial failure, permission-denied and logged-out handling. Avoid last-write-wins without an approved conflict rule.
8. **Export/share:** atomic local export, independent readback, retention of geometry, CRS, fields and eligible symbology/attachments, provenance, user-chosen share intent and safe filename/path handling.
9. **UI/UX:** touch target sizing, safe area, orientation, tablet layout, accessibility, localization RTL/BiDi, focus and keyboard, network/offline status, storage and battery controls, system font scale and high-contrast.
10. **Privacy/security:** least privileges, no secret in repo/logs/project/KML, OS keychain/keystore, TLS, validated deep links, scoped storage, secure backup and deletion. No automatic geographic telemetry or plaintext tokens.

## Native acceptance and release gates

Read [mobile device testing matrix](../../references/mobile-gis-device-test-matrix.md). T0 checks contracts and links; T1 uses deterministic offline permission/sync/auth fixtures; T2 tests real GIS file readback and adapters; T3 uses Android emulator/device plus iOS simulator/device with real map, permissions and lifecycle interaction; T4 signs, installs, launches, updates and uninstalls packages with release evidence. iOS release/signing generally needs an eligible Apple toolchain and developer setup. Mark unavailable native gates **NOT RUN**, never PASS.

Minimum scenario chain: launch -> import real geodata -> identify -> obtain/deny GPS -> edit while offline -> kill/restart -> inspect queue -> sign in/recover session if configured -> reconnect -> detect server conflict -> reconcile without loss -> export -> reread file. Check both RTL and LTR and accessibility. Capture device OS/build, source SHA, app version, permission state, GIS/SDK versions, test artifacts and failures in the [shared acceptance report](../../templates/GIS_ACCEPTANCE_REPORT.md).

An [offline contract example](../../examples/mobile-gis/README.md) and [mobile regression tests](../../tests/test_gis_mobile_application.py) prove only reference policy and deterministic sync semantics, **not** that an Android/iOS binary or device driver has been built.

## Visual assets (conditional)

When the reviewed project needs thumbnails, official logos, photographs, illustrations or screenshots, compose [Visual Asset Discovery & Integration](../visual-asset-discovery-integration/SKILL.md). It owns internet/source discovery, asset suitability, source credits and prepared local variants; keep layout, target resource binding, packaging and actual UI acceptance with this platform/UX owner. Do not silently replace requested images with generic icons or claim a source URL proves local integration.
