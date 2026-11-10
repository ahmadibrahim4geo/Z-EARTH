// Z EARTH World Gazetteer (Mountains, Valleys, Capes, Depressions & Cities)
// Scale-aware Level of Detail (LOD) and bilingual Arabic/English place names.
(function (root) {
  'use strict';

  function createWorldGazetteerManager({ Cesium, viewer, isArabic = false, onStatus = () => {} }) {
    if (!Cesium || !viewer || !viewer.scene || !viewer.camera) {
      throw new Error('WorldGazetteerManager requires Cesium scene and camera.');
    }

    let enabled = false;
    let arabicMode = Boolean(isArabic);
    let dataSource = null;
    let isBuilt = false;

    function getDataSource() {
      if (!dataSource) {
        dataSource = new Cesium.CustomDataSource('ref_world_gazetteer');
        viewer.dataSources.add(dataSource);
      }
      return dataSource;
    }

    function getFeatureStyle(type) {
      switch (type) {
        case 'mountain':
          return { color: Cesium.Color.fromCssColorString('#f59e0b'), pixelSize: 9, icon: '▲' };
        case 'depression':
          return { color: Cesium.Color.fromCssColorString('#06b6d4'), pixelSize: 8, icon: '▼' };
        case 'cape':
        case 'island':
        case 'waterfall':
          return { color: Cesium.Color.fromCssColorString('#10b981'), pixelSize: 8, icon: '◆' };
        case 'city':
        default:
          return { color: Cesium.Color.fromCssColorString('#facc15'), pixelSize: 7, icon: '●' };
      }
    }

    function getDistanceCondition(rank) {
      switch (rank) {
        case 1:
          return new Cesium.DistanceDisplayCondition(0, 35000000); // 35,000 km (Visible at full global eye view)
        case 2:
          return new Cesium.DistanceDisplayCondition(0, 22000000); // 22,000 km
        case 3:
          return new Cesium.DistanceDisplayCondition(0, 12000000); // 12,000 km
        case 4:
        case 5:
          return new Cesium.DistanceDisplayCondition(0, 6000000);  // 6,000 km
        case 6:
          return new Cesium.DistanceDisplayCondition(0, 3000000);  // 3,000 km
        default:
          return new Cesium.DistanceDisplayCondition(0, 1500000);  // 1,500 km
      }
    }

    function localizeType(type) {
      if (arabicMode) {
        switch (type) {
          case 'mountain': return 'جبل / قمة';
          case 'depression': return 'منخفض أرضي';
          case 'cape': return 'رأس جغرافي';
          case 'island': return 'جزيرة';
          case 'waterfall': return 'شلال';
          case 'city': return 'مدينة / عاصمة';
          default: return 'معلم جغرافي';
        }
      } else {
        switch (type) {
          case 'mountain': return 'Mountain / Peak';
          case 'depression': return 'Depression / Basin';
          case 'cape': return 'Cape';
          case 'island': return 'Island';
          case 'waterfall': return 'Waterfall';
          case 'city': return 'City / Capital';
          default: return 'Geographic Landmark';
        }
      }
    }

    function createPlaceDescription(item) {
      const typeLabel = localizeType(item.type);
      const elevFt = item.elevation ? Math.round(item.elevation * 3.28084) : null;

      return `
        <div style="font-family: 'Segoe UI', sans-serif; color: #f1f5f9; background: #0f172a; padding: 12px; border-radius: 8px; font-size: 13px; line-height: 1.5; border: 1px solid #334155;">
          <div style="font-size: 16px; font-weight: bold; color: #f59e0b; margin-bottom: 4px;">
            ${arabicMode ? (item.name_ar || item.name) : (item.name || item.name_ar)}
          </div>
          <div style="font-size: 12px; color: #94a3b8; margin-bottom: 8px; border-bottom: 1px solid #334155; padding-bottom: 4px;">
            ${arabicMode ? item.name : (item.name_ar || '')}
          </div>
          <table style="width: 100%; border-collapse: collapse; margin-bottom: 6px;">
            <tr><td style="color: #94a3b8; padding: 2px 0;">Category:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${typeLabel}</td></tr>
            ${item.elevation ? `<tr><td style="color: #94a3b8; padding: 2px 0;">Elevation:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${item.elevation.toLocaleString()} m (${elevFt.toLocaleString()} ft)</td></tr>` : ''}
            <tr><td style="color: #94a3b8; padding: 2px 0;">Coordinates:</td><td style="font-family: Consolas, monospace; color: #cbd5e1; text-align: right;">${item.lat.toFixed(4)}°, ${item.lon.toFixed(4)}°</td></tr>
          </table>
          <div style="font-size: 10px; color: #64748b; margin-top: 6px; text-align: right;">Source: Natural Earth World Gazetteer (Public Domain)</div>
        </div>
      `;
    }

    function buildEntities() {
      if (isBuilt) return;
      const data = root.ZEARTH_GAZETTEER_DATA || [];
      const ds = getDataSource();

      for (let i = 0; i < data.length; i++) {
        const item = data[i];
        const style = getFeatureStyle(item.type);
        const distCond = getDistanceCondition(item.rank);
        const labelText = arabicMode ? (item.name_ar || item.name) : (item.name || item.name_ar);

        ds.entities.add({
          id: `gazetteer_${i}`,
          name: labelText,
          position: Cesium.Cartesian3.fromDegrees(item.lon, item.lat, (item.elevation || 0) + 15),
          point: {
            pixelSize: style.pixelSize,
            color: style.color,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 1.5,
            disableDepthTestDistance: 0,
            distanceDisplayCondition: distCond
          },
          label: {
            text: labelText,
            font: '11px "Segoe UI", sans-serif',
            fillColor: Cesium.Color.WHITE,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 3,
            style: Cesium.LabelStyle.FILL_AND_OUTLINE,
            verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
            pixelOffset: new Cesium.Cartesian2(0, -10),
            disableDepthTestDistance: 0,
            distanceDisplayCondition: distCond
          },
          description: createPlaceDescription(item)
        });
      }

      isBuilt = true;
    }

    function setEnabled(val) {
      enabled = Boolean(val);
      const ds = getDataSource();
      if (enabled) {
        if (!isBuilt) buildEntities();
        ds.show = true;
      } else {
        ds.show = false;
      }
      viewer.scene.requestRender();
      onStatus({ event: 'gazetteer.toggled', enabled });
    }

    function setLocale(isAr) {
      arabicMode = Boolean(isAr);
      if (!isBuilt || !dataSource) return;

      const data = root.ZEARTH_GAZETTEER_DATA || [];
      const entities = dataSource.entities.values;

      for (let i = 0; i < entities.length; i++) {
        const entity = entities[i];
        const item = data[i];
        if (!item) continue;
        const text = arabicMode ? (item.name_ar || item.name) : (item.name || item.name_ar);
        if (entity.label) entity.label.text = text;
        entity.name = text;
        entity.description = createPlaceDescription(item);
      }

      viewer.scene.requestRender();
    }

    function clear() {
      if (dataSource) {
        dataSource.entities.removeAll();
        viewer.dataSources.remove(dataSource, true);
        dataSource = null;
      }
      isBuilt = false;
      viewer.scene.requestRender();
    }

    return {
      setEnabled,
      setLocale,
      clear,
      getState: () => ({ enabled, count: root.ZEARTH_GAZETTEER_DATA?.length || 0 })
    };
  }

  root.ZEarthWorldGazetteer = Object.freeze({
    create: createWorldGazetteerManager
  });
})(typeof window !== 'undefined' ? window : globalThis);
