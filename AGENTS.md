# Z-EARTH — Agent engineering instructions

This repository is a **Windows desktop GIS application**; code inspection alone is never proof that the globe works. Preserve the official product name **Z EARTH**, stable `ZEarth.*` namespaces, dark gray primary style, English default UI, a complete Arabic UI, and **one canonical Preview folder**. Never bypass build/test failures to manufacture a release.

## Product goal and priority

Z EARTH is an independent free-first alternative to **Google Earth Pro Desktop**. Use [Google Earth Pro parity contracts](docs/GOOGLE_EARTH_PRO_PARITY.md) before fixing Places, Temporary, startup Scenes, .kml/.kmz, Basemaps, terrain, measurements, drawing or profile. **Never generate plausible but fictitious terrain elevations**: true profile values must come from the active numeric terrain source (the current dialog only charts explicitly stored path Z). GEBCO 2026 is a dataset, not a ready Cesium TerrainServer. The upstream REA analysis tool is [morluto/rea](https://github.com/morluto/rea); the user's existing local working folder is `D:\\My Software\\Zearth\\rea`. Read `.github/skills/zearth-rea-parity/SKILL.md` for authorized read-only desktop behavior inspection. Do not run the local tool blindly, reinstall it, or claim Google Earth Pro runtime evidence from the cloud. Preserve independent lawful code, do not copy Google Earth binaries or copyrighted map tiles.

## Historical imagery workflow

Prefer the **inline Esri Wayback timeline inside the Cesium viewport** over the now-unused legacy `HistoricalImageryDialog` WPF modal. The ribbon invokes `historical.loading` → public `WaybackService` catalog → `historical.show` → `ZEarthHistoricalTimeline` JS controller → `ImageryManager.registerDynamicBasemap`. Confirm negative cases: no internet/catalog → truthful visible error, no stale fallback imagery dates; dates are **Wayback release dates**, not image acquisition dates. Do not cache hosted Esri tiles or bypass use restrictions. Unit tests run in Node and MSTest; native T3 WebView2 zoom/pan and live tiles remain required for acceptance. See `docs/HISTORICAL_IMAGERY_TIMELINE.md`.
## Pinned upstream skills and Earth Pro skill routing

The repository includes a locally readable, **pinned snapshot of 19 general GIS agent skills and 51 references** from [GIS Development Skills](https://github.com/ahmadibrahim4geo/GIS-Development-Skills), commit `fa05f322424747a3a58b04fcb744c6868df7d759`. Validate the checksummed [snapshot manifest](docs/GIS_SKILLS_SNAPSHOT.json) with `python scripts/validate-agent-skills.py` before relying on the copy. These are **agent instructions for developing Z EARTH**, not a runtime plugin engine that parses Markdown in the shipped application.

For Google Earth Pro-like work, read [Z EARTH's Google Earth workflow adapter](.github/skills/zearth-google-earth-pro-workflows/SKILL.md) after the project parity and Wayback docs. Preserve the distinction between **Esri Wayback release dates** (existing product) and satellite **scene-acquisition dates** (future independently sourced imagery); do not upgrade the visual label without real provenance. [Blog-derived design patterns](.github/references/google-earth-pro-engineering.md) and [the broader imagery timeline specification](.github/references/historical-imagery-timeline.md) are design references; they are not proof of working services. For places / layers, load the Google [KML/KMZ preservation contract](.github/references/google-kml-kmz-compatibility.md); for dynamic labels, load [World Gazetteer](.github/references/world-gazetteer-reference-layer.md).

Apply [GIS Security Guardian](.github/skills/gis-security-guardian/SKILL.md) as a cross-cutting baseline alongside local `zearth-security`. Never automatically copy changing upstream files into a release: refresh the manifest + snapshot in a reviewed PR and rerun link, hash and runtime tests.

## First steps in a fresh checkout
1. Read this file and [the pinned general skill snapshot](docs/GIS_SKILLS_INTEGRATION.md); verify snapshot integrity with `python scripts/validate-agent-skills.py`.
2. Route the user's issue through `.github/skills/gis-development/SKILL.md` and `.github/references/agent-decision-tree.md`, then read only 1–3 relevant specialized skills. The **Z-EARTH project-specific skills** and [CesiumJS skill router](.github/skills/using-cesiumjs-skills/SKILL.md) refine the generic guidance; they take precedence about local behavior.
3. Inspect actual code paths rather than trusting old release notes or test filenames. Draw the chain **XAML binding → MainViewModel command → GlobeBridge request/response → cesium-engine.js command → Cesium scene effect → WPF state**.
4. List reproducible failure, cause, files modified, baseline test, regression test, and evidence level (T0 code review, T1 static/syntax, T2 unit/fixture, T3 real Cesium/WebView2 Windows test, T4 packaged deployment).
5. Prefer **free, correctly licensed** tools (CesiumJS, GDAL/PROJ, .NET SDK, SQLite) and permitted public tile sources. Public hosted Esri/OSM services and Cesium ion have usage terms, quotas and credits; do not assume perpetual unrestricted commercial rights or ship tokens.
6. Open a feature branch/PR, keep original files and project schema compatible, use real negative/timeout tests, build and run dotnet tests before merging. **FakeGlobeEngine returning true is NOT a native WebView2 or raster acceptance test**.

## Specialist routing for Z EARTH
- WPF UI, XAML, MVVM, command state, splash/theme/language: `.github/skills/zearth-desktop-ux/SKILL.md`, `.github/skills/zearth-ribbon-and-splash/SKILL.md`, `.github/skills/gis-desktop-application-development/SKILL.md`.
- Google Earth parity, Historical Imagery/Wayback, Places, overlays and tours: `.github/skills/zearth-google-earth-pro-workflows/SKILL.md`, `.github/references/google-earth-pro-engineering.md`, `.github/references/historical-imagery-timeline.md` (project behavior wins).
- Globe, 2D/3D modes, camera, time/sunlight/atmosphere, tile layers: `.github/skills/zearth-globe-bridge/SKILL.md`, `.github/skills/geospatial-web-mapping/SKILL.md`, `.github/skills/cesiumjs-viewer-setup/SKILL.md`, `.github/skills/cesiumjs-terrain-environment/SKILL.md`.
- KML/KMZ and folder hierarchy/persistence: `.github/skills/kml-kmz-engineering/SKILL.md`, `.github/skills/zearth-kml-integrity/SKILL.md`, `.github/skills/zearth-project-recovery/SKILL.md`.
- EPSG/CRS, GDAL, snapping and vector: `.github/skills/coordinate-reference-systems/SKILL.md`, `.github/skills/geospatial-data-processing/SKILL.md`, `.github/skills/zearth-crs-transformations/SKILL.md`, `.github/skills/zearth-geodata-pipeline/SKILL.md`.
- Online basemaps and API terms: `.github/skills/geospatial-api-integration/SKILL.md`, `.github/skills/zearth-provider-compliance/SKILL.md`.
- Tests, performance, evidence, safety: `.github/skills/zearth-verification/SKILL.md`, `.github/skills/zearth-security/SKILL.md`, `.github/skills/gis-security-guardian/SKILL.md`, `.github/skills/gis-development/SKILL.md`.

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

## Must-not-do
Do not introduce hardcoded API keys or plaintext persistent tokens; do not relabel a provider as `Ready` without attempting a verified request. Do not launch paid or unlicensed APIs as default. Do not silently migrate user files, delete saved places, publish multiple Preview copies, or assert no errors without checking logs and real GIS outputs. Keep `.github/skills/zearth-*` and `.github/skills/cesiumjs-*` intact unless a specific correction is proven.
