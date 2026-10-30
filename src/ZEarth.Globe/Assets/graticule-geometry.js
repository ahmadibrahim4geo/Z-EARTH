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

  // Extend line geometry beyond the visible rectangle so a moving camera does
  // not expose an empty strip while the next grid is being prepared.
  function expandViewBounds(bounds, fraction = 0.3) {
    if (!bounds || ![bounds.west, bounds.east, bounds.south, bounds.north].every(Number.isFinite)) return null;
    const span = bounds.east >= bounds.west ? bounds.east - bounds.west : 360 - bounds.west + bounds.east;
    const padding = Math.max(0, fraction);
    const width = Math.min(360, span * (1 + 2 * padding));
    const south = Math.max(-90, bounds.south - (bounds.north - bounds.south) * padding);
    const north = Math.min(90, bounds.north + (bounds.north - bounds.south) * padding);
    if (width >= 360 - 1e-9) return { west: -180, east: 180, south, north };
    const wrap = value => ((value + 180) % 360 + 360) % 360 - 180;
    const west = wrap(bounds.west - span * padding);
    return { west, east: wrap(west + width), south, north };
  }

  function boundsContain(outer, inner) {
    if (!outer || !inner || inner.south < outer.south || inner.north > outer.north) return false;
    const covering = longitudeWindows(outer);
    return longitudeWindows(inner).every(w => covering.some(c => w.west >= c.west && w.east <= c.east));
  }

  // Preserve the line's fixed latitude/longitude while letting its label slide
  // along that line as the view moves. Longitude wraps at the antimeridian.
  function movingLabelCoordinates(axis, fixed, along, originalCenter, currentCenter) {
    if (axis === 'parallel') {
      const delta = currentCenter.lon - originalCenter.lon;
      const longitude = ((along + delta + 540) % 360) - 180;
      return { longitude, latitude: fixed };
    }
    return {
      longitude: fixed,
      latitude: Math.max(-89.999, Math.min(89.999, along + currentCenter.lat - originalCenter.lat))
    };
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

  // UTM grid-zone designation latitude bands, C..X (I and O omitted, X spans 12°).
  const LATITUDE_BANDS = 'CDEFGHJKLMNPQRSTUVWX';

  function latitudeBandBounds(latitude) {
    if (!Number.isFinite(latitude) || latitude < LAT_MIN || latitude >= LAT_MAX) return null;
    const index = Math.min(19, Math.floor((latitude - LAT_MIN) / 8));
    const south = LAT_MIN + index * 8;
    const north = index === 19 ? LAT_MAX : south + 8;
    return { band: LATITUDE_BANDS[index], south, north };
  }

  function latitudeBand(latitude) {
    const bounds = latitudeBandBounds(latitude);
    return bounds ? bounds.band : null;
  }

  // A zone label must sit inside the zone polygon, never on a boundary meridian.
  // Longitude is the visual center of the actual (exception-aware) zone width;
  // latitude sits in the lower part of the GZD latitude band.
  function utmLabelAnchor(zone, latitude) {
    if (!Number.isFinite(latitude) || latitude < LAT_MIN || latitude >= LAT_MAX) return null;
    const limits = zoneBounds(zone, latitude);
    const band = latitudeBandBounds(latitude);
    if (!limits || !band) return null;
    // Sit in the lower part of the band rather than its exact centre: the
    // Tropics and the Equator are near band centres, and a centred UTM label
    // collided with those major-reference labels at regional zoom.
    const bandHeight = band.north - band.south;
    const labelLatitude = Math.max(LAT_MIN + 0.001,
      Math.min(LAT_MAX - 0.001, band.south + bandHeight * 0.26));
    // Norway/Svalbard exceptions change the zone width, so recompute there.
    const labelLimits = zoneBounds(zone, labelLatitude) || limits;
    return {
      zone,
      west: labelLimits.west,
      east: labelLimits.east,
      longitude: (labelLimits.west + labelLimits.east) / 2,
      latitude: labelLatitude,
      band: band.band
    };
  }

  function gridZoneDesignation(longitude, latitude) {
    const zone = zoneAt(longitude, latitude);
    const band = latitudeBand(latitude);
    return zone && band ? `${zone}${band}` : null;
  }

  function formatUtmZoneLabel(zone, latitude, mode = 'Both') {
    if (!Number.isInteger(zone) || zone < 1 || zone > 60) return '';
    const band = latitudeBand(latitude);
    if (!band) return '';
    const hemisphere = latitude >= 0 ? 'N' : 'S';
    if (mode === 'Hemisphere') return `UTM ${zone}${hemisphere}`;
    if (mode === 'GridZone') return `${zone}${band}`;
    return `UTM ${zone}${hemisphere} · GZD ${zone}${band}`;
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
  // One label per visible UTM zone, anchored at the zone/band center rather than
  // on the boundary. Boundaries come from visibleUtmSegments; labels never do.
  function visibleUtmZoneLabels(bounds) {
    if (!bounds || !Number.isFinite(bounds.south) || !Number.isFinite(bounds.north)) return [];
    const windows = longitudeWindows(bounds);
    const centerLatitude = Math.max(LAT_MIN, Math.min(LAT_MAX - 0.001, (bounds.south + bounds.north) / 2));
    const band = latitudeBandBounds(centerLatitude);
    if (!band) return [];
    const result = [];
    for (let zone = 1; zone <= 60; zone++) {
      const limits = zoneBounds(zone, centerLatitude);
      if (!limits || !windows.some(w => limits.east >= w.west && limits.west <= w.east)) continue;
      const anchor = utmLabelAnchor(zone, centerLatitude);
      if (!anchor) continue;
      let latitude = anchor.latitude;
      // Keep the label inside the visible part of its own latitude band.
      if (latitude < bounds.south || latitude >= bounds.north) {
        const south = Math.max(bounds.south, band.south);
        const north = Math.min(bounds.north, band.north);
        if (south >= north) continue;
        latitude = (south + north) / 2;
      }
      result.push({
        zone,
        west: limits.west,
        east: limits.east,
        longitude: (limits.west + limits.east) / 2,
        latitude,
        band: band.band
      });
    }
    return result;
  }

  // A surface reference point is visible only on the camera-facing hemisphere.
  // Use the WGS84 geodetic normal supplied by Cesium (not a spherical shortcut);
  // a nonpositive dot product is behind the local horizon/tangent plane.
  function isSurfaceAnchorVisible(anchor, normal, camera) {
    if (![anchor, normal, camera].every(v => v &&
        Number.isFinite(v.x) && Number.isFinite(v.y) && Number.isFinite(v.z))) return false;
    return normal.x * (camera.x - anchor.x) +
           normal.y * (camera.y - anchor.y) +
           normal.z * (camera.z - anchor.z) > 0;
  }

  return { longitudeWindows, expandViewBounds, boundsContain, movingLabelCoordinates, zoneBounds, zoneAt, latitudeBand, latitudeBandBounds, gridZoneDesignation, formatUtmZoneLabel, utmLabelAnchor, visibleUtmSegments, visibleUtmZoneLabels, isSurfaceAnchorVisible };
});
