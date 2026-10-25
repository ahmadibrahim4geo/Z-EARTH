# Z EARTH — downloadable Windows portable preview

The source version in `src/ZEarth.App/ZEarth.App.csproj` is **2.2.4**. The old v1.0.0 release-notes and package filename describe an earlier milestone, not a current downloadable Windows release. No official production Release or digitally signed installer is claimed here.

## Download from GitHub Actions

1. Open the [Z EARTH build and tests workflow](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/workflows/build-and-test.yml).
2. Open the **latest successful run on main**, not an older pull-request run.
3. Scroll to **Artifacts** and download **Z-EARTH-v2.2.4-win-x64-portable-preview**.
4. Extract the downloaded ZIP fully to a folder you trust, then run `Z EARTH.exe` from that extracted folder. Do not move the EXE away from its accompanying DLLs, Assets, JSON and other files.

The CI build uploads this *preview artifact only after* Python/XAML, Node/Cesium and .NET Windows test gates succeed, and after a Windows x64 self-contained publish, asset checks and ZIP creation. GitHub artifact availability requires permitted access to Actions, and **artifacts expire after 30 days**. This is not yet a permanent GitHub Release. The app and preview remain **unsigned**.

## Requirements / limitations

- Windows 10/11 x64, WebGL-capable GPU and installed Microsoft Edge WebView2 Runtime. The ZIP contains the .NET Windows Desktop runtime, so users do not normally need the .NET SDK or separate .NET Desktop Runtime.
- Some online imagery, terrain and geocoding capabilities need internet and remain dependent on third-party provider terms, limits and current service status. Offline fallback is not feature parity with Google Earth Pro.
- The Esri Wayback slider identifies archived **basemap release dates**. It does *not* prove sensor scene acquisition dates in the current viewport.
- The elevation profile may use stored polyline vertex Z; a scientific active-DEM sampled profile is **not yet implemented**.
- WPF/CesiumJS GPU/WebView2 real-use acceptance (T3) and fresh-machine deployment (T4) are **not guaranteed by green CI alone**. Report crashes, blank globe, KML/KMZ style loss, failed layers or provider errors with screenshots/logs and Windows/WebView2 versions.
- The artifact extraction does not change file associations or Windows registry; the separate legacy installer scripts are not part of the portable workflow.
- Unsigned builds may trigger protection warnings. Verify project provenance and the EXE SHA-256 in `Z-EARTH.exe.sha256` before running; do not bypass security restrictions to launch an untrusted file.

## Developer: manual package

On Windows with the .NET 10 SDK:

```powershell
./scripts/create-portable-preview.ps1
```

The command runs the existing fail-closed build/test workflow, then builds a **self-contained win-x64 portable preview** under the single `dist/Z EARTH-Preview/` folder. It also writes a local `dist/Z-EARTH-v2.2.4-win-x64-portable-preview.zip`. The legacy `scripts/package-portable.ps1` simply calls this script.

CI uses `./scripts/create-portable-preview.ps1 -TestsAlreadyPassed` *only after* its restore/build/test steps have succeeded. It uploads the folder directly to GitHub Actions to avoid a ZIP nested inside another ZIP.

## Current backlog

See open issue [#3 terrain and real DEM profiles](https://github.com/ahmadibrahim4geo/Z-EARTH/issues/3), [#4 Places/KML/Explorer](https://github.com/ahmadibrahim4geo/Z-EARTH/issues/4), and [#5 measurement/cartography/themes](https://github.com/ahmadibrahim4geo/Z-EARTH/issues/5). Code-level CI success does not close these native-functionality acceptance gaps.
