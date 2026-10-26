# Z EARTH — GIS Development Skills upstream main sync

**Date:** 2026-10-26  
**Target:** `ahmadibrahim4geo/Z-EARTH` (development-agent infrastructure only)  
**Reviewed source:** `ahmadibrahim4geo/GIS-Development-Skills` `main` commit `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`  
**Baseline Z EARTH:** `f4f9f8616d64784dcd650ba444423d6d5fa2f700`

## Change

- Replace previous general-skill pin from historical feature branch `ef26b76...` (**21 skills, 54 references**) with actual upstream `main` (**30 skills, 76 references**).
- Import missing specialists: GIS UI Command Audit, Desktop End-to-End Testing, GIS Data Flow Diagnostics, GIS UI/UX Design and Testing, DoneProof, System Capabilities and Permissions, Arabic i18n, mobile GIS, QGIS engine integration. Preserve GIS router/intent, security, KML and other source updates.
- Import linked references, source-matched examples, offline helper tools and synthetic tests. The upstream project wizard stays in developer assets under `.github/project-wizard.html`; it is not a new in-app page.
- Refresh `docs/GIS_SKILLS_SNAPSHOT.json`, `docs/GIS_SKILLS_INTEGRATION.md`, root `AGENTS.md` and the offline integrity validator. Generate a *ZEarth-local* skill inventory at `.github/references/repository-manifest.json`, instead of copying the upstream source website's generated counts.
- Preserve all existing `.github/skills/zearth-*` and `.github/skills/cesiumjs-*`, and do not modify desktop runtime `src/`, application data, imagery providers, configuration or Preview output.

## Acceptance and evidence

| Check | Evidence required | Status when authored |
| --- | --- | --- |
| All 30 upstream SKILL files and 76 references have the same Git blob SHAs as pinned source | Tree/object comparison, manifest | T0 comparison staged, CI pending |
| Local routed skill links, SHA manifest and XAML parser | `python scripts/validate-agent-skills.py` | CI pending |
| Added specialist helper fixtures and regression tests | Targeted offline Python `unittest` workflow | CI pending |
| Application build, Node tests and Windows .NET unit tests | Existing `build-and-test.yml` | CI pending |
| Actual WPF/WebView2 GPU/UI command execution and KML/CRS/terrain behavior | Real Windows interactive session (T3/T4) | NOT RUN — no native session in this sync |

**Limitations:** copying a skill file or passing a synthetic test is not evidence that Z EARTH's Ribbon icons, mouse navigation, map layers or sunlight command actually work. Explicit runtime changes and live acceptance require a separate feature task. Do not claim T3/T4 from CI.

## Reproduction

```powershell
python scripts/validate-agent-skills.py
python -m unittest discover -s .github/tests -p "test_gis_ui_quality_four_skills.py" -v
python -m unittest discover -s .github/tests -p "test_doneproof.py" -v
python -m unittest discover -s .github/tests -p "test_system_capabilities_permissions.py" -v
python -m unittest discover -s .github/tests -p "test_gis_mobile_application.py" -v
python -m unittest discover -s .github/tests -p "test_goal_first_workflow_cli.py" -v
dotnet build ZEarth.sln -c Release
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release
```

Restore the previous main commit or revert the skill-sync merge if regressions occur. The upstream source-manifest JSON is intentionally **not** copied because it describes a different repository.
