const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname, '../src/ZEarth.Globe/Assets/cloud-layer.js'), 'utf8');
const context = { window: {} };
vm.runInNewContext(code, context);
let addCount = 0, removeAllCount = 0, renderCount = 0;
let mode = 3;
const morphCallbacks = [];
class CloudCollection {
  constructor() { this.length = 0; this.show = true; }
  add(value) { this.length++; assert.ok(Number.isFinite(value.position.lon)); }
  removeAll() { removeAllCount++; this.length = 0; }
}
const Cesium = {
  CloudCollection, SceneMode: { SCENE3D: 3 },
  Cartesian2: class { constructor(x,y) { this.x=x;this.y=y; } },
  Cartesian3: Object.assign(class { constructor(x,y,z) { this.x=x;this.y=y;this.z=z; } }, { fromDegrees: (lon,lat,h)=>({lon,lat,h}) }),
  Math: { toDegrees: x=>x }
};
const scene = {
  get mode() { return mode; },
  primitives: { add(x) { addCount++; return x; } },
  requestRender() { renderCount++; },
  morphComplete: { addEventListener: f=>morphCallbacks.push(f) }
};
const moveCallbacks = [];
const camera = {
  positionCartographic: { longitude:31, latitude:30 },
  moveEnd: { addEventListener:f=>moveCallbacks.push(f) }
};
const model = context.window.ZEarthCloudLayer.create({ Cesium, viewer: { scene, camera } });
assert.equal(model.getState().enabled, false);
assert.equal(model.setEnabled(true).count, 64);
assert.equal(addCount, 1, 'collection is reused');
assert.equal(model.setEnabled(true).count, 64);
assert.equal(addCount, 1, 'duplicate collection forbidden');
moveCallbacks[0]();
assert.equal(addCount, 1);
mode=2; morphCallbacks[0]();
assert.equal(model.getState().rendered, false);
mode=3; morphCallbacks[0]();
assert.equal(model.getState().count, 64);
assert.equal(model.getState().rendered, true);
assert.equal(model.setEnabled(false).count, 0);
assert.equal(model.getState().enabled, false);
assert.ok(removeAllCount >= 1);
assert.ok(renderCount > 0);
console.log('Cloud layer mock PASS: idempotence / 2D / 3D / disable');
