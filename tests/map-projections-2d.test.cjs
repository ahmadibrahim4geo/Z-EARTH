const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const Proj = require('../src/ZEarth.Globe/Assets/map-projections.js');

test('ZEarthMapProjections instantiates all required projection families', () => {
  assert.ok(Proj.Geographic);
  assert.ok(Proj.WebMercator);
  assert.ok(Proj.Robinson);
  assert.ok(Proj.Mollweide);
  assert.ok(Proj.MillerCylindrical);
  assert.ok(Proj.Sinusoidal);
  assert.ok(Proj.TransverseMercator);
  assert.ok(Proj.PolarStereographic);
  assert.ok(Proj.LambertConformalConic);
});

test('GeographicProjection forward and inverse matches WGS84 coordinates', () => {
  const geo = Proj.create({ epsg: 4326, isProjected: false });
  const cairo = { longitude: 31.2357 * Math.PI / 180, latitude: 30.0444 * Math.PI / 180, height: 10 };
  const p = geo.project(cairo);
  const u = geo.unproject(p);

  assert.ok(Math.abs(u.longitude - cairo.longitude) < 1e-12);
  assert.ok(Math.abs(u.latitude - cairo.latitude) < 1e-12);
  assert.equal(u.height, 10);
});

test('WebMercatorProjection forward and inverse matches EPSG:3857', () => {
  const web = Proj.create({ epsg: 3857, isProjected: true });
  const alex = { longitude: 29.9187 * Math.PI / 180, latitude: 31.2001 * Math.PI / 180, height: 5 };
  const p = web.project(alex);
  const u = web.unproject(p);

  assert.ok(Math.abs(u.longitude - alex.longitude) < 1e-12);
  assert.ok(Math.abs(u.latitude - alex.latitude) < 1e-12);
});

test('World Robinson ESRI:54030 produces smooth curved meridians and recovers position', () => {
  const rob = Proj.create({ epsg: 54030, isProjected: true });
  const points = [
    { longitude: 0, latitude: 0 },
    { longitude: 31.2 * Math.PI / 180, latitude: 30.0 * Math.PI / 180 },
    { longitude: -74.0 * Math.PI / 180, latitude: 40.7 * Math.PI / 180 },
    { longitude: 139.7 * Math.PI / 180, latitude: 35.6 * Math.PI / 180 }
  ];

  for (const pt of points) {
    const p = rob.project(pt);
    const u = rob.unproject(p);
    assert.ok(Math.abs(u.longitude - pt.longitude) < 1e-7, `Robinson lon failed for ${pt.longitude}`);
    assert.ok(Math.abs(u.latitude - pt.latitude) < 1e-7, `Robinson lat failed for ${pt.latitude}`);
  }
});

test('World Mollweide ESRI:54009 equal-area projection round-trip accuracy', () => {
  const mol = Proj.create({ epsg: 54009, isProjected: true });
  const pt = { longitude: 45.0 * Math.PI / 180, latitude: 25.0 * Math.PI / 180 };
  const p = mol.project(pt);
  const u = mol.unproject(p);

  assert.ok(Math.abs(u.longitude - pt.longitude) < 1e-9);
  assert.ok(Math.abs(u.latitude - pt.latitude) < 1e-9);
});

test('Survey of Egypt National Grids match standard Red Belt and Purple Belt false origins', () => {
  // Egypt Red Belt (EPSG:22992): Central Meridian 31°E, Origin Latitude 30°N, False Easting 615000m, False Northing 810000m
  const redBelt = Proj.create({ epsg: 22992, isProjected: true });
  const originRed = { longitude: 31.0 * Math.PI / 180, latitude: 30.0 * Math.PI / 180 };
  const pRed = redBelt.project(originRed);

  assert.equal(Math.round(pRed.x), 615000);
  assert.equal(Math.round(pRed.y), 810000);

  // Egypt Purple Belt (EPSG:22993): Central Meridian 27°E, Origin Latitude 30°N, False Easting 365000m, False Northing 810000m
  const purpleBelt = Proj.create({ epsg: 22993, isProjected: true });
  const originPurple = { longitude: 27.0 * Math.PI / 180, latitude: 30.0 * Math.PI / 180 };
  const pPurple = purpleBelt.project(originPurple);

  assert.equal(Math.round(pPurple.x), 365000);
  assert.equal(Math.round(pPurple.y), 810000);
});

test('UTM Zone Series covers all 60 Northern and Southern zones with exact false origins', () => {
  // UTM Zone 36N (Cairo / Nile Delta): Central Meridian = (36*6 - 183) = 33°E, False Easting = 500,000m, False Northing = 0m
  const utm36N = Proj.create({ epsg: 32636, isProjected: true });
  const pt36N = { longitude: 33.0 * Math.PI / 180, latitude: 0.0 };
  const p36N = utm36N.project(pt36N);
  assert.equal(Math.round(p36N.x), 500000);
  assert.equal(Math.round(p36N.y), 0);

  // UTM Zone 36S (Southern Africa): False Northing = 10,000,000m
  const utm36S = Proj.create({ epsg: 32736, isProjected: true });
  const pt36S = { longitude: 33.0 * Math.PI / 180, latitude: 0.0 };
  const p36S = utm36S.project(pt36S);
  assert.equal(Math.round(p36S.x), 500000);
  assert.equal(Math.round(p36S.y), 10000000);
});

test('Universal Polar Stereographic (UPS) North & South center on poles with 2,000,000m false origin', () => {
  const upsN = Proj.create({ epsg: 32661, isProjected: true });
  const northPole = { longitude: 0.0, latitude: Math.PI / 2.0 };
  const pN = upsN.project(northPole);
  assert.equal(Math.round(pN.x), 2000000);
  assert.equal(Math.round(pN.y), 2000000);

  const upsS = Proj.create({ epsg: 32761, isProjected: true });
  const southPole = { longitude: 0.0, latitude: -Math.PI / 2.0 };
  const pS = upsS.project(southPole);
  assert.equal(Math.round(pS.x), 2000000);
  assert.equal(Math.round(pS.y), 2000000);
});

test('HTML and Engine contract: map-projections.js is loaded and wired to CrsManager and setSceneMode', () => {
  const html = fs.readFileSync(path.join(__dirname, '../src/ZEarth.Globe/Assets/index.html'), 'utf8');
  assert.ok(html.includes('src="map-projections.js"'));
  assert.ok(html.indexOf('map-projections.js') < html.indexOf('cesium-engine.js'));

  const engine = fs.readFileSync(path.join(__dirname, '../src/ZEarth.Globe/Assets/cesium-engine.js'), 'utf8');
  assert.ok(engine.includes('ZEarthMapProjections.create'));
  assert.ok(engine.includes('viewer.scene._mapProjection = newProj'));
  assert.ok(engine.includes('viewer.scene._mapProjection = CrsManager.activeProjection'));
});
