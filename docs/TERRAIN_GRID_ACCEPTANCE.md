# Z EARTH — Terrain and Geographic Grid native acceptance

Source implementation and CI are **not** a confirmation that the user's Windows globe draws the requested features. Run this on the one canonical Windows portable Preview, using a fresh profile and GPU/WebView2. Keep negative results as open defects.

## Terrain model and vertical exaggeration

1. Open at a mountainous area with known elevation, e.g. Sinai or the Alps. Note active provider name, network access, coordinate and camera altitude.
2. Enable the **AWS Terrarium DEM**. Record the HTTP status of actual \`terrain.local\` and S3 elevation tile requests. Both root tile and a geographic tile around the viewport must decode to complete numeric height grids. Capture the returned probe level/x/y, sample count and min/max elevation.
3. Confirm the visible terrain surface (not merely an ON indicator). Compare 1.0x, 3.0x and Reset 1.0x at the same camera, heading and tilt. Record screenshots and response/failure log. Flat terrain at one location is not proof of failure; retry a rugged location.
4. Toggle Terrain OFF and confirm that 3D relief disappears on ellipsoid, and that selecting a multiplier reports **pending DEM**, not confirmed relief.
5. Test no internet, invalid DEM path, missing Cesium ion token, and (with a user-supplied legitimate token) authorized ion terrain. Failure must display the true fallback without a false green ON indicator.
6. Switch to 2D, then back to 3D, save/reopen the project, and verify the selected provider/factor and rendering again. A numerical provider probe does not replace this GPU acceptance.

## Geographic grid and UTM

1. Grid OFF → ON; independently toggle **major features**: Equator, Tropics, Polar Circles, Greenwich, International Date Line, 0°/0° origin and both Poles. Confirm that labels obey the **Show labels** setting.
2. Enable meridians alone, enter 5°, then 2.5°, then Auto 0. Repeat separately for parallels. Invalid or negative intervals must be rejected without saving as a valid setting.
3. At Egyptian latitudes around 27°N/31°E, show UTM zone boundaries and label **UTM 36N** in the visible viewport. Inspect border between zones 35N and 36N, then zoom, pan, and rotate.
4. Repeat for southern-hemisphere latitudes, 180° date line, zone 1/60, Norway 32V, and Svalbard enlarged 31X/33X/35X/37X. UTM standard grid applies only from 80°S to 84°N; do not imply it extends to the poles.
5. Test labels above active 3D mountains, all relevant zoom scales, 2D/3D, Dark/Light, English/Arabic, fullscreen and a restarted application. Verify readable names, no hidden/double lines and no severe label collisions.
6. Network-off grid should still work on ellipsoid. Save/reopen all grid options and verify each remains independent of the status-bar coordinate format.

## Evidence levels and sign-off

- **T0:** code route inspected: XAML → ViewModel → GlobeBridge → Cesium → true render.
- **T1:** JavaScript and XAML syntax, static assets and wiring.
- **T2:** Node geometry tests, .NET tests, negative responses and Windows CI build.
- **T3:** actual Windows WebView2 GPU, numeric raster traffic, screenshots and logging.
- **T4:** clean installed/extracted **single** portable Preview on an independent Windows profile.

If only T0–T2 are available, report **T3/T4 pending**. Never say the DEM or grid is fully working based on a passing fake globe or green GitHub workflow.
