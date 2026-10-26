# GIS Development Skills — reviewed upstream main snapshot for Z EARTH

**Updated:** 2026-10-26. **Canonical source:** https://github.com/ahmadibrahim4geo/GIS-Development-Skills/tree/222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd
**Source branch:** `main`. **Pinned commit:** `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`.
**Verification manifest:** [GIS_SKILLS_SNAPSHOT.json](GIS_SKILLS_SNAPSHOT.json).

## Vendored content and unchanged project boundaries

- **30** source-matched `.github/skills/<specialist>/SKILL.md` files and **76** source-matched `.github/references/` files. Every mirrored source file has the upstream Git blob SHA listed in the manifest. The archived `gis-development/LEGACY_FULL_SKILL.md` is a companion, not another routed specialist.
- Imported compatible helpers, fixtures, tests and the upstream planning wizard live in `.github/scripts/`, `.github/examples/`, `.github/tests/`, `.github/templates/`, `.github/docs/`, `.github/native_tests/` and `.github/project-wizard.html`.
- The source repo's generated `references/repository-manifest.json` is **not** copied: instead, `.github/references/repository-manifest.json` is a separately generated **ZEarth-local** inventory including all local `zearth-*` and `cesiumjs-*` skills. It does not claim the external source site's catalog counts.
- All Z EARTH/CesiumJS project specialists and code under `src/` are preserved. Installing a development skill does not automatically add a WPF Ribbon command, GIS renderer, device permission or new app feature.

## Routing and new scope

1. Start with [root Z EARTH AGENTS.md](../AGENTS.md), [Earth workflow adapter](../.github/skills/zearth-google-earth-pro-workflows/SKILL.md), and [project workflow](ZEARTH_AGENT_WORKFLOW.md).
2. For multi-step development, use [Prompt Intent Orchestrator](../.github/skills/prompt-intent-orchestrator/SKILL.md) → [GIS Development Router](../.github/skills/gis-development/SKILL.md) → project-specific domain owner → [GIS Security Guardian](../.github/skills/gis-security-guardian/SKILL.md) → [DoneProof](../.github/skills/completion-evidence/SKILL.md). An agent's Markdown cannot itself grant user/OS permissions.
3. GIS command audits: `gis-ui-command-audit`; native WPF/WebView2/Windows interaction tests: `gis-desktop-e2e-testing`; import/CRS/renderer/save/export lineage: `gis-data-flow-diagnostics`; interface design, accessibility and Arabic: `gis-ui-ux-design-testing` + `gis-internationalization-arabic`.
4. Conditional related expertise: `system-capabilities-and-permissions` and `gis-device-context` for device/time/location; `qgis-engine-integration` for reviewed optional engine integration; `gis-mobile-application-development` only for actual mobile projects, **not** a mandate to convert this desktop app.
5. The upstream [Project Wizard](../.github/project-wizard.html) is a separate developer planning aid. Nothing is automatically installed or invoked from external repositories. Keep the local JavaScript globe, provider licenses, data and project formats under Z EARTH's existing contracts.

## Integrity and tests

Run `python scripts/validate-agent-skills.py` in the Z EARTH root for checksum/frontmatter/link/XAML checks. Run relevant offline Python helper tests from the `.github` working directory; they validate contracts and synthetic fixtures only. Run actual app `dotnet build`, `dotnet test`, `node --check`, relevant Node tests, and a **real Windows WebView2/Cesium acceptance session** before claiming end-to-end function. A GitHub CI success is **not** a native GUI test.

This snapshot is intentionally pinned, not live-syncing with upstream `main`. Refresh through reviewed changes, regenerate the manifest and record actual evidence. Upstream original content is MIT; third-party code, icon packs, satellite maps, online services and data remain subject to their independent terms. Preserve user data, one canonical Preview, and existing local project skills.
