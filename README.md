<p align="center">
  <img src="assets/branding/zearth-repo-header.webp" alt="Z EARTH Desktop GIS — official repository banner with blue Z globe emblem and topographic background" width="720" />
</p>

# Z EARTH — Desktop GIS & 3D Globe

Open-source-oriented **Windows desktop GIS development project** using **.NET 10 WPF / MVVM**, **CesiumJS hosted in WebView2**, **GDAL/PROJ**, **SQLite**, KML/KMZ and geospatial data visualization.

**Current source version:** 2.2.4 (see `src/ZEarth.App/ZEarth.App.csproj`). This number alone is **not evidence of release readiness**. The codebase is undergoing systematic stabilization; successful unit tests alone do not prove that interactive WebView2 globe functions work.

**Developer:** Ahmed Ibrahim. [الدليل بالعربية](README.ar.md) · [Agent guide](AGENTS.md) · [User manual EN](docs/USER_MANUAL_EN.md) · [User manual AR](docs/USER_MANUAL_AR.md) · [Licensing and privacy](LEGAL_AND_PRIVACY_NOTICES.md).

## Reference software evidence lab and optional clouds (2026-10-26)

The [Google Earth reference framework](docs/google-earth-reference/README.md) now has **eight reference analysis reports**, **four ZEarth source audit reports**, a [30-feature coverage matrix](docs/google-earth-reference/FEATURE_PARITY_MATRIX.md), and a 12-role [audit manifest](docs/google-earth-reference/AGENT_MANIFEST.json). The [read-only evidence runner](scripts/reference_lab_audit.py) runs one scoped role or all twelve; GitHub Actions runs 12 isolated source-evidence jobs. These are **deterministic inspections, not independently launched LLM agents, and not native Windows parity tests**. No Google application binary, resource bundle or shader is committed.

Z EARTH now includes a **Clouds** button in View → Lighting & Atmosphere: optional *offline illustrative* 3D cumulus using Cesium `CloudCollection`, disabled by default and hidden in 2D. It does **not** show actual cloud-cover observations. The option is persistent and localized (EN/AR). [Source](src/ZEarth.Globe/Assets/cloud-layer.js) · [Mock tests](tests/cloud-layer.test.cjs). CI build/unit pass does not yet establish true GPU/WebView2 visual success.

Local evidence checks from a cloned repository:
```bash
python scripts/reference_lab_audit.py --all --out-dir reference-evidence
node --test tests/cloud-layer.test.cjs
```

## Latest application feature work — 26 October 2026

These are **runtime code changes to ZEarth itself**, not additional GIS skills. Source and test presence does not yet prove Windows GPU/WebView2 parity with Google Earth Pro.

| Application feature | Added behavior | Honest limit |
| --- | --- | --- |
| **GPS / GPX** | Import GPX 1.0/1.1 waypoints, routes and track segments into Temporary Places; retain UTC fix times. Select a timed path to **Play GPS / Stop GPS** as a 30-second timeline preview | Missing/out-of-order fix times are rejected for playback; marker replay is not live GPS or a terrain-elevation measurement |
| **Flight** | Basic independent Cesium 3D navigation, keyboard steering/throttle, status HUD and safe exit with mouse controls restored | Navigation prototype only: not an aircraft flight-physics model, autopilot, takeoff/landing simulator or Google asset |
| **Coordinate grid** | User-selectable UTM hemisphere zone (36N), geographic grid zone designation (36R), or both. Latitude/longitude intervals stay independently configurable | Native polar/label overlap testing and accessibility review still needed |
| **Historical imagery** | Existing Esri Wayback release timeline plus date-selectable **NASA GIBS** MODIS Terra/Aqua WMS, with removal and NASA attribution | NASA true color is lower-resolution daily observation imagery, not Google Earth's commercial high-resolution archive; layer configuration does not guarantee a matching tile |
| **Elevation Profile** | Resample a path geometrically, request real numeric height samples from the active Cesium terrain provider and graph measured DEM heights | If terrain service is unavailable/incomplete the graph explicitly reverts to **stored vertex Z**, not a fabricated DEM; WebView2 live terrain requests require verification |
| **Camera navigation** | Flight mode no longer overrides Home/North/Zoom; North/Pitch command acknowledgements occur after camera animation finishes | Native camera gesture and pole/dateline acceptance tests remain necessary |

Implementation: [GPX parser](src/ZEarth.Domain/Gps/GpxService.cs) · [GPS playback](src/ZEarth.Globe/Assets/gps-track-playback.js) · [flight navigation](src/ZEarth.Globe/Assets/flight-simulator.js) · [grid labels](src/ZEarth.Globe/Assets/graticule-geometry.js) · [NASA GIBS](src/ZEarth.Globe/Assets/nasa-gibs-imagery.js) · [terrain path sampler](src/ZEarth.Domain/Terrain/TerrainProfilePathBuilder.cs). Source-specific unit/JS tests are exercised in [CI](.github/workflows/build-and-test.yml); **real application functionality must still be tested on a Windows desktop with WebView2 and GPU**.

## Download the Windows EXE (Portable Preview)

The GitHub source repository previously **did not publish its compiled .exe**; green Actions builds only uploaded unit-test logs. The [Windows portable preview guide](docs/PORTABLE_PREVIEW.md) now explains how to download the **latest successful main-branch Actions artifact** named `Z-EARTH-v2.2.4-win-x64-portable-preview`, extract it, and launch `Z EARTH.exe` with bundled Cesium assets.

[Open the latest build / download artifacts](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/workflows/build-and-test.yml). The unsigned self-contained **preview** is subject to CI publication/30-day retention; it is not a production GitHub Release. Native Windows GPU/WebView2 checks remain necessary. Existing release notes labelled v1.0.0 are historical and should not be mistaken for the current 2.2.4 source.

## Fresh clone: free prerequisites

- Windows 10/11 x64 and **.NET 10 SDK** for source development.
- Microsoft Edge WebView2 Runtime, GPU/WebGL 2 compatible for interactive globe (test your machine).
- Git and optionally Node.js (for the Cesium JavaScript syntax check). **No ArcGIS or Global Mapper license required**.
- **No paid API key** is required for basic offline mode. Online imagery, terrain and hosted services are subject to the provider's license, fair-use limits, availability and optional token requirements.

```powershell
git clone https://github.com/ahmadibrahim4geo/Z-EARTH.git
cd Z-EARTH
dotnet restore ZEarth.sln
dotnet build ZEarth.sln --configuration Release --no-restore
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj --configuration Release --no-build
.\scripts\build-and-test.ps1
```

Launch `dist/Z EARTH-Preview/Z EARTH.exe` **only after** tests and package validation pass. **Single preview policy:** build into a staging folder and replace the single Preview output only after successful validation. See [Windows acceptance checklist](docs/FRESH_CLONE_VALIDATION.md).

## Architecture

| Project path | Role |
| --- | --- |
| `src/ZEarth.App/` | WPF desktop shell, XAML, MVVM commands, settings, project management |
| `src/ZEarth.Globe/` | Bidirectional C# GlobeBridge, packaged CesiumJS/HTML/Workers |
| `src/ZEarth.Domain/` | Geospatial data, CRS/GDAL, KML/KMZ, SQLite, snapping, geometry |
| `tests/ZEarth.Tests/` | Automated unit/component tests; many use **FakeGlobeEngine** |
| `scripts/` | Safe build, Windows screenshot harnesses, reproducibility |
| `.github/skills/` | **30 pinned GIS Development Skills** (single skill source; no local duplicates) |

## GIS Development Skills — single development-agent source

The **only active skills** in `.github/skills/` are the **30 verified source-matched GIS Development Skills specialists**, pinned with **76 references** from upstream [GIS Development Skills](https://github.com/ahmadibrahim4geo/GIS-Development-Skills) `main` commit `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`. The 33 duplicated local Z EARTH/CesiumJS specialist skills have been removed, not used as fallback. Original history remains recoverable from Git. No executable, Cesium assets, or application behavior has been changed by this documentation migration.

Start from [root agent instructions](AGENTS.md), then [the GIS router](.github/skills/gis-development/SKILL.md), selecting the relevant upstream specialist. The actual Z EARTH implementation requirements remain in [Google Earth parity](docs/GOOGLE_EARTH_PRO_PARITY.md), [KML/Explorer acceptance](docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md), [terrain/grid acceptance](docs/TERRAIN_GRID_ACCEPTANCE.md), and [historical imagery contract](docs/HISTORICAL_IMAGERY_TIMELINE.md). For safety/verification use `gis-security-guardian`, `completion-evidence` and true native test results.

[Integration and replacement record](docs/GIS_SKILLS_INTEGRATION.md) · [Pinned source checksum manifest](docs/GIS_SKILLS_SNAPSHOT.json). Execute `python scripts/validate-agent-skills.py` to check inventory, provenance, source file links and XAML. **SKILL.md documents do not run in the installed desktop program.**

## Before reporting a feature as fixed

1. **T0:** identify code path and concrete failure.
2. **T1:** syntax/format/static checks pass.
3. **T2:** .NET unit and negative-response tests pass.
4. **T3:** demonstrate actual hardware Windows/WebView2 engine behavior (navigation, basemap, time/lighting, 2D/3D, terrain, KML, save/open).
5. **T4:** install/run the verified portable Preview on a fresh Windows machine.

Newly cloned agents must not conflate T2 with T3/T4. Follow [release evidence checklist](docs/FRESH_CLONE_VALIDATION.md).

## Google Earth Pro parity & engineering roadmap

Our project targets the useful **Google Earth Pro Desktop** workflows (saved camera scenes and ordered My Places, Temporary Places, KML/KMZ, name/coordinate search, 3D terrain, profiles, measurement, drawing, themes, imagery). The [detailed parity matrix](docs/GOOGLE_EARTH_PRO_PARITY.md) separates implemented fixes from still-planned features. Unlike the previous draft, profile elevation is never *fabricated* from sine waves; the current chart is clearly labelled **stored path vertex Z** until true active-DEM sampling is implemented.

**REA tool:** [morluto/rea](https://github.com/morluto/rea) is the existing MIT-licensed local reverse-engineering **analysis tool**, not Google Earth Pro source. The user's `D:\\My Software\\Zearth\\rea` workspace stays outside tracked Z EARTH. We reviewed REA docs but have **not run REA on the user's local Google Earth Pro installation**; use the documented [Google Earth parity criteria](docs/GOOGLE_EARTH_PRO_PARITY.md). No proprietary Google Earth binaries or extracted code are bundled.

## Historical imagery timeline (Esri Wayback)

The **Historical Imagery → Show Timeline** command now opens a **compact nonmodal bar over the 3D globe**, not a blocking Windows dialog. Use **previous/next**, first/latest, the draggable release slider, the year selector or a calendar date. If the selected day is not an actual archive release, the viewer selects the nearest release **on or before** that date. Close (× or Escape in the timeline) to restore the previous basemap. Pan and zoom remain usable while the bar is open.

The slider uses Esri World Imagery Wayback map **release dates** (archive snapshots dating from 2014), not Google Earth Pro's proprietary time series and **not actual satellite acquisition dates**. Imagery and local update coverage vary, and a requested tile is not guaranteed to load. The real Esri release catalog must be available online; no invented fallback dates or paid service keys are used. Esri hosted services have independent terms and attribution. [Implementation and validation details](docs/HISTORICAL_IMAGERY_TIMELINE.md).
## Current limitations

This source review is not a promise that all basemap providers work or every feature is production ready. External tile servers require real connectivity and provider rights, and paid Cesium ion services are **optional**. Windows GPU UI acceptance is required. Build/test CI is only a prerequisite.

## Licensing

See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) and [LEGAL_AND_PRIVACY_NOTICES.md](LEGAL_AND_PRIVACY_NOTICES.md). The copied GIS skill texts are MIT-licensed **separately from source projects they reference**. Never bundle proprietary GIS SDKs, cache prohibited tiles or publish personal credentials.

## Development skills source snapshot

The single reviewed [GIS Development Skills](https://github.com/ahmadibrahim4geo/GIS-Development-Skills) skill catalog includes 30 specialists and 76 source references, pinned to upstream `main` commit `222699fb04a8d6ea2583f13e5e37dbfc6a0f55cd`. All duplicate Z EARTH/CesiumJS skills were removed from the active skill directory. See [migration documentation](docs/GIS_SKILLS_INTEGRATION.md) and [checksums](docs/GIS_SKILLS_SNAPSHOT.json).
