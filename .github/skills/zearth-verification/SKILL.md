---
name: zearth-verification
description: Use this skill before claiming feature completion or preparing PR/release.
---
# Test driven implementation and feature parity evidence
- Read `BACKLOG.md`, `FEATURE_MATRIX.csv`, and `KML_TEST_MATRIX.md`.
- For each task write acceptance tests first, then minimal implementation.
- Run .NET build/unit, TypeScript lint/typecheck/tests and appropriate UI/integration checks; report EXACT outcomes.
- Playwright covers embedded web UI only; use Windows UI automation for WPF controls.
- Maintain reproducible benchmarks with machine+dataset metadata; never claim unlimited scalability.
- Preserve failing test artifacts, update docs and record code/license/security issues.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
