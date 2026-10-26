# GIS Development Skills — only active Z EARTH agent skill source

**Migrated:** 2026-10-26. **Canonical source:** https://github.com/ahmadibrahim4geo/GIS-Development-Skills/tree/222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd  
**Pinned upstream `main`:** `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd` · [exact checksums](GIS_SKILLS_SNAPSHOT.json)

## Removed: duplicated local specialist instructions

Z EARTH previously had 63 discoverable agent skills: **30 pinned GIS Development Skills specialists** and **33 project-specific `zearth-*`/`cesiumjs-*`/`using-cesiumjs-skills` entries**. Following the requested simplification, all 33 project-specific specialist folders were removed from the *active skill directory*. Their previous content remains accessible through Git history, **not** as a runtime or agent fallback.

This change **does not remove WPF, CesiumJS, WebView2, .NET, KML/KMZ or any project runtime code or saved user data**. Local application-specific requirements remain in [root AGENTS.md](../AGENTS.md), [Google Earth Pro parity](GOOGLE_EARTH_PRO_PARITY.md), [KML/Explorer acceptance](KML_EXPLORER_ACCEPTANCE_2026-10-25.md), [terrain/grid acceptance](TERRAIN_GRID_ACCEPTANCE.md), [historical imagery](HISTORICAL_IMAGERY_TIMELINE.md), and [full runtime validation](FRESH_CLONE_VALIDATION.md). Historical reports may mention paths that were valid when they were written; they are not active routing documentation.

## What remains active

- **30 source-matched upstream specialist `SKILL.md` documents** in `.github/skills/` and **76 corresponding upstream references**, unchanged from the pinned source, with Git blob SHA verification.
- The existing reviewed utility scripts, fixtures, tests, and developer-only [Project Wizard](../.github/project-wizard.html), which do **not** run as app plugins.
- One source router: [GIS Development](../.github/skills/gis-development/SKILL.md), with relevant upstream [Desktop](../.github/skills/gis-desktop-application-development/SKILL.md), [KML](../.github/skills/kml-kmz-engineering/SKILL.md), [Web Mapping/Cesium](../.github/skills/geospatial-web-mapping/SKILL.md), [UI Command Audit](../.github/skills/gis-ui-command-audit/SKILL.md), [Desktop E2E](../.github/skills/gis-desktop-e2e-testing/SKILL.md), [Data Flow](../.github/skills/gis-data-flow-diagnostics/SKILL.md), and [UI/UX](../.github/skills/gis-ui-ux-design-testing/SKILL.md) as needed.
- Security and completion: `gis-security-guardian` and `completion-evidence`; no local duplicate specialist can override them.

## Safety and verification

Agents read [root AGENTS.md](../AGENTS.md) → upstream router → one or more relevant generic specialists → exact project contracts and real code. No skill file itself can grant permission, send data or make Cesium render successfully.

Run `python scripts/validate-agent-skills.py` for upstream hashes, **exact inventory (reject unexpected extra SKILL.md)**, local link checks, removal of stale active routing, and XML/XAML syntax. Native Windows/real GPU/WebView2/terrain/KML acceptance remains required before declaring actual features working. Changes to an installed app need separate code changes and T3/T4 evidence; static source checks only establish agent instruction consistency.

Third-party plugins/data/imagery retain their independent license terms. Never auto-sync a moving upstream branch or restore legacy skills without a reviewed change.
