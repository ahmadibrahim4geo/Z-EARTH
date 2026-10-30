const test = require('node:test');
const assert = require('node:assert/strict');
const {formatDegrees, normalizeFormat, formats} =
  require('../src/ZEarth.Globe/Assets/graticule-labels.js');

test('fine automatic grid intervals retain distinct coordinate labels', () => {
  for (const format of ['DD', 'DM', 'DMS']) {
    const a = formatDegrees(31.123456, 'lon', format, 0.000001);
    const b = formatDegrees(31.123457, 'lon', format, 0.000001);
    assert.notEqual(a, b, `${format} must distinguish neighboring grid lines`);
  }
});

test('supports geographic DD, DMS, DM and whole-degree grid labels', () => {
  assert.equal(formatDegrees(-30.5, 'lat', 'DD'), '30.5° S');
  assert.equal(formatDegrees(31.5, 'lon', 'DD'), '31.5° E');
  assert.equal(formatDegrees(0, 'lon', 'Degrees'), '0°');
  assert.equal(formatDegrees(23.5, 'lat', 'DM'), '23° 30′ N');
  assert.equal(formatDegrees(-23.5, 'lat', 'DMS'), '23° 30′ S');
  assert.equal(formatDegrees(-0.0001, 'lon', 'DMS'), '0° 0′ 0.4″ W');
  assert.equal(formatDegrees(10.508333, 'lat', 'DMS'), '10° 30′ 30″ N');
});

test('trailing zeros are trimmed and whole degrees stay clean in every format', () => {
  assert.equal(formatDegrees(10, 'lat', 'DD'), '10° N');
  assert.equal(formatDegrees(10, 'lat', 'DM'), '10° N');
  assert.equal(formatDegrees(10, 'lat', 'DMS'), '10° N');
  assert.equal(formatDegrees(10, 'lat', 'Degrees'), '10° N');
  assert.equal(formatDegrees(10.5, 'lat', 'DD'), '10.5° N');
  assert.equal(formatDegrees(10.25, 'lon', 'DM'), '10° 15′ E');
  assert.equal(formatDegrees(10.25, 'lon', 'DMS'), '10° 15′ E');
  // No label may contain a redundant zero minute/second group.
  for (const format of formats) {
    assert.doesNotMatch(formatDegrees(12, 'lon', format), /00[′″]|\.0+°/);
  }
});

test('invalid or unsupported axis values do not produce plausible labels', () => {
  assert.equal(formatDegrees(NaN, 'lat', 'DD'), '');
  assert.equal(formatDegrees(20, 'depth', 'DMS'), '');
  assert.equal(formatDegrees(Infinity, 'lon', 'DM'), '');
  assert.equal(normalizeFormat('bogus'), 'DD');
  assert.equal(normalizeFormat('DMS'), 'DMS');
});

test('rounds near minute/second boundaries without 60-second labels', () => {
  assert.equal(formatDegrees(10.999999, 'lon', 'DMS'), '11° E');
  assert.equal(formatDegrees(10.999999, 'lon', 'DM'), '11° E');
  assert.equal(formatDegrees(9.999999, 'lat', 'DMS'), '10° N');
  // A 59.9999" remainder must roll into the next minute, never 60″.
  const rolled = formatDegrees(23.9999999, 'lat', 'DMS');
  assert.doesNotMatch(rolled, /60′|60″/);
  assert.equal(rolled, '24° N');
});

test('zero, ±180 and hemispheres never contradict the numeric sign', () => {
  assert.equal(formatDegrees(0, 'lat', 'DMS'), '0°');
  assert.equal(formatDegrees(-0, 'lon', 'DD'), '0°');
  assert.equal(formatDegrees(180, 'lon', 'DD'), '180°');
  assert.equal(formatDegrees(-180, 'lon', 'DMS'), '180°');
  assert.equal(formatDegrees(-180, 'lon', 'Degrees'), '180°');
  assert.equal(formatDegrees(90, 'lat', 'DD'), '90° N');
  assert.equal(formatDegrees(-90, 'lat', 'DD'), '90° S');
  assert.equal(formatDegrees(1, 'lon', 'DD'), '1° E');
  assert.equal(formatDegrees(-1, 'lon', 'DD'), '1° W');
});
