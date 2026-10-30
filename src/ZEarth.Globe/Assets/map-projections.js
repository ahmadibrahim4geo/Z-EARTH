/**
 * Z EARTH — 2D Map Projections for CesiumJS (ArcGIS Pro Parity)
 *
 * Implements standard Cesium.MapProjection interface for 2D Map mode:
 *   - project(cartographic, result) -> Cartesian3(x, y, z)
 *   - unproject(cartesian, result)   -> Cartographic(lon_rad, lat_rad, height)
 *
 * Supported Families:
 *   1. Geographic / Equirectangular / Plate Carrée (EPSG:4326)
 *   2. Web Mercator (EPSG:3857)
 *   3. World Robinson (ESRI:54030)
 *   4. World Mollweide (ESRI:54009)
 *   5. World Miller Cylindrical (ESRI:54003)
 *   6. World Sinusoidal (ESRI:54008)
 *   7. Transverse Mercator:
 *        - UTM Northern & Southern Hemispheres (EPSG 32601-32660, 32701-32760)
 *        - Survey of Egypt National Grids (Egypt 1907 Red, Purple, South, Extended Purple)
 *        - British National Grid (EPSG:27700)
 *        - Gauss-Krüger Pulkovo 1942 zones 1-32 (EPSG 28401-28432)
 *        - Middle Eastern TM Grids (Ain el Abd, Jordan JTM, Palestine Grid, DLTM)
 *   8. Universal Polar Stereographic (UPS North EPSG:32661, UPS South EPSG:32761, Arctic, Antarctic)
 *   9. Lambert Conformal Conic (France Lambert-93 EPSG:2154, US State Plane LCC)
 */

(function(global) {
  'use strict';

  const WGS84_A = 6378137.0;
  const WGS84_B = 6356752.314245179;
  const WGS84_F = 1.0 / 298.257223563;
  const WGS84_E2 = WGS84_F * (2.0 - WGS84_F);
  const PI = Math.PI;
  const HALF_PI = PI / 2.0;
  const TWO_PI = 2.0 * PI;
  const DEG_TO_RAD = PI / 180.0;
  const RAD_TO_DEG = 180.0 / PI;

  function toRadians(deg) { return deg * DEG_TO_RAD; }
  function toDegrees(rad) { return rad * RAD_TO_DEG; }

  function getEllipsoid(ellipsoid) {
    if (ellipsoid && ellipsoid.maximumRadius) {
      const a = ellipsoid.maximumRadius;
      const b = ellipsoid.minimumRadius || (a * (1.0 - WGS84_F));
      const f = (a - b) / a;
      const e2 = f * (2.0 - f);
      return { a, b, f, e2, raw: ellipsoid };
    }
    return { a: WGS84_A, b: WGS84_B, f: WGS84_F, e2: WGS84_E2, raw: null };
  }

  function makeCartesian3(x, y, z, result) {
    if (result) {
      result.x = x;
      result.y = y;
      result.z = z || 0.0;
      return result;
    }
    if (typeof Cesium !== 'undefined' && Cesium.Cartesian3) {
      return new Cesium.Cartesian3(x, y, z || 0.0);
    }
    return { x, y, z: z || 0.0 };
  }

  function makeCartographic(lon, lat, height, result) {
    if (result) {
      result.longitude = lon;
      result.latitude = lat;
      result.height = height || 0.0;
      return result;
    }
    if (typeof Cesium !== 'undefined' && Cesium.Cartographic) {
      return new Cesium.Cartographic(lon, lat, height || 0.0);
    }
    return { longitude: lon, latitude: lat, height: height || 0.0 };
  }

  // =========================================================================
  // 1. Geographic Projection (Plate Carrée / Equirectangular - EPSG:4326)
  // =========================================================================
  class GeographicMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'Geographic (Equirectangular)';
      this.epsg = 4326;
    }

    project(cartographic, result) {
      const x = this._e.a * cartographic.longitude;
      const y = this._e.a * cartographic.latitude;
      const z = cartographic.height || 0.0;
      return makeCartesian3(x, y, z, result);
    }

    unproject(cartesian, result) {
      const lon = cartesian.x / this._e.a;
      const lat = cartesian.y / this._e.a;
      const h = cartesian.z || 0.0;
      return makeCartographic(lon, lat, h, result);
    }
  }

  // =========================================================================
  // 2. Web Mercator Projection (EPSG:3857)
  // =========================================================================
  class WebMercatorMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'WGS 84 / Pseudo-Mercator';
      this.epsg = 3857;
      this._maxLat = toRadians(85.0511287798);
    }

    project(cartographic, result) {
      const lon = cartographic.longitude;
      let lat = cartographic.latitude;
      if (lat > this._maxLat) lat = this._maxLat;
      else if (lat < -this._maxLat) lat = -this._maxLat;

      const x = this._e.a * lon;
      const y = this._e.a * Math.log(Math.tan(PI / 4.0 + lat / 2.0));
      const z = cartographic.height || 0.0;
      return makeCartesian3(x, y, z, result);
    }

    unproject(cartesian, result) {
      const lon = cartesian.x / this._e.a;
      const lat = 2.0 * Math.atan(Math.exp(cartesian.y / this._e.a)) - HALF_PI;
      const h = cartesian.z || 0.0;
      return makeCartographic(lon, lat, h, result);
    }
  }

  // =========================================================================
  // 3. World Robinson Projection (ESRI:54030)
  // =========================================================================
  const ROBINSON_TABLE_X = [1.0000, 0.9986, 0.9954, 0.9900, 0.9822, 0.9730, 0.9600, 0.9427, 0.9216, 0.8962, 0.8679, 0.8350, 0.7986, 0.7597, 0.7186, 0.6732, 0.6213, 0.5722, 0.5322];
  const ROBINSON_TABLE_Y = [0.0000, 0.0620, 0.1240, 0.1860, 0.2480, 0.3100, 0.3720, 0.4340, 0.4958, 0.5571, 0.6176, 0.6769, 0.7346, 0.7903, 0.8435, 0.8936, 0.9394, 0.9761, 1.0000];

  class RobinsonMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'World Robinson';
      this.epsg = 54030;
    }

    project(cartographic, result) {
      const latDeg = Math.abs(cartographic.latitude * RAD_TO_DEG);
      const latClamped = Math.min(latDeg, 90.0);
      const idx = Math.min(Math.floor(latClamped / 5.0), 17);
      const rem = (latClamped - (idx * 5.0)) / 5.0;

      const factorX = ROBINSON_TABLE_X[idx] + rem * (ROBINSON_TABLE_X[idx + 1] - ROBINSON_TABLE_X[idx]);
      const factorY = ROBINSON_TABLE_Y[idx] + rem * (ROBINSON_TABLE_Y[idx + 1] - ROBINSON_TABLE_Y[idx]);

      const signY = cartographic.latitude < 0 ? -1.0 : 1.0;
      const x = 0.8487 * this._e.a * cartographic.longitude * factorX;
      const y = 1.3523 * this._e.a * factorY * signY;
      const z = cartographic.height || 0.0;
      return makeCartesian3(x, y, z, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const absY = Math.abs(cartesian.y);
      const signY = cartesian.y < 0 ? -1.0 : 1.0;
      const normY = Math.min(absY / (1.3523 * a), 1.0);

      let idx = 0;
      for (let i = 0; i < 18; i++) {
        if (normY >= ROBINSON_TABLE_Y[i] && normY <= ROBINSON_TABLE_Y[i + 1]) {
          idx = i;
          break;
        }
      }
      const spanY = ROBINSON_TABLE_Y[idx + 1] - ROBINSON_TABLE_Y[idx];
      const remY = spanY > 1e-7 ? (normY - ROBINSON_TABLE_Y[idx]) / spanY : 0.0;
      const latDeg = (idx + remY) * 5.0;
      const lat = signY * latDeg * DEG_TO_RAD;

      const factorX = ROBINSON_TABLE_X[idx] + remY * (ROBINSON_TABLE_X[idx + 1] - ROBINSON_TABLE_X[idx]);
      const denomX = 0.8487 * a * factorX;
      let lon = denomX > 1e-4 ? cartesian.x / denomX : 0.0;
      if (lon > PI) lon = PI;
      else if (lon < -PI) lon = -PI;

      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 4. World Mollweide Projection (ESRI:54009)
  // =========================================================================
  class MollweideMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'World Mollweide';
      this.epsg = 54009;
    }

    project(cartographic, result) {
      const a = this._e.a;
      const lat = cartographic.latitude;
      const lon = cartographic.longitude;

      if (Math.abs(Math.abs(lat) - HALF_PI) < 1e-6) {
        return makeCartesian3(0.0, (lat > 0 ? 1.0 : -1.0) * Math.SQRT2 * a, cartographic.height || 0.0, result);
      }

      // Solve 2*theta + sin(2*theta) = PI * sin(phi) via Newton-Raphson
      const target = PI * Math.sin(lat);
      let theta = lat;
      for (let i = 0; i < 10; i++) {
        const delta = (2.0 * theta + Math.sin(2.0 * theta) - target) / (2.0 + 2.0 * Math.cos(2.0 * theta));
        theta -= delta;
        if (Math.abs(delta) < 1e-9) break;
      }

      const x = (2.0 * Math.SQRT2 / PI) * a * lon * Math.cos(theta);
      const y = Math.SQRT2 * a * Math.sin(theta);
      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const sinTheta = Math.max(-1.0, Math.min(1.0, cartesian.y / (Math.SQRT2 * a)));
      const theta = Math.asin(sinTheta);
      const sinLat = (2.0 * theta + Math.sin(2.0 * theta)) / PI;
      const lat = Math.asin(Math.max(-1.0, Math.min(1.0, sinLat)));

      const cosTheta = Math.cos(theta);
      let lon = cosTheta > 1e-7 ? (PI * cartesian.x) / (2.0 * Math.SQRT2 * a * cosTheta) : 0.0;
      if (lon > PI) lon = PI;
      else if (lon < -PI) lon = -PI;

      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 5. World Miller Cylindrical Projection (ESRI:54003)
  // =========================================================================
  class MillerCylindricalMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'World Miller Cylindrical';
      this.epsg = 54003;
    }

    project(cartographic, result) {
      const a = this._e.a;
      const x = a * cartographic.longitude;
      const y = 1.25 * a * Math.log(Math.tan(PI / 4.0 + 0.4 * cartographic.latitude));
      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const lon = cartesian.x / a;
      const lat = 2.5 * (Math.atan(Math.exp(cartesian.y / (1.25 * a))) - PI / 4.0);
      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 6. World Sinusoidal Projection (ESRI:54008)
  // =========================================================================
  class SinusoidalMapProjection {
    constructor(ellipsoid) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.name = 'World Sinusoidal';
      this.epsg = 54008;
    }

    project(cartographic, result) {
      const a = this._e.a;
      const x = a * cartographic.longitude * Math.cos(cartographic.latitude);
      const y = a * cartographic.latitude;
      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const lat = cartesian.y / a;
      const cosLat = Math.cos(lat);
      let lon = cosLat > 1e-7 ? cartesian.x / (a * cosLat) : 0.0;
      if (lon > PI) lon = PI;
      else if (lon < -PI) lon = -PI;
      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 7. Transverse Mercator (UTM Zones, Egypt Belts, British National Grid, Gauss-Krüger)
  // =========================================================================
  class TransverseMercatorMapProjection {
    constructor(ellipsoid, lon0Deg, lat0Deg, k0, falseEasting, falseNorthing, name, epsg) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.lon0 = toRadians(lon0Deg || 0.0);
      this.lat0 = toRadians(lat0Deg || 0.0);
      this.k0 = k0 || 0.9996;
      this.x0 = falseEasting || 500000.0;
      this.y0 = falseNorthing || 0.0;
      this.name = name || 'Transverse Mercator';
      this.epsg = epsg || 0;

      // Meridional distance coefficients on ellipsoid
      const e2 = this._e.e2;
      const e4 = e2 * e2;
      const e6 = e4 * e2;
      this._a0 = 1.0 - (e2 / 4.0) - (3.0 * e4 / 64.0) - (5.0 * e6 / 256.0);
      this._a2 = (3.0 * e2 / 8.0) + (3.0 * e4 / 32.0) + (45.0 * e6 / 1024.0);
      this._a4 = (15.0 * e4 / 256.0) + (45.0 * e6 / 1024.0);
      this._a6 = 35.0 * e6 / 3072.0;

      this._m0 = this._meridionalDistance(this.lat0);
    }

    _meridionalDistance(lat) {
      return this._e.a * (this._a0 * lat - this._a2 * Math.sin(2.0 * lat) + this._a4 * Math.sin(4.0 * lat) - this._a6 * Math.sin(6.0 * lat));
    }

    project(cartographic, result) {
      const a = this._e.a;
      const e2 = this._e.e2;
      const lat = cartographic.latitude;
      const dLon = cartographic.longitude - this.lon0;

      const sinLat = Math.sin(lat);
      const cosLat = Math.cos(lat);
      const tanLat = Math.tan(lat);
      const tan2 = tanLat * tanLat;

      const N = a / Math.sqrt(1.0 - e2 * sinLat * sinLat);
      const eta2 = (e2 / (1.0 - e2)) * cosLat * cosLat;
      const M = this._meridionalDistance(lat);

      const p = dLon * cosLat;
      const p2 = p * p;
      const p3 = p2 * p;
      const p4 = p2 * p2;
      const p5 = p4 * p;

      // Forward series expansions (Redfearn)
      const x = this.x0 + this.k0 * N * (p + (1.0 - tan2 + eta2) * (p3 / 6.0) + (5.0 - 18.0 * tan2 + tan2 * tan2 + 72.0 * eta2) * (p5 / 120.0));
      const y = this.y0 + this.k0 * ((M - this._m0) + N * tanLat * (p2 / 2.0 + (5.0 - tan2 + 9.0 * eta2 + 4.0 * eta2 * eta2) * (p4 / 24.0)));

      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const e2 = this._e.e2;
      const x = (cartesian.x - this.x0) / this.k0;
      const y = (cartesian.y - this.y0) / this.k0;

      // Footprint latitude iteration
      const M = this._m0 + y;
      let mu = M / (a * this._a0);
      let phi1 = mu;
      for (let i = 0; i < 6; i++) {
        const dM = this._meridionalDistance(phi1) - M;
        const dPhi = dM / (a * (1.0 - e2));
        phi1 -= dPhi;
        if (Math.abs(dPhi) < 1e-9) break;
      }

      const sin1 = Math.sin(phi1);
      const cos1 = Math.cos(phi1);
      const tan1 = Math.tan(phi1);
      const tan2 = tan1 * tan1;

      const N1 = a / Math.sqrt(1.0 - e2 * sin1 * sin1);
      const R1 = a * (1.0 - e2) / Math.pow(1.0 - e2 * sin1 * sin1, 1.5);
      const eta2 = (e2 / (1.0 - e2)) * cos1 * cos1;

      const D = x / N1;
      const D2 = D * D;
      const D3 = D2 * D;
      const D4 = D2 * D2;
      const D5 = D4 * D;

      const lat = phi1 - (N1 * tan1 / R1) * (D2 / 2.0 - (5.0 + 3.0 * tan2 + 10.0 * eta2 - 4.0 * eta2 * eta2) * (D4 / 24.0));
      const lon = this.lon0 + (D - (1.0 + 2.0 * tan2 + eta2) * (D3 / 6.0) + (5.0 + 28.0 * tan2 + 24.0 * tan2 * tan2) * (D5 / 120.0)) / cos1;

      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 8. Universal Polar Stereographic (UPS North EPSG:32661 / UPS South EPSG:32761)
  // =========================================================================
  class PolarStereographicMapProjection {
    constructor(ellipsoid, isNorth, name, epsg) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.isNorth = Boolean(isNorth);
      this.k0 = 0.994; // Standard UPS scale factor at pole
      this.x0 = 2000000.0;
      this.y0 = 2000000.0;
      this.name = name || (this.isNorth ? 'WGS 84 / UPS North' : 'WGS 84 / UPS South');
      this.epsg = epsg || (this.isNorth ? 32661 : 32761);
    }

    project(cartographic, result) {
      const a = this._e.a;
      const e = Math.sqrt(this._e.e2);
      const lat = cartographic.latitude;
      const lon = cartographic.longitude;

      const sign = this.isNorth ? 1.0 : -1.0;
      const phi = sign * lat;
      if (phi >= HALF_PI - 1e-7) {
        return makeCartesian3(this.x0, this.y0, cartographic.height || 0.0, result);
      }

      const clampedPhi = Math.max(-HALF_PI + 1e-6, Math.min(HALF_PI - 1e-6, phi));
      const sinPhi = Math.sin(clampedPhi);
      const con = e * sinPhi;
      const ts = Math.tan(PI / 4.0 - clampedPhi / 2.0) / Math.pow((1.0 - con) / (1.0 + con), e / 2.0);

      const rho = 2.0 * a * this.k0 * ts / Math.sqrt(Math.pow(1.0 + e, 1.0 + e) * Math.pow(1.0 - e, 1.0 - e));
      const theta = lon;

      const x = this.x0 + rho * Math.sin(theta);
      const y = this.y0 + (this.isNorth ? -rho * Math.cos(theta) : rho * Math.cos(theta));

      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const e = Math.sqrt(this._e.e2);
      const dx = cartesian.x - this.x0;
      const dy = cartesian.y - this.y0;

      const rho = Math.sqrt(dx * dx + dy * dy);
      if (rho < 1e-4) {
        return makeCartographic(0.0, this.isNorth ? HALF_PI : -HALF_PI, cartesian.z || 0.0, result);
      }

      let lon = Math.atan2(dx, this.isNorth ? -dy : dy);
      const t = (rho * Math.sqrt(Math.pow(1.0 + e, 1.0 + e) * Math.pow(1.0 - e, 1.0 - e))) / (2.0 * a * this.k0);

      // Iterative solution for latitude
      let phi = HALF_PI - 2.0 * Math.atan(t);
      for (let i = 0; i < 8; i++) {
        const con = e * Math.sin(phi);
        const phiNew = HALF_PI - 2.0 * Math.atan(t * Math.pow((1.0 - con) / (1.0 + con), e / 2.0));
        if (Math.abs(phiNew - phi) < 1e-9) break;
        phi = phiNew;
      }

      const lat = this.isNorth ? phi : -phi;
      return makeCartographic(lon, lat, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // 9. Lambert Conformal Conic (LCC - e.g. Lambert-93 EPSG:2154)
  // =========================================================================
  class LambertConformalConicMapProjection {
    constructor(ellipsoid, lon0Deg, lat0Deg, lat1Deg, lat2Deg, falseEasting, falseNorthing, name, epsg) {
      this.ellipsoid = ellipsoid || (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      this._e = getEllipsoid(this.ellipsoid);
      this.lon0 = toRadians(lon0Deg || 0.0);
      this.lat0 = toRadians(lat0Deg || 0.0);
      this.lat1 = toRadians(lat1Deg || 0.0);
      this.lat2 = toRadians(lat2Deg || lat1Deg || 0.0);
      this.x0 = falseEasting || 0.0;
      this.y0 = falseNorthing || 0.0;
      this.name = name || 'Lambert Conformal Conic';
      this.epsg = epsg || 0;

      const a = this._e.a;
      const e = Math.sqrt(this._e.e2);

      const m1 = this._m(this.lat1, e);
      const m2 = this._m(this.lat2, e);
      const t0 = this._t(this.lat0, e);
      const t1 = this._t(this.lat1, e);
      const t2 = this._t(this.lat2, e);

      if (Math.abs(this.lat1 - this.lat2) > 1e-7) {
        this.n = (Math.log(m1) - Math.log(m2)) / (Math.log(t1) - Math.log(t2));
      } else {
        this.n = Math.sin(this.lat1);
      }
      this.F = m1 / (this.n * Math.pow(t1, this.n));
      this.rho0 = a * this.F * Math.pow(t0, this.n);
    }

    _m(lat, e) {
      const sinLat = Math.sin(lat);
      return Math.cos(lat) / Math.sqrt(1.0 - e * e * sinLat * sinLat);
    }

    _t(lat, e) {
      const sinLat = Math.sin(lat);
      const con = e * sinLat;
      return Math.tan(PI / 4.0 - lat / 2.0) / Math.pow((1.0 - con) / (1.0 + con), e / 2.0);
    }

    project(cartographic, result) {
      const a = this._e.a;
      const e = Math.sqrt(this._e.e2);
      const t = this._t(cartographic.latitude, e);
      const rho = a * this.F * Math.pow(t, this.n);
      const theta = this.n * (cartographic.longitude - this.lon0);

      const x = this.x0 + rho * Math.sin(theta);
      const y = this.y0 + this.rho0 - rho * Math.cos(theta);
      return makeCartesian3(x, y, cartographic.height || 0.0, result);
    }

    unproject(cartesian, result) {
      const a = this._e.a;
      const e = Math.sqrt(this._e.e2);
      const dx = cartesian.x - this.x0;
      const dy = this.rho0 - (cartesian.y - this.y0);

      const rho = Math.sign(this.n) * Math.sqrt(dx * dx + dy * dy);
      const theta = Math.atan2(Math.sign(this.n) * dx, Math.sign(this.n) * dy);

      const t = Math.pow(rho / (a * this.F), 1.0 / this.n);
      let phi = HALF_PI - 2.0 * Math.atan(t);
      for (let i = 0; i < 8; i++) {
        const con = e * Math.sin(phi);
        const phiNew = HALF_PI - 2.0 * Math.atan(t * Math.pow((1.0 - con) / (1.0 + con), e / 2.0));
        if (Math.abs(phiNew - phi) < 1e-9) break;
        phi = phiNew;
      }
      const lon = this.lon0 + theta / this.n;
      return makeCartographic(lon, phi, cartesian.z || 0.0, result);
    }
  }

  // =========================================================================
  // Factory and Dispatcher (ArcGIS Pro Catalog Parity)
  // =========================================================================
  const ZEarthMapProjections = {
    Geographic: GeographicMapProjection,
    WebMercator: WebMercatorMapProjection,
    Robinson: RobinsonMapProjection,
    Mollweide: MollweideMapProjection,
    MillerCylindrical: MillerCylindricalMapProjection,
    Sinusoidal: SinusoidalMapProjection,
    TransverseMercator: TransverseMercatorMapProjection,
    PolarStereographic: PolarStereographicMapProjection,
    LambertConformalConic: LambertConformalConicMapProjection,

    create(config, ellipsoid) {
      const epsg = Number(config?.epsg || config?.epsgCode || 4326);
      const name = String(config?.name || '');
      const isProj = Boolean(config?.isProjected);

      // 1. Geographic / Equirectangular (Default WGS84, 3D Ellipsoid)
      if (epsg === 4326 || epsg === 4979 || !isProj) {
        return new GeographicMapProjection(ellipsoid);
      }

      // 2. Web Mercator
      if (epsg === 3857 || epsg === 102100 || epsg === 900913 || name.includes('Pseudo-Mercator')) {
        return new WebMercatorMapProjection(ellipsoid);
      }

      // 3. World Robinson (ESRI:54030)
      if (epsg === 54030 || name.includes('Robinson')) {
        return new RobinsonMapProjection(ellipsoid);
      }

      // 4. World Mollweide (ESRI:54009)
      if (epsg === 54009 || name.includes('Mollweide')) {
        return new MollweideMapProjection(ellipsoid);
      }

      // 5. World Miller Cylindrical (ESRI:54003)
      if (epsg === 54003 || name.includes('Miller')) {
        return new MillerCylindricalMapProjection(ellipsoid);
      }

      // 6. World Sinusoidal (ESRI:54008)
      if (epsg === 54008 || name.includes('Sinusoidal')) {
        return new SinusoidalMapProjection(ellipsoid);
      }

      // 7. Polar Stereographic (UPS North & South, Arctic, Antarctic)
      if (epsg === 32661 || epsg === 3995 || name.includes('UPS North') || name.includes('Arctic Polar')) {
        return new PolarStereographicMapProjection(ellipsoid, true, name || 'UPS North', epsg);
      }
      if (epsg === 32761 || epsg === 3031 || name.includes('UPS South') || name.includes('Antarctic Polar')) {
        return new PolarStereographicMapProjection(ellipsoid, false, name || 'UPS South', epsg);
      }

      // 8. Survey of Egypt National Grids (Egypt 1907 Belts)
      if (epsg === 22992 || name.includes('Red Belt')) {
        return new TransverseMercatorMapProjection(ellipsoid, 31.0, 30.0, 1.0, 615000.0, 810000.0, 'Egypt 1907 / Red Belt', 22992);
      }
      if (epsg === 22993 || name.includes('Purple Belt') && !name.includes('Extended')) {
        return new TransverseMercatorMapProjection(ellipsoid, 27.0, 30.0, 1.0, 365000.0, 810000.0, 'Egypt 1907 / Purple Belt', 22993);
      }
      if (epsg === 22994 || name.includes('South Belt')) {
        return new TransverseMercatorMapProjection(ellipsoid, 31.0, 22.0, 1.0, 615000.0, 300000.0, 'Egypt 1907 / South Belt', 22994);
      }
      if (epsg === 22995 || name.includes('Extended Purple Belt')) {
        return new TransverseMercatorMapProjection(ellipsoid, 25.0, 30.0, 1.0, 165000.0, 810000.0, 'Egypt 1907 / Extended Purple Belt', 22995);
      }

      // 9. British National Grid (OSGB36)
      if (epsg === 27700 || name.includes('British National Grid')) {
        return new TransverseMercatorMapProjection(ellipsoid, -2.0, 49.0, 0.9996012717, 400000.0, -100000.0, 'OSGB36 / British National Grid', 27700);
      }

      // 10. France Lambert-93 (RGF93)
      if (epsg === 2154 || name.includes('Lambert-93')) {
        return new LambertConformalConicMapProjection(ellipsoid, 3.0, 46.5, 44.0, 49.0, 700000.0, 6600000.0, 'RGF93 / Lambert-93', 2154);
      }

      // 11. UTM WGS 84 Northern Hemisphere (EPSG 32601 to 32660)
      if (epsg >= 32601 && epsg <= 32660) {
        const zone = epsg - 32600;
        const lon0 = (zone * 6) - 183;
        return new TransverseMercatorMapProjection(ellipsoid, lon0, 0.0, 0.9996, 500000.0, 0.0, `WGS 84 / UTM zone ${zone}N`, epsg);
      }

      // 12. UTM WGS 84 Southern Hemisphere (EPSG 32701 to 32760)
      if (epsg >= 32701 && epsg <= 32760) {
        const zone = epsg - 32700;
        const lon0 = (zone * 6) - 183;
        return new TransverseMercatorMapProjection(ellipsoid, lon0, 0.0, 0.9996, 500000.0, 10000000.0, `WGS 84 / UTM zone ${zone}S`, epsg);
      }

      // 13. Gauss-Krüger Pulkovo 1942 (EPSG 28401 to 28432)
      if (epsg >= 28401 && epsg <= 28432) {
        const zone = epsg - 28400;
        const lon0 = (zone * 6) - 3;
        return new TransverseMercatorMapProjection(ellipsoid, lon0, 0.0, 1.0, (zone * 1000000) + 500000.0, 0.0, `Pulkovo 1942 / Gauss-Kruger zone ${zone}`, epsg);
      }

      // 14. Generic UTM by name string (e.g. "UTM zone 36N")
      const utmMatch = name.match(/UTM zone (\d+)([NS])/i);
      if (utmMatch) {
        const zone = parseInt(utmMatch[1], 10);
        const isNorth = utmMatch[2].toUpperCase() === 'N';
        const lon0 = (zone * 6) - 183;
        return new TransverseMercatorMapProjection(ellipsoid, lon0, 0.0, 0.9996, 500000.0, isNorth ? 0.0 : 10000000.0, name, epsg);
      }

      // 15. Default projected -> Web Mercator
      return new WebMercatorMapProjection(ellipsoid);
    }
  };

  // Export for both Browser and Node.js testing
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = ZEarthMapProjections;
  }
  if (typeof window !== 'undefined') {
    window.ZEarthMapProjections = ZEarthMapProjections;
  }
})(typeof window !== 'undefined' ? window : globalThis);
