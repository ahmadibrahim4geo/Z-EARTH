const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..', 'src', 'ZEarth.Globe', 'Assets');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const js = fs.readFileSync(path.join(root, 'cesium-engine.js'), 'utf8');
const css = fs.readFileSync(path.join(root, 'globe.css'), 'utf8');

test('measurement panel has one header-only drag target', () => {
  assert.equal((html.match(/id="measure-drag-handle"/g)||[]).length, 1);
  assert.ok(js.includes("enableMovableMeasurementHud"));
  assert.ok(js.includes("setPointerCapture(event.pointerId)"));
  assert.ok(js.includes("event.target.closest('select, button, input')"));
});

test('measurement clicks display centered numbers inside visible points', () => {
  const start = js.indexOf("const MeasurementManager =");
  const end = js.indexOf("function pickGroundAtScreenPosition", start);
  const body = js.slice(start, end);
  assert.ok(body.includes("text: String(this.vertices.length)"));
  assert.ok(body.includes("pixelSize: 22"));
  assert.ok(body.includes("horizontalOrigin: Cesium.HorizontalOrigin.CENTER"));
  assert.ok(body.includes("pixelOffset: new Cesium.Cartesian2(0, 0)"));
});

test('measurement panel respects dark neutral shell', () => {
  assert.ok(css.includes("rgba(160, 164, 170, 0.7)"));
  assert.ok(css.includes("touch-action: none"));
});
