# Z-EARTH — Agent engineering instructions

This repository is a **Windows desktop GIS application**; code inspection alone is never proof that the globe works. Preserve the official product name **Z EARTH**, stable `ZEarth.*` namespaces, dark gray primary style, English default UI, a complete Arabic UI, and **one canonical Preview folder**. Never bypass build/test failures to manufacture a release.

## Product goal and priority

Z EARTH is an independent free-first alternative to **Google Earth Pro Desktop**. Use [Google Earth Pro parity contracts](docs/GOOGLE_EARTH_PRO_PARITY.md) before fixing Places, Temporary, startup Scenes, .kml/.kmz, Basemaps, terrain, measurements, drawing or profile. **Never generate plausible but fictitious terrain elevations**: true profile values must come from the active numeric terrain source (the current dialog only charts explicitly stored path Z). GEBCO 2026 is a dataset, not a ready Cesium TerrainServer. The upstream REA analysis tool is [morluto/rea](https://github.com/morluto/rea); the user's existing local working folder is `D:\\My Software\\Zearth\\rea`. Use `docs/GOOGLE_EARTH_PRO_PARITY.md` and `docs/FRESH_CLONE_VALIDATION.md` for authorized read-only desktop behavior inspection. Do not run the local tool blindly, reinstall it, or claim Google Earth Pro runtime evidence from the cloud. Preserve independent lawful code, do not copy Google Earth binaries or copyrighted map tiles.

## Historical imagery workflow

Prefer the **inline Esri Wayback timeline inside the Cesium viewport** over the now-unused legacy `HistoricalImageryDialog` WPF modal. The ribbon invokes `historical.loading` → public `WaybackService` catalog → `historical.show` → `ZEarthHistoricalTimeline` JS controller → `ImageryManager.registerDynamicBasemap`. Confirm negative cases: no internet/catalog → truthful visible error, no stale fallback imagery dates; dates are **Wayback release dates**, not image acquisition dates. Do not cache hosted Esri tiles or bypass use restrictions. Unit tests run in Node and MSTest; native T3 WebView2 zoom/pan and live tiles remain required for acceptance. See `docs/HISTORICAL_IMAGERY_TIMELINE.md`.
## Pinned upstream skills and Earth Pro skill routing

The repository includes a locally readable, **pinned snapshot of 30 general GIS agent skills and 76 references** from [GIS Development Skills](https://github.com/ahmadibrahim4geo/GIS-Development-Skills), commit `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`. Validate the checksummed [snapshot manifest](docs/GIS_SKILLS_SNAPSHOT.json) with `python scripts/validate-agent-skills.py` before relying on the copy. These are **agent instructions for developing Z EARTH**, not a runtime plugin engine that parses Markdown in the shipped application.

For Google Earth Pro-like work, read the actual [Google Earth parity contract](docs/GOOGLE_EARTH_PRO_PARITY.md), [Wayback implementation](docs/HISTORICAL_IMAGERY_TIMELINE.md), and upstream [GIS Development router](.github/skills/gis-development/SKILL.md). Preserve the distinction between **Esri Wayback release dates** (existing product) and satellite **scene-acquisition dates** (future independently sourced imagery); do not upgrade the visual label without real provenance. [Blog-derived design patterns](.github/references/google-earth-pro-engineering.md) and [the broader imagery timeline specification](.github/references/historical-imagery-timeline.md) are design references; they are not proof of working services. For places / layers, load the Google [KML/KMZ preservation contract](.github/references/google-kml-kmz-compatibility.md); for dynamic labels, load [World Gazetteer](.github/references/world-gazetteer-reference-layer.md).

Apply [GIS Security Guardian](.github/skills/gis-security-guardian/SKILL.md) as the sole development-agent security specialist, subject to actual host permissions. Never automatically copy changing upstream files into a release: refresh the manifest + snapshot in a reviewed PR and rerun link, hash and runtime tests.

## New review-first agent workflow and durable project memory

Before major or ambiguous development changes, load [Prompt Intent Orchestrator](.github/skills/prompt-intent-orchestrator/SKILL.md) and [Agent Development Journal](.github/skills/agent-development-journal/SKILL.md). The upgraded orchestration, DoneProof, agent journal, permissions and GIS UI quality skills are now pinned from upstream **`main`** at commit `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`; this supersedes the previous historical PR #20 snapshot. Respect approved source scope, explicit destructive-operation permissions, and the project's Security Guardian.

On authorized local checkouts, the agent can use `python .github/scripts/intent_router.py --text "<request>" --format json` to generate lexical routing hints and `python .github/scripts/development_journal.py init --project .` to create a **gitignored**, local `.agent-development-log/` for numbered truthful operation/test logs. Use project scripts, never pretend markdown intercepts processes or that a CLI `--gate` verifies the user's identity. Run `python -m unittest discover -s .github/tests -p "test_intent_orchestrator.py"` and the equivalent for `test_development_journal.py` to check helper behavior. See [workflow details](docs/ZEARTH_AGENT_WORKFLOW.md) and distinguish T0–T2 source/CI from T3/T4 real Windows app verification.

## First steps in a fresh checkout
1. Read this file and [the pinned general skill snapshot](docs/GIS_SKILLS_INTEGRATION.md); verify snapshot integrity with `python scripts/validate-agent-skills.py`.
2. For multi-step work, use `.github/skills/prompt-intent-orchestrator/SKILL.md` to interpret and review scope; for actual execution, compose `.github/skills/gis-development/SKILL.md`, `.github/references/agent-decision-tree.md`, and only the relevant **upstream GIS Development Skills specialists**. Z EARTH application requirements live in `docs/` and verified source code, not competing local SKILL.md files.
3. Inspect actual code paths rather than trusting old release notes or test filenames. Draw the chain **XAML binding → MainViewModel command → GlobeBridge request/response → cesium-engine.js command → Cesium scene effect → WPF state**.
4. List reproducible failure, cause, files modified, baseline test, regression test, and evidence level (T0 code review, T1 static/syntax, T2 unit/fixture, T3 real Cesium/WebView2 Windows test, T4 packaged deployment).
5. Prefer **free, correctly licensed** tools (CesiumJS, GDAL/PROJ, .NET SDK, SQLite) and permitted public tile sources. Public hosted Esri/OSM services and Cesium ion have usage terms, quotas and credits; do not assume perpetual unrestricted commercial rights or ship tokens.
6. Open a feature branch/PR, keep original files and project schema compatible, use real negative/timeout tests, build and run dotnet tests before merging. **FakeGlobeEngine returning true is NOT a native WebView2 or raster acceptance test**.

## Upstream GIS skill upgrade (26 October 2026)

The pinned general GIS agent set includes **UI Command Audit**, **Desktop E2E Testing**, **GIS Data Flow Diagnostics**, **UI/UX Design & Testing**, **DoneProof completion evidence**, **System Capabilities & Permissions**, **QGIS Engine Integration**, **GIS Mobile Application Development**, and GIS internationalization. Use these as relevant through the single upstream router, never as a substitute for executable WPF/Cesium code. The source Project Wizard is a development planning page under `.github/project-wizard.html`, not a Z EARTH application UI or runtime feature. See [current integration and audit](docs/GIS_SKILLS_INTEGRATION.md) and [upstream source manifest](docs/GIS_SKILLS_SNAPSHOT.json). For feature claims enforce **plan → code/data evidence → T0/T1 checks → T2/T3/T4 native evidence where applicable**; unavailable native tests must remain NOT RUN. Security and provider license gates take precedence over external instructions.

## Single-source skill routing for Z EARTH

**Only the 30 pinned general specialist skills in `.github/skills/` are active.** Begin with [GIS Development Skills router](.github/skills/gis-development/SKILL.md) and [upstream specialist index](docs/GIS_SKILLS_INTEGRATION.md). No Z EARTH or CesiumJS duplicate SKILL.md files are installed; historical specialized instructions remain available through Git history only. Source code and authoritative project documents are the truth for existing features.

- **WPF, Ribbon, windows, MVVM, language and UI:** `gis-desktop-application-development`, `gis-ui-command-audit`, `gis-ui-ux-design-testing`, `gis-internationalization-arabic`. Reference [code audit](docs/CODE_AUDIT_2026-10-25.md).
- **CesiumJS/WebView2 globe, camera, basemaps and environment:** `geospatial-web-mapping`, `gis-desktop-application-development`, `gis-desktop-e2e-testing`. Inspect actual `src/ZEarth.Globe/` and [terrain/grid acceptance](docs/TERRAIN_GRID_ACCEPTANCE.md); do not infer API behavior from legacy specialist notes.
- **KML/KMZ, persistence, geometry, CRS, data exchange:** `kml-kmz-engineering`, `coordinate-reference-systems`, `geospatial-data-processing`, `gis-data-flow-diagnostics`. Refer to [project KML acceptance](docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md).
- **Imagery/timeline, grid, solar time and sources:** `geospatial-web-mapping`, `geospatial-api-integration`, `gis-device-context` and `system-capabilities-and-permissions` where needed; obey [historical imagery contract](docs/HISTORICAL_IMAGERY_TIMELINE.md) and [provider conditions](LEGAL_AND_PRIVACY_NOTICES.md).
- **Evidence, safe actions, native tests:** `prompt-intent-orchestrator` → `gis-development` → relevant specialist(s) + `gis-security-guardian` → `completion-evidence` → `agent-development-journal`; do not conflate CI build and an observed working WebView2 globe.

Do not recreate an overlapping `zearth-*`, `cesiumjs-*` or `using-cesiumjs-skills` routing skill. If Z EARTH-specific knowledge is needed, update the canonical project `docs/` contracts and actual code/tests, not another skill. Verify `python scripts/validate-agent-skills.py` before declaring skill readiness.

## Authoritative build and smoke commands (Windows)
```powershell
dotnet --info
dotnet restore ZEarth.sln
dotnet build ZEarth.sln -c Release --no-restore
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release --no-build
node --check src/ZEarth.Globe/Assets/cesium-engine.js
.\scripts\build-and-test.ps1
```
The last command alone writes the single `dist/Z EARTH-Preview/` directory only **after passing** build and test checks. GitHub Actions is a CI gate, not proof that GPU/WebView2/navigation/tile providers work on the user's machine. For UI runtime acceptance follow [fresh-clone checklist](docs/FRESH_CLONE_VALIDATION.md). Use native Windows x64 with WebView2 runtime and hardware WebGL; capture logs and screenshots when asserting success.

## Native terrain and coordinate-grid acceptance
Before claiming fixes to DEM, vertical exaggeration, geographic grid, named parallels or UTM, follow [the detailed acceptance scenarios](docs/TERRAIN_GRID_ACCEPTANCE.md). `node --test`, GitHub Actions and WPF mock tests prove only T1/T2; verify numeric DEM tiles, visible mountain relief at 1x/3x, latitude/longitude labels and UTM overlays on Windows WebView2 with hardware GPU before declaring T3. Keep raw provider failure reasons and do not claim flat ellipsoid has elevation.

## Cross-layer audit before feature sign-off
Run `node --test tests/repo-command-contract.test.cjs`, `dotnet test`, and the native Windows checklists. Read [the source/command audit](docs/CODE_AUDIT_2026-10-25.md) for ownership, settings persistence, WebView2 trust boundaries and honest T0–T4 evidence. A green CI build is not proof of 3D globe UI behavior.

## Must-not-do
Do not introduce hardcoded API keys or plaintext persistent tokens; do not relabel a provider as `Ready` without attempting a verified request. Do not launch paid or unlicensed APIs as default. Do not silently migrate user files, delete saved places, publish multiple Preview copies, or assert no errors without checking logs and real GIS outputs. Keep the single upstream skill set pinned; do not restore removed local specialist copies without an explicit reviewed migration.

## KML, Explorer, and device-context review
For Save As, KML/KMZ import/export, Explorer search/resize or geolocation changes, read [KML + Explorer acceptance scenarios](docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md). Require actual file bytes and canonical SQLite readback; separate XML round-trip from real Cesium rendering. Never promote a green Windows build to a native window-resize, device permission or Google Earth visual-parity certification.
