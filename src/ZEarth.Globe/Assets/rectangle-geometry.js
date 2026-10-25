/* Zero-dependency geographic rectangle math shared by Cesium and Node.js.
 * Axis-aligned WGS84 angular rectangle (not constant-distance geodesic box).
 * Deliberately reject dateline crossings until split-polygon support is added.
 */
(function (root) {
  'use strict';
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
  const api = Object.freeze({rectangleCorners});
  if (root) root.ZEarthGeometry = api;
  if (typeof module === 'object' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : null);
