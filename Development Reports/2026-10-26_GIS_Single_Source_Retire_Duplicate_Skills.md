# Z EARTH — Retire duplicate development-agent skills

**Date:** 2026-10-26  
**User directive:** Replace local Z EARTH/CesiumJS development agent specialists with the shared GIS Development Skills skill collection.  
**Upstream source:** `ahmadibrahim4geo/GIS-Development-Skills` at `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`.

## Observed baseline

There were **63 active** `SKILL.md` manifests: **30 upstream source-matched** and **33 project-specific** (Z EARTH/CesiumJS) skill manifests. The old `AGENTS.md`, README and skill validator continued to prioritize local adapters. These conflicts are a documentation/routing risk; the presence of a duplicate alone does **not** prove a real app fault.

## Safe migration

- Remove all 33 project-specific `zearth-*`, `cesiumjs-*`, `using-cesiumjs-skills` skill folders from `.github/skills` and their subordinate non-runtime examples. Git history retains the prior contents if ever needed.
- Keep the **30 upstream general skills and 76 source-reference snapshots unchanged**, including the exact pinned hashes. Preserve all `src/` code, actual Cesium assets, Windows .NET build, tests, user-facing assets, data, project files and Preview policies.
- Update `AGENTS.md`, `.github/AGENTS.md`, English/Arabic READMEs, the integration and agent workflow guides, and the Z EARTH-local skill inventory.
- Harden `scripts/validate-agent-skills.py` so a stray active local skill, stale active routing pointer or mismatched manifest fails CI.

## Verification levels

| Acceptance item | Expected observation | Evidence level |
| --- | --- | --- |
| Active upstream skill inventory | Exactly 30 skills, no local duplicates | T0 tree + CI validator |
| Vendored source pin | All 30 skills and 76 references match original SHA entries | T0 manifest + CI |
| Guides and links | Active entrypoints route through generic GIS router | T0 + CI |
| Application functional behavior | Build/Node/.NET unit suites unaffected | CI T1/T2 |
| Actual Windows WebView2/terrain/KML map behavior | Interactive real target machine required | T3/T4 NOT RUN by skill migration |

This is **not** an application bug fix or a claim that the 30 generic skills implement missing features. Restore the pre-migration commit via a Git revert if unforeseen agent tooling depends on a removed local guide.
