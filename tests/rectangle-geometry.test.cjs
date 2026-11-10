const test = require('node:test');
const assert = require('node:assert/strict');
const {
  rectangleCorners,
  computeOrientedRectangle,
  calculateBearing,
  calculateEllipsePerimeter,
  sampleCircle,
  sampleEllipse
} = require('../src/ZEarth.Globe/Assets/rectangle-geometry.js');

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

test('calculateBearing returns accurate forward geodesic azimuth [0, 360)', () => {
  assert.equal(calculateBearing(0, 0, 0, 10), 0);
  assert.equal(calculateBearing(0, 0, 10, 0), 90);
  assert.equal(calculateBearing(0, 0, 0, -10), 180);
  assert.equal(calculateBearing(0, 0, -10, 0), 270);
  assert.equal(calculateBearing(30, 30, 30, 30), 0);
  const ne = calculateBearing(0, 0, 10, 10);
  assert.ok(ne > 44.0 && ne < 46.0);
});

test('calculateEllipsePerimeter calculates exact circle and Ramanujan ellipse perimeter', () => {
  const circlePerim = calculateEllipsePerimeter(100, 100);
  assert.ok(Math.abs(circlePerim - 2 * Math.PI * 100) < 1e-6);

  const ellipsePerim = calculateEllipsePerimeter(100, 50);
  // Ramanujan approx for 100, 50 is ~484.42 m
  assert.ok(ellipsePerim > 480 && ellipsePerim < 490);

  assert.equal(calculateEllipsePerimeter(0, 0), 0);
  assert.equal(calculateEllipsePerimeter(100, 0), 400);
});

test('sampleCircle generates valid closed ring coordinates around center', () => {
  const coords = sampleCircle(31.23, 30.04, 1000, 16);
  assert.equal(coords.length, 32); // 16 points * 2 (lon, lat)
  assert.ok(coords.every(Number.isFinite));

  // Point 0 should be due North (latitude higher than center, longitude approx center)
  assert.ok(coords[1] > 30.04);
  assert.ok(Math.abs(coords[0] - 31.23) < 1e-4);

  // Point 4 (at index 8, 9) should be due East
  assert.ok(coords[8] > 31.23);
  assert.ok(Math.abs(coords[9] - 30.04) < 1e-4);
});

test('sampleEllipse respects major/minor axes and bearing rotation', () => {
  // Semi-major 1000m North-South (bearing 0), semi-minor 500m East-West
  const coords = sampleEllipse(31.23, 30.04, 1000, 500, 0, 16);
  assert.equal(coords.length, 32);
  assert.ok(coords.every(Number.isFinite));

  // Semi-major at t=0 (North): deltaLat should be approx 1000m in degrees (~0.00899)
  const dLatMajor = coords[1] - 30.04;
  assert.ok(dLatMajor > 0.008 && dLatMajor < 0.010);

  // Semi-minor at t=pi/2 (index 8, 9, East): deltaLon should be approx 500m in degrees (~0.00519)
  const dLonMinor = coords[8] - 31.23;
  assert.ok(dLonMinor > 0.004 && dLonMinor < 0.006);
});

test('computeOrientedRectangle calculates exact dimensions and 4 corners for 3 points', () => {
  // P1=(31.0, 30.0), P2=(31.0, 30.01) -> due North (bearing 0), length ~1111.95m
  // P3=(31.01, 30.01) -> East perpendicular, width ~962.8m
  const rect = computeOrientedRectangle(31.0, 30.0, 31.0, 30.01, 31.01, 30.01);
  assert.ok(rect !== null);
  assert.equal(rect.corners.length, 8);
  assert.ok(rect.length > 1100 && rect.length < 1120);
  assert.ok(rect.width > 950 && rect.width < 975);
  assert.equal(Math.round(rect.bearing), 0);
  assert.ok(rect.area > 1000000);
  assert.ok(rect.perimeter > 4000);

  // Corner 1 should be P1
  assert.equal(rect.corners[0], 31.0);
  assert.equal(rect.corners[1], 30.0);
  // Corner 2 should be P2
  assert.equal(rect.corners[2], 31.0);
  assert.equal(rect.corners[3], 30.01);
});

test('computeOrientedRectangle handles arbitrary angled baseline', () => {
  // 45 degree baseline
  const rect = computeOrientedRectangle(0, 0, 0.01, 0.01, 0.0, 0.02);
  assert.ok(rect !== null);
  assert.ok(rect.bearing > 44 && rect.bearing < 46);
  assert.ok(rect.length > 0);
  assert.ok(rect.width > 0);
});

test('computeOrientedRectangle rejects degenerate baseline (P1 == P2)', () => {
  const degenerate = computeOrientedRectangle(30, 30, 30, 30, 31, 31);
  assert.equal(degenerate, null);
});
