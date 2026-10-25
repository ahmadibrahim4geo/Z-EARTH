# Pinned GIS Development Skills snapshot

- Upstream project: https://github.com/ahmadibrahim4geo/GIS-Development-Skills
- Source revision: `5868ea33db2a22843f09120ebab961d02cc313d0` (2026-10-25)
- License text copied to `.github/LICENSE`: MIT (the copied GIS guidance only).
- **16 skills**, copied to `.github/skills/<skill>/SKILL.md`; directly linked guidance and catalog files mirrored to `.github/references`, `.github/examples`, `.github/docs`, `.github/scripts`, `.github/templates`.
- Previous `zearth-*` and `cesiumjs-*` skills are preserved, not replaced.
- New *project-local* `zearth-rea-parity` specialist explains how to call the existing MIT-licensed [morluto/rea](https://github.com/morluto/rea) on an authorized Windows machine. It is not a copy of REA's tool source or Google Earth Pro binaries.

For new checkouts: read [AGENTS.md](../AGENTS.md), route via `.github/skills/gis-development/SKILL.md`, then use relevant project skills. Do not load all 16 into context at once. If a snapshot example references an upstream Python implementation that is **not copied**, open the original source repository; do not call the reference document an executable tool.

Updating a snapshot requires a separate reviewed PR that refreshes all referenced paths, retains upstream attribution, and runs skill-link tests and application tests. **External software/services do not inherit MIT** and can be commercial or access-limited. See [free-first policy](../.github/references/free-first-policy.md).
