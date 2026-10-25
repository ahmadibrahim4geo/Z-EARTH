const test = require('node:test');
const assert = require('node:assert/strict');
const { rectangleCorners } = require('../src/ZEarth.Globe/Assets/rectangle-geometry.js');

test('rectangle opposite corners normalize to consistent four-corner polygon', () => {
  assert.deepEqual(rectangleCorners(32, 30, 30, 25), [
    30, 25, 32, 25, 32, 30, 30, 30
  ]);
  assert.deepEqual(rectangleCorners(30, 25, 32, 30), [
    30, 25, 32, 25, 32, 30, 30, 30
  ]);
});

test('reject degenerate rectangles and invalid world coordinates', () => {
  for (const args of [
    [32, 30, 32, 31], [32, 30, 33, 30], [200, 30, 31, 29],
    [31, 91, 30, 25], [NaN, 20, 30, 30], [Infinity, 20, 30, 30],
    [179, 10, -179, 11]
  ]) assert.equal(rectangleCorners(...args), null);
});

test('southern and western hemisphere rectangles preserve signed coordinates', () => {
  assert.deepEqual(rectangleCorners(-75, -10, -70, -15), [
    -75, -15, -70, -15, -70, -10, -75, -10
  ]);
});

test('geometry is deliberately WGS84 lon/lat rectangle, not an area/distance estimate', () => {
  const vals = rectangleCorners(30, 29, 30.5, 29.4);
  assert.equal(vals.length, 8);
  assert.ok(vals.every(Number.isFinite));
});
