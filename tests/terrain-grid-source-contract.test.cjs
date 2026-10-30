const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const read = (path) => fs.readFileSync(path, 'utf8');

test('terrain requires live numeric root and viewport tiles before reporting readiness', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(js, /this\.loadTerrariumTile\(0, 0, 0\)/);
  assert.match(js, /this\.verifyViewportTerrainTile\(\)/);
  assert.match(js, /sampleCount: valid/);
  assert.match(js, /minimumElevationM, maximumElevationM/);
  assert.match(js, /viewer\.scene\.verticalExaggeration = f/);
  assert.match(js, /const \[, probe\] = await Promise\.all\(/);
  const bridge = read('src/ZEarth.Globe/GlobeBridge.cs');
  assert.match(bridge, /terrain\.setProvider[^\n]+timeoutMs: 45000/);
});

test('independent free DEM providers do not masquerade as Cesium ion assets', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const options = read('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
  const online = read('src/ZEarth.App/Services/OnlineTerrainTileService.cs');
  assert.match(js, /JAXA\.EORC_ALOS\.PRISM_AW3D30\.v3\.2_global/);
  assert.match(js, /new je\.TileGenerator/);
  assert.match(js, /type === 'copernicusGlo30'/);
  assert.match(js, /type === 'gebco2026'/);
  assert.match(js, /type === 'srtm30'/);
  assert.match(online, /COPERNICUS_30/);
  assert.match(online, /sh\.dataspace\.copernicus\.eu\/process\/v1/);
  assert.match(online, /source\.coop/);
  assert.match(js, /Australian Antarctic Division \/ Source Cooperative/);
  assert.match(options, /does not use a Cesium ion token/);
  assert.match(options, /JAXA Earth API/);
});

test('grid uses dynamic independent intervals, antimeridian windows and tested zone geometry', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(js, /ZEarthGraticuleGeometry\.longitudeWindows\(bounds\)/);
  assert.match(js, /ZEarthGraticuleGeometry\.visibleUtmSegments\(lineBounds\)/);
  assert.match(js, /0\.000001, 0\.000002, 0\.000005/);
  // Cesium drops ground-clamped polylines in 2D, which made the whole grid
  // disappear when switching to 2D. The grid must clamp only in 3D.
  assert.match(js, /clampToGround: this\.groundClamp\(\)/);
  assert.match(js, /return viewer\.scene\.mode === Cesium\.SceneMode\.SCENE3D/);
  // Labels follow each enabled component automatically; no general label switch
  // may suppress them, and no permanent equator-only label block remains.
  assert.doesNotMatch(js, /const showZoneLabels = this\.config\.showLabels/);
  assert.match(js, /if \(this\.config\.showLongitudeLines\)/);
  assert.match(js, /if \(this\.config\.showLatitudeLines\)/);
  assert.match(js, /if \(this\.config\.showKeyParallels\)/);
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  assert.match(xaml, /x:Name="LongitudeIntervalInput"/);
  assert.match(xaml, /x:Name="LatitudeIntervalInput"/);
  assert.match(xaml, /x:Name="UtmZonesCheck"/);
  assert.match(xaml, /x:Name="MajorColorPicker"/);
  assert.match(xaml, /x:Name="LongitudeColorPicker"/);
  assert.match(xaml, /x:Name="LatitudeColorPicker"/);
  assert.match(xaml, /x:Name="LabelColorPicker"/);
  assert.match(xaml, /x:Name="GridLabelSizeSlider"/);
  assert.match(xaml, /x:Name="GridStatePill"/);
  assert.doesNotMatch(xaml, /GraticuleLabelsCheck/);
  const code = read('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
  assert.match(code, /TryReadGridInterval/);
  assert.match(code, /gridAcknowledged/);
  assert.match(code, /OnResetGridDefaultsClick/);
});

test('grid defaults stay aligned across settings, bridge, Cesium and Options UI', () => {
  const defaults = {
    gridMajorColor: '#FF5252',
    gridLongitudeColor: '#3BA9FF',
    gridLatitudeColor: '#3AD17B',
    gridLabelColor: '#E8E8E8',
    gridUtmColor: '#FFC107'
  };
  const settings = read('src/ZEarth.App/Services/GridSettingsDefaults.cs');
  const bridge = read('src/ZEarth.Globe/GlobeBridge.cs');
  const engine = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');

  for (const [name, value] of Object.entries(defaults)) {
    const csharpName = name[0].toUpperCase() + name.slice(1);
    assert.match(settings, new RegExp(`const string ${csharpName} = "${value}"`));
    assert.match(bridge, new RegExp(`string ${name} = "${value}"`));
    assert.match(engine, new RegExp(`${name}: '${value}'`));
  }
  for (const value of Object.values(defaults)) {
    assert.match(xaml, new RegExp(`ColorHex="${value}"`));
  }
});

test('UTM labels are anchored inside the zone, never on a boundary meridian', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(js, /visibleUtmZoneLabels\(bounds\)/);
  assert.match(js, /zoneLabel\.longitude/);
  // Boundary drawing and label drawing must be separate code paths.
  const grid = js.split('const GraticuleManager = {')[1].split('// Compact Compliant Credits')[0];
  const boundaryIndex = grid.indexOf('visibleUtmSegments(lineBounds)');
  const labelIndex = grid.indexOf('visibleUtmZoneLabels(bounds)');
  assert.ok(boundaryIndex > 0 && labelIndex > boundaryIndex);
});

test('local UTM helper loads before Cesium engine in shipped HTML', () => {
  const html = read('src/ZEarth.Globe/Assets/index.html');
  const geometry = html.indexOf('src="graticule-geometry.js"');
  const cesium = html.indexOf('src="cesium-engine.js"');
  assert.ok(geometry > 0 && cesium > geometry);
});

test('graticule labels slide with the camera while preserving their geographic line', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  // A meridian's longitude and a parallel's latitude remain fixed; only the
  // coordinate along that line follows the geographic camera centre.
  assert.match(js, /const anchor = Cesium\.Cartesian3\.fromDegrees\(longitude, latitude\)/);
  assert.match(js, /movingLabelCoordinates\(/);
  assert.match(js, /CallbackPositionProperty\(positionForView, false\)/);
  assert.match(js, /axis: 'parallel'/);
  assert.match(js, /axis: 'meridian'/);
  assert.match(js, /showBackground: false/);
  assert.match(js, /scene\.preRender\.addEventListener/);
  assert.match(js, /GraticuleManager\.refreshForCamera\(\)/);
  assert.match(js, /new ResizeObserver/);
  assert.match(js, /viewer\.canvas\.clientWidth.*viewer\.canvas\.clientHeight/);
  assert.match(js, /const regularOverlap = this\.config\.showLongitudeLines/);
  assert.doesNotMatch(js, /this\.addMeridian\(-180, primeColor/);
});

test('2D scene mode keeps grid lines, labels and edge-safe anchors', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  // Cesium's ground-polyline primitive is 3D-only: in 2D the clamped polyline is
  // dropped and the whole grid disappeared. It must clamp only in 3D.
  assert.match(js, /clampToGround: this\.groundClamp\(\)/);
  assert.match(js, /return viewer\.scene\.mode === Cesium\.SceneMode\.SCENE3D/);
  // The spherical horizon test hid every off-centre label on the 2D plane.
  assert.match(js, /surfaceVisible\(anchor, normal\)/);
  assert.match(js, /if \(viewer\.scene\.mode !== Cesium\.SceneMode\.SCENE3D\) return true;/);
  // A label anchored near a viewport edge flips its origin instead of clipping.
  assert.match(js, /projectToScreen\(anchor\)/);
  assert.match(js, /worldToWindowCoordinates \|\| transforms\.wgs84ToWindowCoordinates/);
  assert.match(js, /horizontalOrigin: horizontalOrigin/);
  // A label whose anchor is outside the canvas is skipped, not half-drawn.
  assert.match(js, /screen\.x < 0 \|\| screen\.x > canvasWidth/);
  // Labels are spaced in pixels, so a 5° setting cannot produce a dense wall of
  // text at world scale, and edge-adjacent labels are skipped.
  assert.match(js, /const lonLabelStep = Math\.max\(1,/);
  assert.match(js, /const latLabelStep = Math\.max\(1,/);
  assert.match(js, /const edgePadLon = Math\.max\(lonInterval, lonSpan \* 0\.14\)/);
});

test('global grid visibility is a master gate that preserves component settings', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const grid = js.split('const GraticuleManager = {')[1].split('// Compact Compliant Credits')[0];
  // Turning the grid off must remove every primitive without rewriting the
  // component flags, so switching it back on restores the user's choices.
  assert.match(grid, /if \(!this\.config\.visible\) \{/);
  assert.match(grid, /this\.dataSource\.entities\.removeAll\(\);/);
  for (const flag of ['showKeyParallels', 'showLongitudeLines',
    'showLatitudeLines', 'showUtmZones']) {
    assert.doesNotMatch(grid, new RegExp('this\\.config\\.' + flag + '\\s*=\\s*false'),
      `Hiding the grid must not force ${flag} off.`);
  }
});

test('major reference labels honour the configured coordinate format', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const grid = js.split('const GraticuleManager = {')[1].split('// Compact Compliant Credits')[0];
  // These labels used to be hardcoded strings such as 'Tropic of Cancer 23.44° N',
  // so switching to DM/DMS/Degrees left them contradicting the chosen format.
  assert.doesNotMatch(grid, /'Tropic of Cancer 23\.44/);
  assert.doesNotMatch(grid, /'Arctic Circle 66\.56/);
  assert.doesNotMatch(grid, /'Equator 0°/);
  assert.match(grid, /const coordinate = \(value, axis\) =>/);
  assert.match(grid, /ZEarthGraticuleLabels\.formatDegrees\(value, axis, format\)/);
  for (const name of ['Equator', 'Tropic of Cancer', 'Tropic of Capricorn',
    'Arctic Circle', 'Antarctic Circle', 'Greenwich / Prime Meridian',
    'International Date Line', 'North Pole', 'South Pole', 'Grid Origin']) {
    assert.ok(grid.includes("'" + name + " '"), `Missing formatted label for ${name}`);
  }
});

test('grid OFF gates all source primitives and pole labels respect the camera horizon', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const grid = js.split('const GraticuleManager = {')[1].split('// Compact Compliant Credits')[0];
  assert.match(grid, /this\.dataSource\.show = this\.config\.visible/);
  assert.match(grid, /isSurfaceAnchorVisible\(/);
  assert.match(grid, /show: visibleFromCamera/);
  assert.equal((grid.match(/show: visibleFromCamera/g) || []).length, 2);
  // Depth testing must never be disabled unconditionally: in 3D that would draw
  // labels through the globe. It is disabled only for the flat 2D projection,
  // where labels and grid lines are coplanar and would otherwise z-fight.
  assert.doesNotMatch(grid, /disableDepthTestDistance:\s*Number\.POSITIVE_INFINITY/);
  assert.match(grid,
    /const depthTestDisabled = viewer\.scene\.mode === Cesium\.SceneMode\.SCENE3D\s*\?\s*0\s*:\s*Number\.POSITIVE_INFINITY/);
});

test('Sunlight Now uses both the live UTC date and hour and holds manual clock', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const env = js.split('const EnvironmentManager = {')[1].split('const SwipeManager = {')[0];
  assert.match(env, /now\.toISOString\(\)\.slice\(0, 10\)/);
  assert.match(env, /viewer\.clock\.shouldAnimate = false/);
});

test('Sunlight Now synchronizes UTC date and real-time intent all the way to WPF', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const bridge = read('src/ZEarth.Globe/GlobeBridge.cs');
  const viewModel = read('src/ZEarth.App/ViewModels/MainViewModel.cs');
  assert.match(js, /dateUtc: this\.dateUtc, isRealTime/);
  assert.match(bridge, /TimeContextChanged\?\.Invoke\(this, json\)/);
  assert.match(viewModel, /_globeBridge\.TimeContextChanged \+=/);
  assert.match(viewModel, /if \(isRealTime\) _sunlightTimeCustomized = false/);
});
