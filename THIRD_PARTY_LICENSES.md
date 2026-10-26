# Z EARTH — third-party software, data and redistribution notices

**Source baseline:** Z EARTH v2.2.4 (development preview, 26 October 2026).  
**Project-authored source:** [GPL-3.0-only](LICENSE), subject to ownership and [scope limitations](LEGAL_AND_PRIVACY_NOTICES.md).  
**Bundled independent GIS skills:** [MIT notice](.github/LICENSE); upstream attributions must remain intact.

**Important:** This file is a **partial provenance and obligation inventory**, NOT a complete set of all copyright notices or a finding that all redistributed artifacts are cleared. The old document was labelled v1.0.0 and asserted complete compliance; that assertion was not justified by the actual v2.2.4 binary inventory. Package versions and transitively bundled assets must be verified against the actual release build.

## Code/runtime dependencies seen in source

| Component | Source or pinned reference | License/terms to preserve | Notes |
| --- | --- | --- | --- |
| CesiumJS | Bundled `src/ZEarth.Globe/Assets/cesium/`; historic notes say 1.146.0 | Apache-2.0, plus applicable third-party notices/assets | Upstream: https://github.com/CesiumGS/cesium ; exact embedded version & notices must be verified |
| Microsoft Edge WebView2 | `Microsoft.Web.WebView2` NuGet 1.0.2903.40 | Microsoft software/runtime redistribution terms | https://developer.microsoft.com/en-us/microsoft-edge/webview2/ |
| .NET 10 / WPF | `net10.0-windows`, `UseWPF` | Applicable .NET open-source/runtime licenses; verify any redistributable terms | https://github.com/dotnet/wpf |
| GDAL / OGR | `MaxRev.Gdal.Core` 3.13.3.583; `MaxRev.Gdal.WindowsRuntime.Minimal` 3.13.3.583 | GDAL MIT/X-style core and its per-driver/provider dependencies; MaxRev package terms | https://github.com/OSGeo/gdal |
| PROJ / `proj.db` | Packaged from MaxRev.Gdal.Core by `ZEarth.App.csproj` | PROJ MIT-style license and any separately acquired data/grid terms | https://github.com/OSGeo/PROJ |
| SQLite and Microsoft.Data.Sqlite | `Microsoft.Data.Sqlite` 10.0.13 | SQLite public-domain statement; .NET provider and dependency terms | https://www.sqlite.org/ |
| System.Drawing.Common | `System.Drawing.Common` 10.0.13 | .NET dependency licensing and platform restrictions | https://github.com/dotnet/runtime |
| Copied GIS Development Skills | `.github/skills/`, `.github/references/`, `.github/examples/` | MIT and upstream attributions, as shipped in [`.github/LICENSE`](.github/LICENSE) and snapshot | https://github.com/ahmadibrahim4geo/GIS-Development-Skills |

**Compatibility caveat:** Apache-2.0 can be combined with GPLv3 in relevant circumstances, but each original Apache license/NOTICE must still be preserved. Separate WebView2 SDK/runtime rights, asset metadata, dependencies and provider data rights need their own review. Do not infer GPL compatibility of an entire deliverable from one dependency's headline license.

## Embedded third-party artifacts requiring release-audit evidence

The repository tree contains a vendored Cesium distribution, including `Cesium.js`, `index.js`, `index.cjs`, shaders/workers, images, tiles and these notable files:

- `src/ZEarth.Globe/Assets/cesium/ThirdParty/basis_transcoder.wasm`
- `src/ZEarth.Globe/Assets/cesium/ThirdParty/draco_decoder.wasm`
- `src/ZEarth.Globe/Assets/cesium/ThirdParty/wasm_splats_bg.wasm`
- `src/ZEarth.Globe/Assets/cesium/ThirdParty/zip-module.wasm`
- `src/ZEarth.Globe/Assets/cesium/ThirdParty/google-earth-dbroot-parser.js`
- `src/ZEarth.Globe/Assets/cesium/Assets/Textures/` and `Assets/Images/`

**Inspection outcome:** no standalone `LICENSE`, `COPYING` or `NOTICE` was identified inside the *vendored Cesium subtree* in the main-branch Git tree reviewed on 26 October 2026. Embedded code comments may contain notices; absence of standalone files is not proof of missing rights or compliance. A release must inventory provenance and supply applicable third-party notices for binary/transitive dependencies, including those assets and any third-party data packs.

## Online map and data providers (not an unrestricted asset license)

| Provider | Attribution/permission considerations |
| --- | --- |
| OpenStreetMap data & tiles | © OpenStreetMap contributors; data ODbL terms, hosted tile usage policy and permitted caching separate. https://www.openstreetmap.org/copyright |
| Natural Earth | Public-domain source datasets as represented by the distributor; document exact sourced resolution/files and attribution. https://www.naturalearthdata.com/ |
| OpenTopoMap | © OpenStreetMap contributors and OpenTopoMap; map data/style/tile services have separate rules. https://opentopomap.org/ |
| CARTO basemaps | © OSM contributors and CARTO; provider terms and attribution apply. https://carto.com/attributions |
| Esri World Imagery / Wayback | Hosted-provider terms, fair use, archive release metadata, tile caching and attribution apply; no redistribution of Esri imagery implied. |
| Other terrain, imagery and geocoding | Evaluate the active provider's terms at use time. Optional paid APIs do not become free through Z EARTH's GPL license. |

## Release compliance checklist (pending)

1. Build a manifest of all **actual** shipped DLLs, NuGet runtime assemblies, fonts, Cesium sub-assets, WebAssembly modules, data layers and other binaries with versions and hash/provenance.
2. Retrieve and preserve every applicable original license, copyright notice and required Apache `NOTICE` (if present), including Cesium and its subcomponents. Do not invent missing notices or simply reproduce incomplete extracts.
3. Review original MIT/copyleft obligations for bundled GIS skills separately from Z EARTH's GPL source.
4. Document provider attribution (including within the UI where required), service rate limits, caching/offline restrictions and supplied data rights.
5. Confirm GPL-covered source/Corresponding Source, build scripts and license access accompany any redistributed portable preview/installer as required; assess commercial runtime and linking compatibility with appropriate legal expertise if needed.
6. Only after these checks, describe a release as redistribution-cleared. Current CI passes and a checked-in license **do not** certify that state.

Useful upstream references: [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html), [Apache / GPL compatibility](https://www.apache.org/licenses/GPL-compatibility), [CesiumJS upstream license](https://github.com/CesiumGS/cesium/blob/main/LICENSE.md), [GDAL license](https://github.com/OSGeo/gdal/blob/master/LICENSE.TXT), [PROJ COPYING](https://github.com/OSGeo/PROJ/blob/master/COPYING).
