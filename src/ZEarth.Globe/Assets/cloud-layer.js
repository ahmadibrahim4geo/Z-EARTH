// Z EARTH offline, illustrative cumulus clouds; not meteorological observations.
(function (root) {
  'use strict';
  function create({ Cesium, viewer, onChange = () => {} }) {
    if (!Cesium || !viewer || !viewer.scene || !viewer.camera)
      throw new Error('Cloud layer requires the Cesium scene and camera.');
    let enabled = false;
    let collection = null;
    let lastAnchor = '';
    const cloudCount = 16;
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
      const centerLat = Math.max(-78, Math.min(78, Math.round(lat * 2) / 2));
      const centerLon = Math.round(lon * 2) / 2;
      const anchor = `${centerLon}:${centerLat}`;
      if (force || lastAnchor !== anchor) {
        collection.removeAll();
        for (let i = 0; i < cloudCount; i++) {
          const bearing = i * 2.399963229728653;
          const ring = 0.10 + 0.085 * Math.sqrt(i + 1);
          const latitude = Math.max(-85, Math.min(85, centerLat + Math.sin(bearing) * ring));
          const longitude = ((centerLon + Math.cos(bearing) * ring / Math.max(0.2, Math.cos(centerLat * Math.PI / 180)) + 540) % 360) - 180;
          const width = 6000 + (i % 5) * 1600;
          collection.add({
            position: Cesium.Cartesian3.fromDegrees(longitude, latitude, 2500 + (i % 4) * 400),
            scale: new Cesium.Cartesian2(width, width * 0.55),
            maximumSize: new Cesium.Cartesian3(width, width * 0.55, width * 0.45),
            brightness: 0.85 + (i % 3) * 0.05,
            slice: -1.0
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
