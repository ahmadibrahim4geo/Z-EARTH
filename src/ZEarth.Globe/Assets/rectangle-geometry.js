/* Zero-dependency geographic geometry math shared by Cesium and Node.js.
 * Provides rectangle corners, forward geodesic bearing, Ramanujan ellipse perimeter,
 * and high-precision geodesic circle and ellipse boundary sampling.
 * Deliberately reject dateline crossings until split-polygon support is added.
 */
(function (root) {
  'use strict';

  const WGS84_AUTHALIC_RADIUS = 6371007.181;

  function rectangleCorners(lon1, lat1, lon2, lat2) {
    const v = [lon1, lat1, lon2, lat2];
    if (!v.every(Number.isFinite)) return null;
    if (Math.abs(lon1) > 180 || Math.abs(lon2) > 180 ||
        Math.abs(lat1) > 90 || Math.abs(lat2) > 90) return null;
    if (Math.abs(lon1 - lon2) >= 180 ||
        Math.abs(lon1 - lon2) < 1e-9 ||
        Math.abs(lat1 - lat2) < 1e-9) return null;
    const west = Math.min(lon1, lon2), east = Math.max(lon1, lon2);
    const south = Math.min(lat1, lat2), north = Math.max(lat1, lat2);
    return [west, south, east, south, east, north, west, north];
  }

  function computeOrientedRectangle(lon1, lat1, lon2, lat2, lon3, lat3) {
    const v = [lon1, lat1, lon2, lat2, lon3, lat3];
    if (!v.every(Number.isFinite)) return null;
    if (Math.abs(lon1) > 180 || Math.abs(lon2) > 180 || Math.abs(lon3) > 180 ||
        Math.abs(lat1) > 90 || Math.abs(lat2) > 90 || Math.abs(lat3) > 90) return null;

    const latMid = ((lat1 + lat2) / 2.0) * Math.PI / 180.0;
    const mPerDegLat = WGS84_AUTHALIC_RADIUS * Math.PI / 180.0;
    const mPerDegLon = mPerDegLat * Math.cos(latMid);

    // Baseline vector 1 -> 2 in meters
    const dx12 = (lon2 - lon1) * mPerDegLon;
    const dy12 = (lat2 - lat1) * mPerDegLat;
    const length = Math.hypot(dx12, dy12);
    if (length < 1e-4) return null;

    // Unit baseline vector
    const ux = dx12 / length;
    const uy = dy12 / length;

    // Unit perpendicular vector (90 deg counter-clockwise)
    const nx = -uy;
    const ny = ux;

    // Vector 1 -> 3 in meters
    const dx13 = (lon3 - lon1) * mPerDegLon;
    const dy13 = (lat3 - lat1) * mPerDegLat;

    // Width is projection onto unit normal
    const widthSigned = dx13 * nx + dy13 * ny;
    const width = Math.abs(widthSigned);

    // Corner 1: lon1, lat1
    // Corner 2: lon2, lat2
    // Corner 3: P2 + normal * widthSigned
    const c3_dx = dx12 + nx * widthSigned;
    const c3_dy = dy12 + ny * widthSigned;
    const lon3Corner = lon1 + c3_dx / mPerDegLon;
    const lat3Corner = lat1 + c3_dy / mPerDegLat;

    // Corner 4: P1 + normal * widthSigned
    const c4_dx = nx * widthSigned;
    const c4_dy = ny * widthSigned;
    const lon4Corner = lon1 + c4_dx / mPerDegLon;
    const lat4Corner = lat1 + c4_dy / mPerDegLat;

    const bearing = calculateBearing(lon1, lat1, lon2, lat2);
    const perimeter = 2.0 * (length + width);
    const area = length * width;

    return {
      corners: [lon1, lat1, lon2, lat2, lon3Corner, lat3Corner, lon4Corner, lat4Corner],
      length,
      width,
      bearing,
      perimeter,
      area
    };
  }

  function calculateBearing(lon1, lat1, lon2, lat2) {
    if (!Number.isFinite(lon1) || !Number.isFinite(lat1) ||
        !Number.isFinite(lon2) || !Number.isFinite(lat2)) return 0.0;
    if (Math.abs(lon1 - lon2) < 1e-9 && Math.abs(lat1 - lat2) < 1e-9) return 0.0;
    const phi1 = (lat1 * Math.PI) / 180;
    const phi2 = (lat2 * Math.PI) / 180;
    const dlam = ((lon2 - lon1) * Math.PI) / 180;
    const y = Math.sin(dlam) * Math.cos(phi2);
    const x = Math.cos(phi1) * Math.sin(phi2) - Math.sin(phi1) * Math.cos(phi2) * Math.cos(dlam);
    const deg = (Math.atan2(y, x) * 180) / Math.PI;
    return (deg + 360.0) % 360.0;
  }

  function calculateEllipsePerimeter(a, b) {
    const axisA = Math.abs(Number(a) || 0);
    const axisB = Math.abs(Number(b) || 0);
    if (axisA < 1e-9 && axisB < 1e-9) return 0.0;
    if (axisA < 1e-9) return 4.0 * axisB;
    if (axisB < 1e-9) return 4.0 * axisA;
    if (Math.abs(axisA - axisB) < 1e-9) return 2.0 * Math.PI * axisA;
    const h = ((axisA - axisB) ** 2) / ((axisA + axisB) ** 2);
    return Math.PI * (axisA + axisB) * (1.0 + (3.0 * h) / (10.0 + Math.sqrt(4.0 - 3.0 * h)));
  }

  function sampleCircle(centerLon, centerLat, radiusMeters, pointsCount) {
    if (!Number.isFinite(centerLon) || !Number.isFinite(centerLat) || !Number.isFinite(radiusMeters)) return [];
    const n = Math.max(8, Math.min(256, pointsCount || 64));
    const phi0 = (centerLat * Math.PI) / 180;
    const lam0 = (centerLon * Math.PI) / 180;
    const delta = radiusMeters / WGS84_AUTHALIC_RADIUS;
    const coords = [];
    for (let i = 0; i < n; i++) {
      const alpha = (2 * Math.PI * i) / n;
      const sinPhi = Math.sin(phi0) * Math.cos(delta) + Math.cos(phi0) * Math.sin(delta) * Math.cos(alpha);
      const phi = Math.asin(Math.max(-1, Math.min(1, sinPhi)));
      const lam = lam0 + Math.atan2(Math.sin(alpha) * Math.sin(delta) * Math.cos(phi0), Math.cos(delta) - Math.sin(phi0) * sinPhi);
      let degLon = (lam * 180) / Math.PI;
      while (degLon > 180) degLon -= 360;
      while (degLon < -180) degLon += 360;
      coords.push(degLon, (phi * 180) / Math.PI);
    }
    return coords;
  }

  function sampleEllipse(centerLon, centerLat, semiMajorMeters, semiMinorMeters, bearingDegrees, pointsCount) {
    if (!Number.isFinite(centerLon) || !Number.isFinite(centerLat) ||
        !Number.isFinite(semiMajorMeters) || !Number.isFinite(semiMinorMeters)) return [];
    const n = Math.max(8, Math.min(256, pointsCount || 64));
    const a = Math.abs(semiMajorMeters);
    const b = Math.abs(semiMinorMeters);
    const phi0 = (centerLat * Math.PI) / 180;
    const lam0 = (centerLon * Math.PI) / 180;
    const theta0 = ((bearingDegrees || 0) * Math.PI) / 180;
    const coords = [];
    for (let i = 0; i < n; i++) {
      const t = (2 * Math.PI * i) / n;
      const dx = a * Math.cos(t) * Math.sin(theta0) + b * Math.sin(t) * Math.cos(theta0);
      const dy = a * Math.cos(t) * Math.cos(theta0) - b * Math.sin(t) * Math.sin(theta0);
      const d = Math.hypot(dx, dy);
      const alpha = Math.atan2(dx, dy);
      const delta = d / WGS84_AUTHALIC_RADIUS;
      const sinPhi = Math.sin(phi0) * Math.cos(delta) + Math.cos(phi0) * Math.sin(delta) * Math.cos(alpha);
      const phi = Math.asin(Math.max(-1, Math.min(1, sinPhi)));
      const lam = lam0 + Math.atan2(Math.sin(alpha) * Math.sin(delta) * Math.cos(phi0), Math.cos(delta) - Math.sin(phi0) * sinPhi);
      let degLon = (lam * 180) / Math.PI;
      while (degLon > 180) degLon -= 360;
      while (degLon < -180) degLon += 360;
      coords.push(degLon, (phi * 180) / Math.PI);
    }
    return coords;
  }

  const api = Object.freeze({
    rectangleCorners,
    computeOrientedRectangle,
    calculateBearing,
    calculateEllipsePerimeter,
    sampleCircle,
    sampleEllipse
  });

  if (root) root.ZEarthGeometry = api;
  if (typeof module === 'object' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : null);
