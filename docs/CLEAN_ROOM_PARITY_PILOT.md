# Reference-driven functional parity pilot (Google Earth Pro)

This document is an implementation and verification plan, **not** evidence that any behavior is complete. Apply [upstream clean-room guidance](https://github.com/ahmadibrahim4geo/GIS-Development-Skills/blob/main/skills/clean-room-reference-implementation/SKILL.md). Do not copy proprietary Google code, imagery, assets or restricted services. The pilot is independent implementation guided by observable behavior; strict clean-room claims require separate teams and controlled information barriers.

## Phase 1: Placemark and KML/KMZ
- Observe public UI behavior for add/edit/delete placemark, folder visibility and save/open.
- Record exact test fixtures: coordinates, altitude mode, icon URI, name, Unicode description, nested folders and style.
- Validate serialized KML/KMZ, reload and re-export without silently dropping supported fields.
- Run native WPF/WebView2 UI tests as well as unit tests; capture logs/screenshots and label unavailable Windows tests NOT VERIFIED.

## Phase 2: Globe and camera
- Check pan/zoom/rotate, reset north, home, 2D/3D and saved camera; test poles, dateline, extreme zoom and startup/shutdown.

## Phase 3: Grid and sunlight
- Independently test global Show Grid, longitude/latitude toggles, major geographic lines, labels, and disabling all overlay primitives.
- Test sunlight time/date changes and off-state restoration; require Cesium bridge readback.

## Phase 4: Search, layers and export
- Test gazetteer/geocoding with provider terms, layer visibility and persisted state; export data using permitted interoperable formats.

## Evidence matrix
For every feature log: feature ID, reference version, ZEarth commit, steps, fixture, expected, actual, result (PASS/FAIL/NOT VERIFIED), evidence URL/path, environment, owner, issue link. Green unit tests with FakeGlobeEngine never imply live Cesium/WebView2 parity.

## Delivery
Coordinate changes with existing `docs/GOOGLE_EARTH_PRO_PARITY.md`, `AGENTS.md`, Windows acceptance checklist, and upstream GIS skill routing. Do not replace or duplicate existing local skill snapshots until the upstream synchronization mechanism is verified. Merge only verified changes; keep one portable preview.
