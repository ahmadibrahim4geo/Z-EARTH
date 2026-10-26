# Z EARTH — repository entry point / بداية الاستخدام

**State as documented on 26 October 2026:** development / portable preview. A passing GitHub Actions build is not a substitute for interactive Windows WebView2/GPU acceptance. Follow the [current limitations](../README.md#current-limitations).

| I want to… | Start here |
| --- | --- |
| Understand the software | [English README](../README.md) / [الدليل العربي](../README.ar.md) |
| Download the latest tested Windows preview | [Portable preview guide](PORTABLE_PREVIEW.md) (short-lived GitHub Actions artifact, **not** a production Release) |
| Compile the application | [Fresh-clone validation](FRESH_CLONE_VALIDATION.md) |
| Check Google Earth Pro parity | [Implemented vs planned matrix](GOOGLE_EARTH_PRO_PARITY.md) |
| Test import, KML/KMZ, search, grids or Sunlight | [Native acceptance checklist](FRESH_CLONE_VALIDATION.md); provide actual UI evidence |
| Contribute changes | [Contributing guide](../CONTRIBUTING.md) |
| Report a bug | [GitHub Issues](https://github.com/ahmadibrahim4geo/Z-EARTH/issues/new/choose) |
| Report a private security vulnerability | [Security guide](../SECURITY.md) |
| Understand source-code license status | [GNU GPLv3 source license](../LICENSE); [component inventory](../THIRD_PARTY_LICENSES.md); [release obligations](LICENSE_STATUS.md) |

## Evidence tiers

1. **T0:** static/source inspection.
2. **T1:** syntax and format checks.
3. **T2:** unit/component tests.
4. **T3:** interactive native Windows GPU/WebView2/UI behavior on actual hardware.
5. **T4:** fresh-machine installation/execution plus representative real inputs.

When T3/T4 were not performed, write **NOT RUN**, not “fixed”, “working” or “verified”. For KML/KMZ, test import → model/scene → save/export → reopen, preserving original styles, local icons, geometry, altitude, CRS and folder hierarchy wherever supported.

## GitHub presentation tasks (account owner)

Repository **About** suggested description:

> Experimental Windows GIS desktop and 3D globe project based on .NET WPF, CesiumJS/WebView2, GDAL/PROJ and KML/KMZ workflows.

Suggested topics: `gis`, `desktop-gis`, `cesiumjs`, `wpf`, `webview2`, `kml`, `kmz`, `geospatial`.

Do not link a nonexistent official homepage or mark preview builds as stable Releases.
