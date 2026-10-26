# Z EARTH — Terrain, Geographic Grid, Polar Markers and Sunlight

**Date:** 2026-10-26  
**Source branch:** `fix/terrain-grid-poles-sunlight-20261026`  
**Pull request:** https://github.com/ahmadibrahim4geo/Z-EARTH/pull/17

## Reproduced reports and source-level findings (T0)
1. At global zoom, north/south pole and 0°/0° anchor markers could be displayed through the globe because labels/points disabled depth testing for unlimited camera distance.
2. Disabling the geographic grid cleared entity collections but did not explicitly disable the datasource. UTM zone boundaries and major reference lines are distinct subclasses, yet must obey the parent Show Grid state.
3. AWS Terrarium startup checked the root and viewport tile sequentially; two 20-second operations could outlast the existing 30-second host/JS bridge request and falsely appear as activation failure.
4. The Cesium sunlight Now button updated UTC hours but reused the previously selected calendar date across midnight. The WPF time slider received only a number, not the full date or real-time intent.

## Implemented repairs
- **Occlusion:** all graticule labels/points use depth tests. Geographic anchor points additionally use a camera-facing WGS84 geodetic-normal horizon predicate from `graticule-geometry.js`, applied to the point and label.
- **Atomic grid OFF:** `GraticuleManager.buildGraticules()` now gates `CustomDataSource.show` by the master visibility setting and clears entities, including dynamic UTM and named geographic references.
- **Terrain:** both independent numeric DEM probes execute concurrently via `Promise.all`. Tile network deadlines are aligned with the bridge deadline (25-second fetch/proxy, 45-second bridge), without reporting ON until both decoded height grids are verified. Failed or unlicensed providers still revert to ellipsoid and display their existing explicit errors.
- **Sunlight:** Now refreshes the full UTC date/time. Manual Cesium clock is held at the selected time instead of silently drifting. The date and real-time intent cross the JS → GlobeBridge → WPF boundary and update `SunlightDateUtc`. The Now action restores real-time intent on the next toggle.

## Changed implementation files
- `src/ZEarth.Globe/Assets/cesium-engine.js`
- `src/ZEarth.Globe/Assets/graticule-geometry.js`
- `src/ZEarth.Globe/GlobeBridge.cs`
- `src/ZEarth.App/ViewModels/MainViewModel.cs`
- `src/ZEarth.App/Views/MainWindow.xaml.cs`

## Regression tests
- `tests/graticule-geometry.test.cjs`: north/south pole visibility, prime-meridian origin, tangent/invalid camera.
- `tests/terrain-grid-source-contract.test.cjs`: grid OFF datasource, depth, probe concurrency, bridge deadline and full UTC date/real-time contract.
- GitHub Actions runs source/Node/Python tests and Windows WPF .NET build/unit/portable packaging. CI results: https://github.com/ahmadibrahim4geo/Z-EARTH/pull/17/checks

## Scientific and operational boundaries
- **T0/T1/T2** are source, syntax/unit/build evidence. Passing CI does **not** confirm native globe visuals.
- **T3** remains open: Windows WebView2/GPU rendering with actual AWS terrain tiles, polar rotation, grid ON/OFF at multiple scales, solar terminator at chosen UTC instants, and 2D/3D mode changes.
- **T4** remains open: clean portable preview on an independent Windows profile with reproducible screenshots and logs.
- Cesium ion terrain still needs an authorized token. The WGS84 ellipsoid is intentionally flat. No API keys, fabricated elevation values, unlicensed basemap providers, or duplicate portable preview folders were added.

## Native sign-off scenarios
1. At Atlantic-centered globe scale, verify that North/South Pole and 0°/0° marker points never show on the rear hemisphere; rotate 360° and repeat in 2D/3D.
2. Enable all normal geographic/major/UTM lines; use Show Grid OFF and ensure no residual violet lines, golden reference parallels, blue meridians, marker labels or pole dots remain. Reenable and verify saved choices.
3. At mountainous Sinai and Alpine sites, activate AWS Terrain, capture the DEM probe (z/x/y, sample count, min/max) and compare actual visible relief at 1× and 3×. Verify offline and wrong-ion-token failures show accurate provider names/reasons.
4. Check Sunlight Now across UTC midnight, rotate from Egypt into the opposite hemisphere, test manual dates/times and no unwanted Cesium time drift.
5. Save/reopen and repeat. Only mark the fix fully accepted after T3/T4 evidence.
