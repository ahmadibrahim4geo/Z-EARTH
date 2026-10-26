const test = require('node:test');
const assert = require('node:assert/strict');
const { longitudeWindows, zoneBounds, zoneAt, visibleUtmSegments } =
  require('../src/ZEarth.Globe/Assets/graticule-geometry.js');

test('UTM longitude zones including exact zone 1/60 edges and hemisphere', () => {
  assert.equal(zoneAt(-180, 25), 1);
  assert.equal(zoneAt(180, 25), 60);
  assert.equal(zoneAt(30.1, 30), 36);
  assert.equal(zoneAt(30.1, -30), 36);
  assert.equal(zoneAt(0, 84), null);
  assert.equal(zoneAt(30, -80.01), null);
});
test('Norway and Svalbard have correct UTM exceptions', () => {
  assert.equal(zoneAt(4, 60), 32);
  assert.deepEqual(zoneBounds(31, 60), { zone: 31, west: 0, east: 3 });
  assert.deepEqual(zoneBounds(32, 60), { zone: 32, west: 3, east: 12 });
  assert.equal(zoneAt(8.5, 78), 31);
  assert.equal(zoneAt(13, 78), 33);
  assert.equal(zoneAt(25, 78), 35);
  assert.equal(zoneAt(37, 78), 37);
  assert.equal(zoneBounds(32, 78), null);
});
test('antimeridian viewport produces two narrow windows, not full globe', () => {
  assert.deepEqual(longitudeWindows({west:170,east:-170}), [
    {west:170,east:180},{west:-180,east:-170}
  ]);
  const segments = visibleUtmSegments({west:175,east:-175,south:22,north:32});
  assert.ok(segments.length > 0 && segments.length < 6);
  assert.deepEqual([...new Set(segments.map(s => s.zone))], [1, 60]);
});
test('Egypt viewport contains realistic zone geometries and latitudes', () => {
  const segments = visibleUtmSegments({west:24,east:37,south:21,north:33});
  assert.deepEqual(segments.map(s => s.zone), [34, 35, 36, 37]);
  assert.ok(segments.every(s => s.south === 21 && s.north === 33));
});

test('camera-facing horizon culls north/south poles and 0/0 when they are behind globe', () => {
  const { isSurfaceAnchorVisible: visible } =
    require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  const north = { x: 0, y: 0, z: 1 };
  const south = { x: 0, y: 0, z: -1 };
  const origin = { x: 1, y: 0, z: 0 };
  assert.equal(visible(north, north, { x: 0, y: 0, z: 4 }), true);
  assert.equal(visible(north, north, { x: 0, y: 0, z: -4 }), false);
  assert.equal(visible(south, south, { x: 0, y: 0, z: -4 }), true);
  assert.equal(visible(south, south, { x: 0, y: 0, z: 4 }), false);
  assert.equal(visible(origin, origin, { x: 3, y: 0, z: 0 }), true);
  assert.equal(visible(origin, origin, { x: -3, y: 0, z: 0 }), false);
  assert.equal(visible(north, north, { x: 2, y: 0, z: 1 }), false);
  assert.equal(visible(north, north, { x: NaN, y: 0, z: 4 }), false);
});

test('UTM grid-zone latitude bands reflect real geographic grid zone designation', () => {
  const { latitudeBand, gridZoneDesignation, formatUtmZoneLabel } =
    require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  assert.equal(latitudeBand(-80), 'C');
  assert.equal(latitudeBand(-72), 'D');
  assert.equal(latitudeBand(30), 'R');
  assert.equal(latitudeBand(75), 'X');
  assert.equal(latitudeBand(84), null);
  assert.equal(gridZoneDesignation(31.1, 30), '36R');
  assert.equal(gridZoneDesignation(31.1, -30), '36J');
  assert.equal(formatUtmZoneLabel(36, 30, 'Both'), 'UTM 36N · GZD 36R');
  assert.equal(formatUtmZoneLabel(36, 30, 'Hemisphere'), 'UTM 36N');
  assert.equal(formatUtmZoneLabel(36, 30, 'GridZone'), '36R');
  assert.equal(formatUtmZoneLabel(36, 84), '');
});
