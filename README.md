# Z EARTH — Desktop GIS & 3D Globe

Open-source-oriented **Windows desktop GIS development project** using **.NET 10 WPF / MVVM**, **CesiumJS hosted in WebView2**, **GDAL/PROJ**, **SQLite**, KML/KMZ and geospatial data visualization.

**Current source version:** 2.2.4 (see `src/ZEarth.App/ZEarth.App.csproj`). This number alone is **not evidence of release readiness**. The codebase is undergoing systematic stabilization; successful unit tests alone do not prove that interactive WebView2 globe functions work.

**Developer:** Ahmed Ibrahim. [الدليل بالعربية](README.ar.md) · [Agent guide](AGENTS.md) · [User manual EN](docs/USER_MANUAL_EN.md) · [User manual AR](docs/USER_MANUAL_AR.md) · [Licensing and privacy](LEGAL_AND_PRIVACY_NOTICES.md).

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
| `.github/skills/` | Project-specialized CesiumJS skills **and 16 pinned GIS Development Skills** |

## GIS skill integration: new Codex/Agent checkout

Read [AGENTS.md](AGENTS.md) as the first action. The [vendor snapshot](docs/GIS_SKILLS_INTEGRATION.md) copies 16 domain skills, their direct reference documents and the MIT notice from [GIS Development Skills](https://github.com/ahmadibrahim4geo/GIS-Development-Skills), **without replacing existing Z EARTH skills**.

The agent picks the task-specific chain. Example: *Sunlight toggle not working* → `gis-development` router → `gis-desktop-application-development` + `geospatial-web-mapping` → existing `zearth-globe-bridge` and `cesiumjs-terrain-environment` → correct source and acceptance tests.

## Before reporting a feature as fixed

1. **T0:** identify code path and concrete failure.
2. **T1:** syntax/format/static checks pass.
3. **T2:** .NET unit and negative-response tests pass.
4. **T3:** demonstrate actual hardware Windows/WebView2 engine behavior (navigation, basemap, time/lighting, 2D/3D, terrain, KML, save/open).
5. **T4:** install/run the verified portable Preview on a fresh Windows machine.

Newly cloned agents must not conflate T2 with T3/T4. Follow [release evidence checklist](docs/FRESH_CLONE_VALIDATION.md).

## Current limitations

This source review is not a promise that all basemap providers work or every feature is production ready. External tile servers require real connectivity and provider rights, and paid Cesium ion services are **optional**. Windows GPU UI acceptance is required. Build/test CI is only a prerequisite.

## Licensing

See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) and [LEGAL_AND_PRIVACY_NOTICES.md](LEGAL_AND_PRIVACY_NOTICES.md). The copied GIS skill texts are MIT-licensed **separately from source projects they reference**. Never bundle proprietary GIS SDKs, cache prohibited tiles or publish personal credentials.
