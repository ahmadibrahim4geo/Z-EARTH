const test = require('node:test');
const assert = require('node:assert/strict');
const { longitudeWindows, zoneBounds, zoneAt, visibleUtmSegments } =
  require('../src/ZEarth.Globe/Assets/graticule-geometry.js');

test('camera-following labels slide only along their own geographic line', () => {
  const { movingLabelCoordinates: move } = require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  assert.deepEqual(move('parallel', 10, 20, {lon: 20, lat: 30}, {lon: 30, lat: 40}), {longitude: 30, latitude: 10});
  assert.deepEqual(move('meridian', 25, 40, {lon: 20, lat: 30}, {lon: 30, lat: 40}), {longitude: 25, latitude: 50});
  assert.deepEqual(move('parallel', 10, 175, {lon: 175, lat: 30}, {lon: -175, lat: 30}), {longitude: -175, latitude: 10});
  assert.deepEqual(move('meridian', 25, 80, {lon: 20, lat: 30}, {lon: 20, lat: 60}), {longitude: 25, latitude: 89.999});
});

test('buffered visible bounds cover movement across the date line and up to the poles', () => {
  const {expandViewBounds, boundsContain} = require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  const original = {west: 170, east: -170, south: 78, north: 89};
  const expanded = expandViewBounds(original);
  assert.equal(expanded.south, 74.7);
  assert.equal(expanded.north, 90);
  assert.equal(boundsContain(expanded, original), true);
  assert.equal(boundsContain(expanded, {west: 173, east: -167, south: 79, north: 90}), true);
  assert.equal(boundsContain(expanded, {west: 145, east: 165, south: 79, north: 89}), false);
  assert.deepEqual(expandViewBounds({west:-180,east:180,south:-90,north:90}),
    {west:-180,east:180,south:-90,north:90});
});

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

test('UTM zone labels sit inside their zone and never on a boundary meridian', () => {
  const { utmLabelAnchor, visibleUtmZoneLabels, zoneBounds } =
    require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  const anchor = utmLabelAnchor(36, 30);
  assert.ok(anchor);
  assert.equal(anchor.zone, 36);
  assert.equal(anchor.west, 30);
  assert.equal(anchor.east, 36);
  // The centre is strictly inside the zone, not on either boundary.
  assert.ok(anchor.longitude > anchor.west && anchor.longitude < anchor.east);
  assert.equal(anchor.longitude, 33);
  assert.equal(anchor.band, 'R');
  assert.ok(anchor.latitude > 24 && anchor.latitude < 32);

  const labels = visibleUtmZoneLabels({ west: 24, east: 37, south: 21, north: 33 });
  assert.deepEqual(labels.map(l => l.zone), [34, 35, 36, 37]);
  for (const label of labels) {
    const limits = zoneBounds(label.zone, label.latitude);
    assert.ok(label.longitude > limits.west && label.longitude < limits.east,
      `zone ${label.zone} label must be inside its boundaries`);
  }
});

test('UTM label placement respects Norway and Svalbard exception widths', () => {
  const { utmLabelAnchor, visibleUtmZoneLabels, zoneBounds } =
    require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  // Norway 32V spans 3°E..12°E; its label must be centred near 7.5°E.
  const norway = utmLabelAnchor(32, 60);
  assert.ok(norway.longitude > 3 && norway.longitude < 12);
  // Svalbard 33X spans 9°E..21°E.
  const svalbard = utmLabelAnchor(33, 78);
  assert.equal(svalbard.west, 9);
  assert.equal(svalbard.east, 21);
  assert.ok(svalbard.longitude > 9 && svalbard.longitude < 21);
  // Zones that do not exist in the Svalbard X band produce no label.
  assert.equal(utmLabelAnchor(32, 78), null);
  const labels = visibleUtmZoneLabels({ west: 0.5, east: 41.5, south: 72, north: 84 });
  assert.deepEqual(labels.map(l => l.zone), [31, 33, 35, 37]);
  for (const label of labels) {
    const limits = zoneBounds(label.zone, label.latitude);
    assert.ok(label.longitude > limits.west && label.longitude < limits.east);
  }
});

test('UTM labels crossing the antimeridian stay inside their own zone window', () => {
  const { visibleUtmZoneLabels } = require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  const labels = visibleUtmZoneLabels({ west: 175, east: -175, south: 22, north: 32 });
  assert.deepEqual(labels.map(l => l.zone), [1, 60]);
  const zone1 = labels.find(l => l.zone === 1);
  const zone60 = labels.find(l => l.zone === 60);
  assert.ok(zone1.longitude > -180 && zone1.longitude < -174);
  assert.ok(zone60.longitude > 174 && zone60.longitude < 180);
});

test('UTM label latitude stays clear of the band centre used by major parallels', () => {
  const { utmLabelAnchor, latitudeBandBounds } =
    require('../src/ZEarth.Globe/Assets/graticule-geometry.js');
  // Band R spans 24..32; the Tropic of Cancer (23.44) sits just below it and
  // an equator-centred label used to be drawn on top of the Tropic label.
  const band = latitudeBandBounds(30);
  const anchor = utmLabelAnchor(36, 30);
  assert.ok(anchor.latitude > band.south && anchor.latitude < band.north,
    'The label must stay inside its own latitude band.');
  assert.ok(anchor.latitude < (band.south + band.north) / 2,
    'The label must sit below the band centre, not on it.');
  for (const major of [0, 23.4365, -23.4365, 66.5635, -66.5635]) {
    assert.ok(Math.abs(anchor.latitude - major) > 1.5,
      `UTM label must not coincide with the major parallel at ${major}`);
  }
});
