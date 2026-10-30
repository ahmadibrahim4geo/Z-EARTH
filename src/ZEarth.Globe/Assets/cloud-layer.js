// Z EARTH offline, illustrative cumulus clouds; not meteorological observations.
(function (root) {
  'use strict';
  function create({ Cesium, viewer, onChange = () => {} }) {
    if (!Cesium || !viewer || !viewer.scene || !viewer.camera)
      throw new Error('Cloud layer requires the Cesium scene and camera.');
    let enabled = false;
    let collection = null;
    let lastAnchor = '';
    const cloudCount = 64;
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
      const anchor = `${centerLon}:${centerLat}:${isHighAltitude ? "high" : "low"}`;

      if (force || lastAnchor !== anchor) {
        collection.removeAll();
        const GOLDEN_ANGLE = 2.399963229728653;
        const maxRadiusDeg = isHighAltitude ? 22.0 : 4.5;
        const baseWidth = isHighAltitude ? 35000 : 12000;
        const baseAltitude = isHighAltitude ? 6000 : 2800;

        for (let i = 0; i < cloudCount; i++) {
          const t = (i + 1) / cloudCount;
          const bearing = i * GOLDEN_ANGLE;
          const ring = Math.sqrt(t) * maxRadiusDeg + 0.15;
          const cosLat = Math.max(0.18, Math.cos(centerLat * Math.PI / 180));
          const latitude = Math.max(-85, Math.min(85, centerLat + Math.sin(bearing) * ring));
          const longitude = ((centerLon + (Math.cos(bearing) * ring) / cosLat + 540) % 360) - 180;

          const sizeVariation = 0.7 + 0.6 * ((i * 7) % 11) / 10;
          const width = baseWidth * sizeVariation;
          const heightOffset = (i % 7) * 450;
          const altitude = baseAltitude + heightOffset;

          collection.add({
            position: Cesium.Cartesian3.fromDegrees(longitude, latitude, altitude),
            scale: new Cesium.Cartesian2(width, width * 0.58),
            maximumSize: new Cesium.Cartesian3(width, width * 0.58, width * 0.42),
            brightness: 0.90 + (i % 5) * 0.02,
            slice: -1.0 + ((i % 4) * 0.15)
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
