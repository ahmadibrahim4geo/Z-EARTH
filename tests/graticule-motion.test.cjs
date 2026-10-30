const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const geometry = require('../src/ZEarth.Globe/Assets/graticule-geometry.js');

test('moving beyond buffered coverage schedules one refresh; hiding the grid cancels it', () => {
  const source = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
  const body = source.split('  const GraticuleManager = {')[1].split('\n  // Compact Compliant Credits')[0];
  const frames = [];
  let rect = {west: 10, east: 20, south: 20, north: 30};
  const context = {
    GRID_DEFAULT_COLORS: {}, Date: {now: () => 1000},
    window: {ZEarthGraticuleGeometry: geometry, requestAnimationFrame: cb => frames.push(cb)},
    viewer: {camera: {computeViewRectangle: () => rect}, scene: {globe: {ellipsoid: {}}}},
    Cesium: {Math: {toDegrees: x => x}}
  };
  const manager = vm.runInNewContext('({' + body.trim().replace(/;\s*$/, '') + ')', context);
  manager.config.visible = true;
  manager.lineCoverage = geometry.expandViewBounds(rect);
  let refreshes = 0;
  manager.refreshForCamera = () => refreshes++;
  manager.refreshWhileMoving();
  assert.equal(frames.length, 0, 'movement within buffered coverage should not rebuild');
  rect = {west: 35, east: 45, south: 20, north: 30};
  manager.lastMotionCheck = 0;
  manager.refreshWhileMoving();
  manager.lastMotionCheck = 0;
  manager.refreshWhileMoving();
  assert.equal(frames.length, 1, 'repeated frames must not queue duplicate rebuilds');
  frames.shift()();
  assert.equal(refreshes, 1);
  manager.lastMotionCheck = 0;
  manager.refreshWhileMoving();
  manager.config.visible = false;
  frames.shift()();
  assert.equal(refreshes, 1, 'a pending callback must respect grid OFF');
});

test('label centre follows the screen ground target rather than an asymmetric view rectangle', () => {
  const source = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
  const body = source.split('  const GraticuleManager = {')[1].split('\n  // Compact Compliant Credits')[0];
  let ground = {valid: true, longitude: 179, latitude: 87};
  const context = { GRID_DEFAULT_COLORS: {}, getScreenCenterGround: () => ground,
    viewer: {camera: {}, scene: {}}, Cesium: {Math: {toDegrees: x => x}} };
  const manager = vm.runInNewContext('({' + body.trim().replace(/;\s*$/, '') + ')', context);
  manager.updateLabelViewCenter();
  assert.equal(manager.labelViewCenter.lon, 179);
  assert.equal(manager.labelViewCenter.lat, 87);
  ground = {valid: true, longitude: -179, latitude: 88};
  manager.updateLabelViewCenter();
  assert.equal(manager.labelViewCenter.lon, -179);
  assert.equal(manager.labelViewCenter.lat, 88);
});
