# GIS Development Skills — pinned and vendored into Z EARTH

Upstream source: https://github.com/ahmadibrahim4geo/GIS-Development-Skills

**Pin date:** 2026-10-25. **Upstream source commit:** `ef26b76a134b89e950b616e30326df91bd226b43`.
**Verification manifest:** [GIS_SKILLS_SNAPSHOT.json](GIS_SKILLS_SNAPSHOT.json).

### What is actually vendored

- **21** generic `.github/skills/<specialist>/SKILL.md` documents, including `gis-cartography-symbology`, `gis-device-context`, `gis-security-guardian`, `kml-kmz-engineering`, `gis-desktop-application-development`, plus **`prompt-intent-orchestrator` and `agent-development-journal`**.
- **54** source-matched engineering references and catalogs in `.github/references`: Google Earth Pro, dated imagery, Google/OGC KML/KMZ, World Gazetteer, icon library, imagery/APIs, security references, CRS and desktop engineering.
- Selected accompanying offline example fixtures, **executable intent/journal helpers** and Python unit tests under `.github/examples`, `.github/scripts` and `.github/tests` for agent use. Not every upstream command or example is a native Z EARTH executable. The GIS Development Skills source-generated `repository-manifest.json` is deliberately **not** copied because its metrics do not describe Z EARTH.
- **All existing `zearth-*` and `cesiumjs-*` skills are retained**, plus the new project-specific `zearth-google-earth-pro-workflows` adapter.

### How agents must load the skills

1. Always start with [AGENTS.md](../AGENTS.md), [Google Earth Pro parity](GOOGLE_EARTH_PRO_PARITY.md), [Z EARTH agent workflow](ZEARTH_AGENT_WORKFLOW.md) and the pinned source manifest. Verify locally: `python scripts/validate-agent-skills.py`.
2. For complex user requests, first use `.github/skills/prompt-intent-orchestrator/SKILL.md` with a reviewable plan and real host approval, then route via `.github/skills/gis-development/SKILL.md`. For Earth/Wayback work, load `.github/skills/zearth-google-earth-pro-workflows/SKILL.md` and [the implemented Historical Imagery contract](HISTORICAL_IMAGERY_TIMELINE.md). Then load 1–3 specialized skills only.
3. **Project-specific requirements take precedence** about current WPF/Cesium wiring. The general Earth history design is source-independent; the existing Wayback timeline uses Esri **release dates**, not actual per-pixel acquisition dates.
4. For untrusted KMZ, network resources, credentials and release workflows, apply both local `zearth-security` and the imported `gis-security-guardian`.
5. After authorized meaningful execution batches, record numbered, **private, gitignored** local project history using `.github/scripts/development_journal.py`; a helper does not automatically listen for every application process or execute code. Record T0–T4 evidence honestly. Source copied successfully does not mean app runtime uses a feature; app behavior must be wired and tested separately.

### Source review status and integration details

The source pin is from upstream [GIS Development Skills PR #20](https://github.com/ahmadibrahim4geo/GIS-Development-Skills/pull/20) at commit `ef26b76a134b89e950b616e30326df91bd226b43`, **not from an upstream merged `main` update** at sync time. It adds exactly two cross-cutting specialist skills and three reference documents relative to the previous Z EARTH snapshot. The upstream-generated `repository-manifest.json` is intentionally excluded from the 54 vendored references; it describes the source repository, not this project.

The original Z EARTH/CesiumJS specialists stay unchanged. New local glue lives in `.github/skills/zearth-agent-workflow/SKILL.md`, and the onboarding, privacy, review/approval, offline tests, and limitations are detailed in [Z EARTH Agent Workflow](ZEARTH_AGENT_WORKFLOW.md). Both skills are **development-agent guidance**, not an application feature or direct WPF/Cesium runtime integration.

### Provenance, boundaries, and updates

The original upstream skills and guidance are MIT licensed; source packages, icons, imagery, hosted APIs, Cesium assets, Google/Esri services and individual external projects have independent licenses. Google Earth Blog is an independently maintained historical reference; do not scrape Google imagery or copy Google Earth binaries.

The snapshot is reproducible and **not dynamically updated in the deployed app**. Its manifest records the original Git blob SHA of every pinned general skill and reference. The offline validator verifies each SHA, frontmatter, local links, XML/XAML and the Z EARTH adapter; mismatches fail CI. To update again, review a new upstream commit, copy only permitted files under `.github/skills` and `.github/references`, regenerate checksums and pin, update this document and [AGENTS.md](../AGENTS.md), and test a PR. Never overwrite the project-local or Cesium specialists automatically.

**Native runtime tests still required:** WPF controls, WebView2/Cesium, zoom/camera persistence, network/provider restrictions, historical imagery release switching, project save/open and one canonical Preview deployment.
