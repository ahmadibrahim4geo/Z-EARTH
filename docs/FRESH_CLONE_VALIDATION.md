# Z EARTH — Clean checkout & functional acceptance

## Test tiers
- **T0 source**: documented code path; no execution.
- **T1 static**: XML parsing and JS syntax, secrets/static checks.
- **T2**: .NET restore/build and unit/integration tests, including negative bridge responses; a FakeGlobeEngine always saying success is not real Cesium.
- **T3**: *real Windows WebView2 + GPU* verification with pictures, logs and loaded map; blocked on unsupported environments.
- **T4**: packaged executable verified on a separate clean Windows profile/device.

## Windows checklist (record pass/fail and evidence)
- [ ] Fresh clone, `dotnet restore`, `dotnet build -c Release` and `dotnet test` all succeed.
- [ ] Exact one Preview executable `dist/Z EARTH-Preview/Z EARTH.exe` starts without crashes and shows actual Cesium tiles/offline grid.
- [ ] Sidebar left/right/collapsed, ribbon shortcuts, theme and English/Arabic labels don't break layout.
- [ ] Camera Home, Extent, Reset North, Zoom In/Out and history visibly modify globe.
- [ ] 2D ↔ 3D transitions retain globe and state; test repeated switches.
- [ ] Sunlight button changes rendered scene and stays in sync with acknowledgement; time slider and Atmosphere separately verified.
- [ ] Offline basemap works without Internet; online basemap tiles load only after provider response, legal attribution visible; bad provider fails visibly.
- [ ] Terrain activation shows **actual relief**, not only enabled button; tile failure falls back visibly to ellipsoid.
- [ ] Import representative KML/KMZ (point, line, polygon, nested folders); export and reopen without losing coordinates/attributes.
- [ ] Project save, reopen, auto-save/recovery preserve camera and Places/Layer settings.
- [ ] CRS EPSG 4326/3857/UTM Egypt transformations use valid axis order and units; check against trusted baseline.
- [ ] No personal tokens in tracked files, logs, screenshots or persisted plaintext settings.
- [ ] Screenshot, log, Windows version, GPU/WebView2 version, failure evidence attached.

Current source-level review cannot claim any T3/T4 item already passed. Preserve one preview folder and avoid manufacturing screenshots or success logs.
