# Terrain and coordinate-grid follow-up — 29 October 2026

## Scope

Continuation of local/global DEM and online GEBCO fixes, plus geographic grid motion, label positioning, Map Grid preview and Options dialog sizing. Changes are in `canonical-main`; pre-existing worktree changes were preserved. The original user TIFF was opened read-only with GDAL PAM disabled.

## Changes

- Local numeric raster retains NaN for missing coverage; the globe displays the ellipsoid in missing areas without presenting that as an elevation measurement. Regional source activation probes real data within the raster. Existing projection, NoData, unit and scale/offset validation are retained.
- GEBCO 2026 numeric data uses the Australian Antarctic Division Source Cooperative GeoTIFF mirror. HTTPS certificate verification remains enabled; the OpenSSL CA bundle generated from Windows trusted roots does not enforce every Windows chain policy.
- Coordinate lines are built beyond the current view rectangle and are refreshed when movement leaves that buffered area; geographic meridians extend to the poles. UTM remains restricted to its supported latitude range. Labels follow the screen-centre ground target along their own fixed latitude or longitude, and fine automatic intervals use enough decimal precision to distinguish labels.
- Fresh default grid component settings enable meridians and parallels with automatic spacing; the master grid switch remains off by default. Previously saved user choices are not migrated. A fixed user interval may legitimately place the whole viewport between grid lines; set each interval to 0 for adaptive spacing.
- The Options window starts smaller and remains resizable, bounded by the owner and work area. The Map Grid preview places the illustrative bitmap and live overlays in one uniformly scaled coordinate space; it is an illustration, not a geographic data source.

## Evidence

- Release solution build: passed, no warnings/errors.
- `dotnet test ... -c Release --no-build`: 275 passed, 0 failed (T2).
- `node --test tests/*.test.cjs`: 103 passed, 0 failed, including buffered antimeridian/polar bounds, motion refresh, label anchoring, and fine-interval label precision (T1/T2).
- Real GDAL service probe on the user's global TIFF: configuration 0.58 s, root 0.06 s, 4160/4225 finite samples; its known eastern seam is handled separately for rendering. One regional tile returned 4225 finite samples. Read-only regional Sinai crop: root 1/4225 finite, partly covered tile 400/4225 finite, in-coverage tile 4225/4225 finite (T2, not globe-render acceptance).
- Live GEBCO mirror through application GDAL service: root 4225 finite samples, min -5938.8 m, max +4917.2 m in 15.89 s; z8 tile 4225 finite samples in 3.38 s (T2). No API key was used.
- In the previous turn, actual WebView2 accepted the user's global TIFF, and GEBCO Test Connection reported Connected. That did not verify visible 1x/3x relief.
- **T3 current changes NOT RUN:** auto-review rejected Computer Use because it had been stopped with Escape. No workaround was attempted. Regional on-globe navigation, actual grid behavior through pan/zoom/poles, responsive Options window, Arabic layout, and visual 1x/3x comparisons remain to be verified on Windows WebView2.
- **T4 NOT RUN:** no new canonical portable Preview was published. The source build output is available for local testing.
- Full `git diff --check` reported a pre-existing blank line at EOF in `scripts/build-and-test.ps1`; scoped diff check for this follow-up passed. An auxiliary diagnostic build emitted NU1900 because the NuGet vulnerability feed was unreachable; the actual solution build passed without warnings.

## Remaining limitations

Local terrain tiles use Web Mercator and therefore do not cover polar latitudes beyond approximately 85.05°. Sparse isolated valid pixels can be missed by the bounded activation probe; rasters without overviews can be slower at low zoom. Geographic reference grid itself was extended to the poles. No claim is made that every command or all user-reported application defects have been audited.
