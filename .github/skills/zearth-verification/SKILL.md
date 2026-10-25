---
name: zearth-verification
description: Use this skill before claiming feature completion or preparing PR/release.
---
# Test driven implementation and feature parity evidence
- Read actual tracked evidence: `AGENTS.md`, `docs/GOOGLE_EARTH_PRO_PARITY.md`, `docs/FRESH_CLONE_VALIDATION.md`, `docs/ZE_UI_FUNCTIONAL_AUDIT_2026-10-25.md`, and `docs/TERRAIN_GRID_ACCEPTANCE.md`. Do not depend on non-existent `BACKLOG.md`, `FEATURE_MATRIX.csv`, or `KML_TEST_MATRIX.md`.
- For each task write acceptance tests first, then minimal implementation.
- Run .NET build/unit, TypeScript lint/typecheck/tests and appropriate UI/integration checks; report EXACT outcomes.
- Playwright covers embedded web UI only; use Windows UI automation for WPF controls.
- Maintain reproducible benchmarks with machine+dataset metadata; never claim unlimited scalability.
- Preserve failing test artifacts, update docs and record code/license/security issues.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.

## Terrain / grid: required evidence
- DEM/vertical exaggeration: show numerical elevation tile readiness at the camera's ground position, provider/fallback and the factor applied through WPF → GlobeBridge → Cesium; use mountains at 1x vs 3x for native screenshot comparison. Loading one tile or a simulated provider is **not** proof of visible relief.
- Coordinate grid: independently test parallels, meridians, major lines/points, their labels, floating degree intervals and real UTM zones including 1/60, Norway, Svalbard, N/S and dateline-crossing viewports. Visible labels must follow the camera; don't anchor UTM names at 0° latitude.
- Test both 2D and 3D against terrain, after restart, with grid on/off and zoom in/out. A test that inspects source strings is T1; Node, unit and FakeGlobeEngine are T2; real WebView2/GPU and network capture are T3; a fresh Windows portable EXE is T4.
- If T3 or T4 are unavailable, mark the feature **Native acceptance pending**; never describe it as verified on the user's PC.
