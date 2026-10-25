const test = require('node:test');
const assert = require('node:assert/strict');
const {formatDegrees} = require('../src/ZEarth.Globe/Assets/graticule-labels.js');

test('supports geographic DD, DMS, DM and whole-degree grid labels', () => {
  assert.equal(formatDegrees(-30.5, 'lat', 'DD'), '30.5000° S');
  assert.equal(formatDegrees(31.5, 'lon', 'DD'), '31.5000° E');
  assert.equal(formatDegrees(0, 'lon', 'Degrees'), '0°');
  assert.equal(formatDegrees(23.5, 'lat', 'DM'), '23° 30.000′ N');
  assert.equal(formatDegrees(-23.5, 'lat', 'DMS'), '23° 30′ 0.0″ S');
  assert.equal(formatDegrees(-0.0001, 'lon', 'DMS'), '0° 0′ 0.4″ W');
});
test('invalid or unsupported axis values do not produce plausible labels', () => {
  assert.equal(formatDegrees(NaN, 'lat', 'DD'), '');
  assert.equal(formatDegrees(20, 'depth', 'DMS'), '');
  assert.equal(formatDegrees(Infinity, 'lon', 'DM'), '');
});
test('rounds near minute/second boundaries without 60-second labels', () => {
  assert.equal(formatDegrees(10.999999, 'lon', 'DMS'), '11° 0′ 0.0″ E');
  assert.equal(formatDegrees(10.999999, 'lon', 'DM'), '11° 0.000′ E');
});
