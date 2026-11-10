// Z EARTH offline, illustrative cumulus clouds; not meteorological observations.
(function (root) {
  'use strict';
  function create({ Cesium, viewer, onChange = () => {} }) {
    if (!Cesium || !viewer || !viewer.scene || !viewer.camera)
      throw new Error('Cloud layer requires the Cesium scene and camera.');
    let enabled = false;
    let collection = null;
    let lastAnchor = '';
    const localCloudCount = 64;
    const globalCloudCount = 180;
    const scene = viewer.scene;
    const camera = viewer.camera;

    function is3D() {
      return scene.mode === Cesium.SceneMode.SCENE3D;
    }

    function refresh(force = false) {
      if (!enabled || !collection) return { enabled, rendered: false, count: 0 };
      collection.show = is3D();
      if (!is3D()) {
        scene.requestRender();
        return { enabled, rendered: false, count: collection.length };
      }
      const carto = camera.positionCartographic;
      if (!carto) return { enabled, rendered: true, count: collection.length };
      const lat = Cesium.Math.toDegrees(carto.latitude);
      const lon = Cesium.Math.toDegrees(carto.longitude);
      if (!Number.isFinite(lat) || !Number.isFinite(lon)) return { enabled, rendered: true, count: collection.length };

      const camHeight = Number.isFinite(carto.height) ? carto.height : 50000;
      const isHighAltitude = camHeight > 250000;
      const centerLat = Math.max(-78, Math.min(78, Math.round(lat * 2) / 2));
      const centerLon = Math.round(lon * 2) / 2;
      const anchor = isHighAltitude ? 'global' : `${centerLon}:${centerLat}:low`;

      if (force || lastAnchor !== anchor) {
        collection.removeAll();
        const GOLDEN_ANGLE = 2.399963229728653;
        const maxRadiusDeg = 4.5;
        // From orbital distances, realistic cloud clusters span 1,000-1,800 km and hover in upper troposphere (15-22 km).
        // This forms a continuous, majestic 3D cloud envelope wrapping the Earth, inspired by Google Earth Pro.
        const baseWidth = isHighAltitude ? 1400000 : 38000;
        const baseAltitude = isHighAltitude ? 18000 : 3200;
        const cloudCount = isHighAltitude ? globalCloudCount : localCloudCount;

        for (let i = 0; i < cloudCount; i++) {
          const t = (i + 1) / cloudCount;
          const bearing = i * GOLDEN_ANGLE;
          const ring = Math.sqrt(t) * maxRadiusDeg + 0.15;
          const cosLat = Math.max(0.18, Math.cos(centerLat * Math.PI / 180));
          const latitude = isHighAltitude
            ? Math.asin(1 - 2 * t) * 180 / Math.PI
            : Math.max(-85, Math.min(85, centerLat + Math.sin(bearing) * ring));
          const longitude = isHighAltitude
            ? ((bearing * 180 / Math.PI + 180) % 360 + 360) % 360 - 180
            : ((centerLon + (Math.cos(bearing) * ring) / cosLat + 540) % 360) - 180;

          const sizeVariation = 0.75 + 0.55 * ((i * 7) % 11) / 10;
          const width = baseWidth * sizeVariation;
          const heightOffset = isHighAltitude ? ((i % 5) * 1200) : ((i % 7) * 350);
          const altitude = baseAltitude + heightOffset;

          // Aspect ratio: flat, wispy atmospheric formations rather than bulky spheres
          const heightRatio = isHighAltitude ? 0.22 : 0.28;
          const depthRatio = isHighAltitude ? 0.18 : 0.20;

          collection.add({
            position: Cesium.Cartesian3.fromDegrees(longitude, latitude, altitude),
            scale: new Cesium.Cartesian2(width, width * heightRatio),
            maximumSize: new Cesium.Cartesian3(width, width * heightRatio, width * depthRatio),
            brightness: 0.82 + (i % 5) * 0.02,
            slice: 0.30 + ((i % 5) * 0.08)
          });
        }
        lastAnchor = anchor;
      }
      scene.requestRender();
      return { enabled, rendered: true, count: collection.length };
    }

    function setEnabled(requested) {
      const target = requested === true;
      if (target && typeof Cesium.CloudCollection !== 'function')
        throw new Error('Cesium CloudCollection is unavailable in this build.');
      if (target && !collection) {
        collection = scene.primitives.add(new Cesium.CloudCollection({ noiseDetail: 8 }));
      }
      enabled = target;
      if (collection) collection.show = enabled && is3D();
      const state = enabled ? refresh() : { enabled: false, rendered: false, count: 0 };
      scene.requestRender();
      onChange(state);
      return state;
    }

    if (camera.moveEnd && typeof camera.moveEnd.addEventListener === 'function') {
      camera.moveEnd.addEventListener(() => { if (enabled) refresh(); });
    }
    if (scene.morphComplete && typeof scene.morphComplete.addEventListener === 'function') {
      scene.morphComplete.addEventListener(() => { if (enabled) refresh(true); });
    }
    return { setEnabled, refresh, getState: () => ({ enabled, rendered: enabled && is3D(), count: collection?.length || 0 }) };
  }
  root.ZEarthCloudLayer = Object.freeze({ create });
})(typeof window !== 'undefined' ? window : globalThis);
