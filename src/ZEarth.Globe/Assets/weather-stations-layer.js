// Z EARTH Live Global Weather Stations (NOAA AWC METAR)
// Free, live surface observations streamed on-demand.
(function (root) {
  'use strict';

  function createWeatherStationsManager({ Cesium, viewer, onStatus = () => {} }) {
    if (!Cesium || !viewer || !viewer.scene || !viewer.camera) {
      throw new Error('WeatherStationsManager requires Cesium scene and camera.');
    }

    let enabled = false;
    let currentMetric = 'temperature'; // 'temperature' | 'humidity' | 'wind' | 'pressure'
    let isMetricFahrenheit = false;
    let dataSource = null;
    let debounceTimer = null;
    let isFetching = false;
    const stationCache = new Map(); // stationId -> feature object

    function getDataSource() {
      if (!dataSource) {
        dataSource = new Cesium.CustomDataSource('ref_weather_stations');
        viewer.dataSources.add(dataSource);
      }
      return dataSource;
    }

    function calcRelativeHumidity(t, dp) {
      if (t == null || dp == null) return null;
      const a = 17.625, b = 243.04;
      const alpha = (a * t) / (b + t);
      const beta = (a * dp) / (b + dp);
      return Math.min(100, Math.max(0, Math.round(100 * Math.exp(beta - alpha))));
    }

    function getTemperatureColor(tempC) {
      if (tempC == null) return Cesium.Color.fromCssColorString('#94a3b8');
      if (tempC <= 0) return Cesium.Color.fromCssColorString('#38bdf8'); // icy blue
      if (tempC <= 10) return Cesium.Color.fromCssColorString('#2dd4bf'); // teal
      if (tempC <= 20) return Cesium.Color.fromCssColorString('#34d399'); // green
      if (tempC <= 28) return Cesium.Color.fromCssColorString('#fbbf24'); // warm amber
      if (tempC <= 36) return Cesium.Color.fromCssColorString('#f97316'); // orange
      return Cesium.Color.fromCssColorString('#ef4444'); // hot red
    }

    function formatMetricLabel(props) {
      if (!props) return '';
      switch (currentMetric) {
        case 'humidity': {
          const rh = calcRelativeHumidity(props.temp, props.dewp);
          return rh != null ? `${rh}%` : (props.cover || '--');
        }
        case 'wind': {
          if (props.wspd == null) return 'Calm';
          const speedKm = Math.round(props.wspd * 1.852);
          const dir = props.wdir != null ? `${props.wdir}°` : '';
          return `${speedKm} km/h ${dir}`.trim();
        }
        case 'pressure': {
          return props.altim != null ? `${props.altim} hPa` : '--';
        }
        case 'temperature':
        default: {
          if (props.temp == null) return '--';
          if (isMetricFahrenheit) {
            const f = Math.round((props.temp * 9) / 5 + 32);
            return `${f}°F`;
          }
          return `${Math.round(props.temp)}°C`;
        }
      }
    }

    function createWeatherIconSvg(props) {
      const tempC = props.temp ?? 20;
      let badgeColor = '#34d399';
      if (tempC <= 0) badgeColor = '#38bdf8';
      else if (tempC <= 12) badgeColor = '#2dd4bf';
      else if (tempC <= 24) badgeColor = '#34d399';
      else if (tempC <= 32) badgeColor = '#fbbf24';
      else if (tempC <= 38) badgeColor = '#f97316';
      else badgeColor = '#ef4444';

      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
        <circle cx="16" cy="16" r="13" fill="#0f172a" stroke="${badgeColor}" stroke-width="2.5" />
        <circle cx="16" cy="16" r="8" fill="${badgeColor}" />
      </svg>`;
      return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    }

    function createStationDescription(props) {
      const rh = calcRelativeHumidity(props.temp, props.dewp);
      const windSpeedKm = props.wspd != null ? Math.round(props.wspd * 1.852) : null;
      const fahrenheit = props.temp != null ? Math.round((props.temp * 9) / 5 + 32) : null;

      return `
        <div style="font-family: 'Segoe UI', sans-serif; color: #f1f5f9; background: #0f172a; padding: 12px; border-radius: 8px; font-size: 13px; line-height: 1.5; border: 1px solid #334155;">
          <div style="font-size: 15px; font-weight: bold; color: #38bdf8; margin-bottom: 6px; border-bottom: 1px solid #334155; padding-bottom: 4px;">
            ${props.site || props.id} (${props.id})
          </div>
          <table style="width: 100%; border-collapse: collapse; margin-bottom: 10px;">
            <tr><td style="color: #94a3b8; padding: 2px 0;">Temperature:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${props.temp != null ? `${props.temp}°C / ${fahrenheit}°F` : 'N/A'}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Dew Point:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${props.dewp != null ? `${props.dewp}°C` : 'N/A'}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Rel. Humidity:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${rh != null ? `${rh}%` : 'N/A'}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Wind:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${props.wspd != null ? `${windSpeedKm} km/h (${props.wspd} kt)` : 'Calm'} ${props.wdir ? `from ${props.wdir}°` : ''}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Pressure:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${props.altim != null ? `${props.altim} hPa` : 'N/A'}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Clouds / Visib:</td><td style="font-weight: bold; color: #f8fafc; text-align: right;">${props.cover || 'N/A'} ${props.visib ? `• ${props.visib} sm` : ''}</td></tr>
            <tr><td style="color: #94a3b8; padding: 2px 0;">Obs Time (UTC):</td><td style="font-size: 11px; color: #cbd5e1; text-align: right;">${props.obsTime || 'Recent'}</td></tr>
          </table>
          ${props.rawOb ? `<div style="background: #1e293b; padding: 6px; border-radius: 4px; font-family: Consolas, monospace; font-size: 11px; color: #38bdf8; word-break: break-all;">${props.rawOb}</div>` : ''}
          <div style="font-size: 10px; color: #64748b; margin-top: 8px; text-align: right;">Source: NOAA Aviation Weather Center (AWC)</div>
        </div>
      `;
    }

    function renderStation(feature) {
      const ds = getDataSource();
      const props = feature.properties || {};
      const id = props.id || feature.id;
      if (!id || !feature.geometry || !feature.geometry.coordinates) return;

      const lon = feature.geometry.coordinates[0];
      const lat = feature.geometry.coordinates[1];
      const labelText = formatMetricLabel(props);
      const pinColor = getTemperatureColor(props.temp);

      const existing = ds.entities.getById(id);
      if (existing) {
        if (existing.label) existing.label.text = labelText;
        if (existing.point) existing.point.color = pinColor;
        existing.description = createStationDescription(props);
        return;
      }

      ds.entities.add({
        id: id,
        name: `${props.site || id} (${id})`,
        position: Cesium.Cartesian3.fromDegrees(lon, lat, 25),
        point: {
          pixelSize: 10,
          color: pinColor,
          outlineColor: Cesium.Color.WHITE,
          outlineWidth: 2,
          disableDepthTestDistance: Number.POSITIVE_INFINITY,
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 25000000)
        },
        billboard: {
          image: createWeatherIconSvg(props),
          width: 22,
          height: 22,
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          pixelOffset: new Cesium.Cartesian2(0, -6),
          disableDepthTestDistance: Number.POSITIVE_INFINITY,
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 8000000)
        },
        label: {
          text: labelText,
          font: 'bold 12px "Segoe UI", sans-serif',
          fillColor: Cesium.Color.WHITE,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 3,
          style: Cesium.LabelStyle.FILL_AND_OUTLINE,
          pixelOffset: new Cesium.Cartesian2(0, -28),
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          disableDepthTestDistance: Number.POSITIVE_INFINITY,
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 10000000)
        },
        description: createStationDescription(props)
      });
    }

    async function fetchViewportStations() {
      if (!enabled || isFetching) return;
      isFetching = true;

      try {
        let bboxParam = '-180,-85,180,85';
        const rect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
        if (rect) {
          let west = Cesium.Math.toDegrees(rect.west);
          let south = Cesium.Math.toDegrees(rect.south);
          let east = Cesium.Math.toDegrees(rect.east);
          let north = Cesium.Math.toDegrees(rect.north);

          south = Math.max(-85, Math.min(85, south));
          north = Math.max(-85, Math.min(85, north));

          if (east >= west) {
            const marginLon = Math.min(8, (east - west) * 0.15);
            const marginLat = Math.min(8, (north - south) * 0.15);
            west = Math.max(-180, west - marginLon);
            east = Math.min(180, east + marginLon);
            south = Math.max(-85, south - marginLat);
            north = Math.min(85, north + marginLat);
            bboxParam = `${west.toFixed(2)},${south.toFixed(2)},${east.toFixed(2)},${north.toFixed(2)}`;
          }
        }

        const url = `https://aviationweather.gov/api/data/metar?bbox=${bboxParam}&format=geojson`;
        const res = await fetch(url, { headers: { Accept: 'application/geo+json, application/json' } });
        if (!res.ok) throw new Error(`NOAA METAR returned HTTP ${res.status}`);

        const data = await res.json();
        const features = data.features || [];

        for (const feature of features) {
          const id = feature.properties?.id;
          if (id) {
            stationCache.set(id, feature);
            renderStation(feature);
          }
        }

        viewer.scene.requestRender();
        onStatus({
          event: 'weather.stationsUpdated',
          count: features.length,
          totalCached: stationCache.size,
          metric: currentMetric
        });
      } catch (err) {
        console.warn('[WeatherStationsManager] Fetch error:', err);
        onStatus({ event: 'weather.error', message: String(err?.message || err) });
      } finally {
        isFetching = false;
      }
    }

    function onCameraMoveEnd() {
      if (!enabled) return;
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => {
        fetchViewportStations();
      }, 700);
    }

    function setEnabled(val) {
      enabled = Boolean(val);
      const ds = getDataSource();
      ds.show = enabled;

      if (enabled) {
        viewer.camera.moveEnd.addEventListener(onCameraMoveEnd);
        fetchViewportStations();
      } else {
        viewer.camera.moveEnd.removeEventListener(onCameraMoveEnd);
        if (debounceTimer) clearTimeout(debounceTimer);
      }
      viewer.scene.requestRender();
    }

    function setMetric(metric) {
      if (metric === 'fahrenheit') {
        isMetricFahrenheit = true;
        currentMetric = 'temperature';
      } else if (metric === 'celsius') {
        isMetricFahrenheit = false;
        currentMetric = 'temperature';
      } else if (['temperature', 'humidity', 'wind', 'pressure'].includes(metric)) {
        currentMetric = metric;
      }

      // Re-label all existing cached stations
      for (const feature of stationCache.values()) {
        renderStation(feature);
      }
      viewer.scene.requestRender();
    }

    function clear() {
      if (debounceTimer) clearTimeout(debounceTimer);
      if (dataSource) {
        dataSource.entities.removeAll();
        viewer.dataSources.remove(dataSource, true);
        dataSource = null;
      }
      stationCache.clear();
      viewer.scene.requestRender();
    }

    return {
      setEnabled,
      setMetric,
      refresh: () => fetchViewportStations(),
      clear,
      getState: () => ({ enabled, metric: currentMetric, count: stationCache.size })
    };
  }

  root.ZEarthWeatherStations = Object.freeze({
    create: createWeatherStationsManager
  });
})(typeof window !== 'undefined' ? window : globalThis);
