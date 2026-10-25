/* Independent WGS84 UTM geometry used by Z EARTH's graticule layer and Node tests. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.ZEarthGraticuleGeometry = api;
})(typeof window !== 'undefined' ? window : null, function () {
  const LAT_MIN = -80;
  const LAT_MAX = 84;

  function longitudeWindows(bounds) {
    if (!bounds || !Number.isFinite(bounds.west) || !Number.isFinite(bounds.east)) return [];
    if (bounds.east >= bounds.west) return [{ west: bounds.west, east: bounds.east }];
    // A viewport crossing the antimeridian is not the entire planet.
    return [{ west: bounds.west, east: 180 }, { west: -180, east: bounds.east }];
  }

  function zoneBounds(zone, latitude) {
    if (!Number.isInteger(zone) || zone < 1 || zone > 60 ||
        !Number.isFinite(latitude) || latitude < LAT_MIN || latitude >= LAT_MAX) return null;
    let west = -180 + (zone - 1) * 6;
    let east = west + 6;
    // Norway 32V exception; adjacent 31V is narrower.
    if (latitude >= 56 && latitude < 64) {
      if (zone === 31) east = 3;
      if (zone === 32) west = 3;
    }
    // Svalbard X band has 31X, 33X, 35X and 37X enlarged zones.
    if (latitude >= 72 && latitude < 84) {
      if ([32, 34, 36].includes(zone)) return null;
      if (zone === 31) { west = 0; east = 9; }
      if (zone === 33) { west = 9; east = 21; }
      if (zone === 35) { west = 21; east = 33; }
      if (zone === 37) { west = 33; east = 42; }
    }
    return { zone, west, east };
  }

  function zoneAt(longitude, latitude) {
    if (!Number.isFinite(longitude) || !Number.isFinite(latitude) ||
        longitude < -180 || longitude > 180 || latitude < LAT_MIN || latitude >= LAT_MAX) return null;
    if (latitude >= 72 && longitude >= 0 && longitude < 42) {
      return longitude < 9 ? 31 : longitude < 21 ? 33 : longitude < 33 ? 35 : 37;
    }
    if (latitude >= 56 && latitude < 64 && longitude >= 3 && longitude < 12) return 32;
    return longitude === 180 ? 60 : Math.floor((longitude + 180) / 6) + 1;
  }

  function visibleUtmSegments(bounds) {
    if (!bounds || !Number.isFinite(bounds.south) || !Number.isFinite(bounds.north)) return [];
    const windows = longitudeWindows(bounds);
    const result = [];
    const cuts = [LAT_MIN, 56, 64, 72, LAT_MAX];
    for (let band = 0; band < cuts.length - 1; band++) {
      const south = Math.max(bounds.south, cuts[band]);
      const north = Math.min(bounds.north, cuts[band + 1]);
      if (south >= north) continue;
      const representativeLat = (south + north) / 2;
      for (let zone = 1; zone <= 60; zone++) {
        const limits = zoneBounds(zone, representativeLat);
        if (!limits || !windows.some(w => limits.east >= w.west && limits.west <= w.east)) continue;
        result.push({ zone, west: limits.west, east: limits.east, south, north });
      }
    }
    return result;
  }
  return { longitudeWindows, zoneBounds, zoneAt, visibleUtmSegments };
});
