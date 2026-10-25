const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const read = (path) => fs.readFileSync(path, 'utf8');

test('terrain requires live numeric root and viewport tiles before reporting readiness', () => {
  const js = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(js, /await this\.loadTerrariumTile\(0, 0, 0\)/);
  assert.match(js, /await this\.verifyViewportTerrainTile\(\)/);
  assert.match(js, /sampleCount: valid/);
  assert.match(js, /minimumElevationM, maximumElevationM/);
  assert.match(js, /viewer\.scene\.verticalExaggeration = f/);
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
