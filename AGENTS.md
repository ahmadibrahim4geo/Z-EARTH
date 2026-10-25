# Z-EARTH — Agent engineering instructions

This repository is a **Windows desktop GIS application**; code inspection alone is never proof that the globe works. Preserve the official product name **Z EARTH**, stable `ZEarth.*` namespaces, dark gray primary style, English default UI, a complete Arabic UI, and **one canonical Preview folder**. Never bypass build/test failures to manufacture a release.

## Product goal and priority

Z EARTH is an independent free-first alternative to **Google Earth Pro Desktop**. Use [Google Earth Pro parity contracts](docs/GOOGLE_EARTH_PRO_PARITY.md) before fixing Places, Temporary, startup Scenes, .kml/.kmz, Basemaps, terrain, measurements, drawing or profile. **Never generate plausible but fictitious terrain elevations**: true profile values must come from the active numeric terrain source (the current dialog only charts explicitly stored path Z). GEBCO 2026 is a dataset, not a ready Cesium TerrainServer. The upstream REA analysis tool is [morluto/rea](https://github.com/morluto/rea); the user's existing local working folder is `D:\\My Software\\Zearth\\rea`. Read `.github/skills/zearth-rea-parity/SKILL.md` for authorized read-only desktop behavior inspection. Do not run the local tool blindly, reinstall it, or claim Google Earth Pro runtime evidence from the cloud. Preserve independent lawful code, do not copy Google Earth binaries or copyrighted map tiles.

## First steps in a fresh checkout
1. Read this file and [the pinned general skill snapshot](docs/GIS_SKILLS_INTEGRATION.md).
2. Route the user's issue through `.github/skills/gis-development/SKILL.md` and `.github/references/agent-decision-tree.md`, then read only 1–3 relevant specialized skills. The **Z-EARTH project-specific skills** and [CesiumJS skill router](.github/skills/using-cesiumjs-skills/SKILL.md) refine the generic guidance; they take precedence about local behavior.
3. Inspect actual code paths rather than trusting old release notes or test filenames. Draw the chain **XAML binding → MainViewModel command → GlobeBridge request/response → cesium-engine.js command → Cesium scene effect → WPF state**.
4. List reproducible failure, cause, files modified, baseline test, regression test, and evidence level (T0 code review, T1 static/syntax, T2 unit/fixture, T3 real Cesium/WebView2 Windows test, T4 packaged deployment).
5. Prefer **free, correctly licensed** tools (CesiumJS, GDAL/PROJ, .NET SDK, SQLite) and permitted public tile sources. Public hosted Esri/OSM services and Cesium ion have usage terms, quotas and credits; do not assume perpetual unrestricted commercial rights or ship tokens.
6. Open a feature branch/PR, keep original files and project schema compatible, use real negative/timeout tests, build and run dotnet tests before merging. **FakeGlobeEngine returning true is NOT a native WebView2 or raster acceptance test**.

## Specialist routing for Z EARTH
- WPF UI, XAML, MVVM, command state, splash/theme/language: `.github/skills/zearth-desktop-ux/SKILL.md`, `.github/skills/zearth-ribbon-and-splash/SKILL.md`, `.github/skills/gis-desktop-application-development/SKILL.md`.
- Globe, 2D/3D modes, camera, time/sunlight/atmosphere, tile layers: `.github/skills/zearth-globe-bridge/SKILL.md`, `.github/skills/geospatial-web-mapping/SKILL.md`, `.github/skills/cesiumjs-viewer-setup/SKILL.md`, `.github/skills/cesiumjs-terrain-environment/SKILL.md`.
- KML/KMZ and folder hierarchy/persistence: `.github/skills/kml-kmz-engineering/SKILL.md`, `.github/skills/zearth-kml-integrity/SKILL.md`, `.github/skills/zearth-project-recovery/SKILL.md`.
- EPSG/CRS, GDAL, snapping and vector: `.github/skills/coordinate-reference-systems/SKILL.md`, `.github/skills/geospatial-data-processing/SKILL.md`, `.github/skills/zearth-crs-transformations/SKILL.md`, `.github/skills/zearth-geodata-pipeline/SKILL.md`.
- Online basemaps and API terms: `.github/skills/geospatial-api-integration/SKILL.md`, `.github/skills/zearth-provider-compliance/SKILL.md`.
- Tests, performance, evidence, safety: `.github/skills/zearth-verification/SKILL.md`, `.github/skills/zearth-security/SKILL.md`, `.github/skills/gis-development/SKILL.md`.

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
