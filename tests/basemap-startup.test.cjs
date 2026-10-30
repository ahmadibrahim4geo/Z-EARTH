const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
const start = source.indexOf('  const ImageryManager = {');
const end = source.indexOf('\n  // NOTE: ImageryManager.init()', start);
assert.ok(start >= 0 && end > start, 'ImageryManager source must be present');

function makeManager() {
  const messages = [];
  class Provider { constructor() { this.errorEvent = {addEventListener() {}}; } }
  const layers = [];
  const context = {
    Cesium: {
      UrlTemplateImageryProvider: Provider,
      GridImageryProvider: Provider,
      Color: class {},
      buildModuleUrl: () => 'offline',
      TileMapServiceImageryProvider: {fromUrl: () => new Promise(() => {})}
    },
    viewer: {imageryLayers: {
      addImageryProvider(provider) { const layer = {provider, show: true}; layers.push(layer); return layer; },
      raiseToTop() {}, remove() {}
    }},
    CreditsManager: {updateActiveBadges() {}},
    sendHostMessage: msg => messages.push(msg),
    console: {warn() {}}
  };
  vm.runInNewContext(source.slice(start, end).replace('const ImageryManager =', 'globalThis.ImageryManager ='), context);
  return {manager: context.ImageryManager, messages};
}

test('provisional offline initialization cannot replace the persisted startup selection', () => {
  const {manager, messages} = makeManager();
  manager.init();
  assert.equal(manager.activeBasemap, 'offline-vector');
  assert.equal(messages.some(m => m.event === 'imagery.basemapChanged'), false);
  assert.equal(manager.setBasemap('esri-hybrid'), true);
  assert.equal(manager.activeBasemap, 'esri-hybrid');
  assert.equal(manager.baseLayers['esri-imagery'].layer.show, true);
  assert.equal(manager.hybridLabelsLayer.show, true);
  assert.equal(messages.filter(m => m.event === 'imagery.basemapChanged').at(-1).basemap, 'esri-hybrid');
});

test('unknown basemap is rejected without changing the active layer', () => {
  const {manager, messages} = makeManager();
  manager.init();
  manager.setBasemap('esri-hybrid');
  assert.equal(manager.setBasemap('nonexistent-provider'), false);
  assert.equal(manager.activeBasemap, 'esri-hybrid');
  assert.equal(manager.baseLayers['esri-imagery'].layer.show, true);
  assert.equal(messages.filter(m => m.event === 'imagery.basemapChanged').length, 1);
});

test('bridge sends a failed response for a rejected basemap', () => {
  const section = source.split("case 'layers.setBasemap':")[1].split("case 'historical.loading':")[0];
  assert.match(section, /if \(ImageryManager\.setBasemap\(mode\)\) respondSuccess/);
  assert.match(section, /else respond\('failed'/);
});
