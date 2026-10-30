const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function managerFixture(fetchImpl) {
  const source = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
  const section = source.split('  const TerrainManager = {')[1]
    .split('\n  // 3. Initial Camera Scene')[0];
  assert.ok(section, 'TerrainManager source exists');
  const originalProvider = { name: 'ellipsoid' };
  const viewer = { terrainProvider: originalProvider, scene: {
    globe: {}, mode: 3, requestRender() {},
  }};
  const events = [];
  const Cesium = {
    CustomHeightmapTerrainProvider: function(options) { this.options = options; },
    EllipsoidTerrainProvider: function() {},
    WebMercatorTilingScheme: function() {},
    SceneMode: { SCENE3D: 3 }
  };
  const context = { viewer, Cesium, fetch: fetchImpl, sendHostMessage: event => events.push(event) };
  const manager = vm.runInNewContext('({' + section.trim().replace(/;\s*$/, '') + ')', context);
  return { manager, viewer, originalProvider, events };
}

test('terrain does not switch provider when the numeric root tile fails', async () => {
  const {manager, viewer, originalProvider, events} = managerFixture();
  manager.verifyViewportLoader = async () => ({sampleCount: 4225});
  await assert.rejects(manager.activateHeightmapProvider('localDem', 'local', 'credit',
    async (x, y, level) => { if (level === 0) throw new Error('missing root'); }), /missing root/);
  assert.equal(viewer.terrainProvider, originalProvider);
  assert.equal(events.length, 0);
});

for (const outcome of ['resolve', 'reject']) {
  test(`superseded GEBCO activation cannot overwrite newer ellipsoid on ${outcome}`, async () => {
    const {manager, viewer, events} = managerFixture();
    let resolveTile, rejectTile;
    const pending = new Promise((resolve, reject) => { resolveTile = resolve; rejectTile = reject; });
    manager.loadHostFloatTile = () => pending;
    manager.verifyViewportLoader = async () => ({sampleCount: 4225});
    const older = manager.setProvider('gebco2026');
    const latest = await manager.setProvider('ellipsoid');
    const selected = viewer.terrainProvider;
    if (outcome === 'resolve') resolveTile(new Float32Array(4225));
    else rejectTile(new Error('delayed network failure'));
    const result = await older;
    assert.equal(latest.success, true);
    assert.equal(result.superseded, true);
    assert.equal(manager.currentType, 'ellipsoid');
    assert.equal(viewer.terrainProvider, selected);
    assert.deepEqual(events.map(event => event.event), ['terrain.changed']);
  });
}

test('terrain switches only after numeric root and viewport probes succeed', async () => {
  const {manager, viewer, events} = managerFixture();
  const visited = [];
  manager.verifyViewportLoader = async loader => { await loader(4, 5, 8); return {sampleCount: 4225}; };
  await manager.activateHeightmapProvider('dem', 'numeric', 'credit',
    async (x, y, level) => { visited.push([x, y, level]); return new Float32Array(4225); });
  assert.deepEqual(visited, [[0, 0, 0], [4, 5, 8]]);
  assert.equal(manager.currentType, 'dem');
  assert.equal(events[0].event, 'terrain.changed');
  assert.equal(viewer.scene.verticalExaggeration, 1);
});

test('terrain proxy accepts preflight and avoids stale cached raster or token responses', () => {
  const host = fs.readFileSync('src/ZEarth.App/Views/MainWindow.xaml.cs', 'utf8');
  const script = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
  assert.match(host, /e\.Request\.Method, "OPTIONS"/);
  assert.match(host, /Access-Control-Allow-Origin: https:\/\/cesium\.local/);
  assert.match(host, /Access-Control-Allow-Headers: X-ZEarth-Provider-Token/);
  assert.match(host, /Cache-Control: no-store/);
  assert.match(script, /terrain\.local\/local-dem[\s\S]{0,140}cache: 'no-store'/);
  assert.match(script, /terrain\.local\/\$\{source\}[\s\S]{0,100}cache: 'no-store'/);
  const online = fs.readFileSync('src/ZEarth.App/Services/OnlineTerrainTileService.cs', 'utf8');
  const copernicus = online.split('GetCopernicusGlo30TileAsync')[1].split('GetGebco2026TileAsync')[0];
  assert.doesNotMatch(copernicus, /_cache|Store\(/);
});


test('regional raster activates using a coverage probe even if world root has no data', async () => {
  const {manager, viewer, events} = managerFixture(async () => ({
    ok: true, json: async () => ({probeLevel: 18, probeX: 1, probeY: 2})
  }));
  const raw = new Float32Array(4225).fill(NaN);
  const covered = new Float32Array(4225).fill(NaN);
  covered[12] = 372;
  manager.loadLocalRasterTile = async (x, y, level) => level === 0 ? raw : covered;
  const result = await manager.activateLocalRasterProvider();
  assert.equal(result.probe.sampleCount, 1);
  assert.equal(result.probe.minimumElevationM, 372);
  assert.match(result.label, /ellipsoid outside raster coverage/);
  assert.equal(events[0].event, 'terrain.changed');
  const render = await viewer.terrainProvider.options.callback(0, 0, 0);
  assert.equal(render[12], 0);
  assert.ok(Number.isNaN(raw[12]), 'rendering must not change raw coverage mask');
});

test('regional raster rejects missing probe data instead of pretending it is ready', async () => {
  const {manager, viewer, originalProvider} = managerFixture(async () => ({
    ok: true, json: async () => ({probeLevel: 18, probeX: 1, probeY: 2})
  }));
  manager.loadLocalRasterTile = async () => new Float32Array(4225).fill(NaN);
  await assert.rejects(manager.activateLocalRasterProvider(), /No numeric DEM samples/);
  assert.equal(viewer.terrainProvider, originalProvider);
});

test('local elevations use raw DEM samples and reject missing coverage or holes', async () => {
  const {manager} = managerFixture();
  const raw = new Float32Array(4225).fill(150);
  manager.loadLocalRasterTile = async () => raw;
  let points = await manager.sampleLocalRasterPoints([{longitude: 0, latitude: 0}]);
  assert.equal(points[0].height, 150);
  raw.fill(NaN);
  points = await manager.sampleLocalRasterPoints([{longitude: 0, latitude: 0}]);
  assert.ok(Number.isNaN(points[0].height), 'outside coverage is not zero elevation');
  raw.fill(150);
  raw[1] = NaN;
  points = await manager.sampleLocalRasterPoints([{longitude: 0, latitude: 0}]);
  assert.ok(Number.isNaN(points[0].height), 'interpolation touching a NoData hole is rejected');
  points = await manager.sampleLocalRasterPoints([{longitude: 0, latitude: 89}]);
  assert.ok(Number.isNaN(points[0].height), 'Web Mercator does not cover the poles');
});


test('local root wraps real western-meridian heights to the equivalent eastern edge', async () => {
  const {manager} = managerFixture();
  const raw = new Float32Array(4225).fill(25);
  for (let row = 0; row < 65; row++) {
    raw[row * 65] = 45 + row;
    raw[row * 65 + 64] = NaN;
  }
  manager.loadLocalRasterTile = async () => raw;
  const rendered = await manager.loadLocalRasterRenderTile(0, 0, 0);
  assert.equal(rendered[64], 45);
  assert.equal(rendered[64 + 64 * 65], 109);
  assert.ok(Number.isNaN(raw[64]), 'wrap must not change the raw source mask');
});
