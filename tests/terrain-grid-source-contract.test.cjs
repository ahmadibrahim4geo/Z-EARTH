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

test('grid uses dynamic independent intervals, antimeridian windows and tested zone geometry', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(js, /ZEarthGraticuleGeometry\.longitudeWindows\(bounds\)/);
  assert.match(js, /ZEarthGraticuleGeometry\.visibleUtmSegments\(bounds\)/);
  assert.match(js, /const candidates = \[0\.05, 0\.1, 0\.25/);
  assert.match(js, /clampToGround: true/);
  assert.match(js, /const showZoneLabels = this\.config\.showLabels/); // no permanent equator labels
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  assert.match(xaml, /x:Name="LongitudeIntervalInput"/);
  assert.match(xaml, /x:Name="LatitudeIntervalInput"/);
  assert.match(xaml, /x:Name="UtmZonesCheck"/);
  const code = read('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
  assert.match(code, /TryReadGridInterval/);
  assert.match(code, /gridAcknowledged/);
});

test('local UTM helper loads before Cesium engine in shipped HTML', () => {
  const html = read('src/ZEarth.Globe/Assets/index.html');
  const geometry = html.indexOf('src="graticule-geometry.js"');
  const cesium = html.indexOf('src="cesium-engine.js"');
  assert.ok(geometry > 0 && cesium > geometry);
});

test('camera-following graticule labels use Cesium PositionProperty and deduplicate overlaps', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.equal((js.match(/position: new Cesium\.CallbackPositionProperty\(/g) || []).length, 3);
  assert.match(js, /const regularOverlap = this\.config\.showLongitudeLines/);
  assert.doesNotMatch(js, /this\.addMeridian\(-180, primeColor/);
});

test('grid OFF gates all source primitives and pole labels respect the camera horizon', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const grid = js.split('const GraticuleManager = {')[1].split('// Compact Compliant Credits')[0];
  assert.match(grid, /this\\.dataSource\\.show = this\\.config\\.visible/);
  assert.match(grid, /isSurfaceAnchorVisible\\(/);
  assert.match(grid, /show: visibleFromCamera/);
  assert.equal((grid.match(/show: visibleFromCamera/g) || []).length, 2);
  assert.doesNotMatch(grid, /disableDepthTestDistance: Number\\.POSITIVE_INFINITY/);
});

test('Sunlight Now uses both the live UTC date and hour and holds manual clock', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  const env = js.split('const EnvironmentManager = {')[1].split('const SwipeManager = {')[0];
  assert.match(env, /now\\.toISOString\\(\\)\\.slice\\(0, 10\\)/);
  assert.match(env, /viewer\\.clock\\.shouldAnimate = false/);
});
