// Z EARTH Desktop - CesiumJS Geographic Engine
// Lead Architect & Developer: Ahmad Ibrahim
// Official CesiumJS Integration (WGS84 Ellipsoid, Offline/OSM Imagery, 2D/3D Morphing)

(function () {
  'use strict';

  // Configure Cesium base URL for local Workers, Assets, and Widgets
  window.CESIUM_BASE_URL = 'cesium/';

  // Explicitly disable mandatory Cesium ion token
  Cesium.Ion.defaultAccessToken = '';

  const statusEl = document.getElementById('coord-display');
  const compassRing = document.getElementById('compass-ring');
  const btn3D = document.getElementById('btn-3d');
  const btn2D = document.getElementById('btn-2d');

  // Continental Home Anchor (Egypt & Nile Basin Framing)
  const HOME_LON = 30.8025;
  const HOME_LAT = 26.8206;
  const HOME_ALT = 4500000.0; // 4,500 km

  // 1. Initialize Viewer
  const viewer = new Cesium.Viewer('cesium-container', {
    baseLayer: false, // Managed by ImageryManager below
    terrainProvider: new Cesium.EllipsoidTerrainProvider(), // WGS84 Ellipsoid
    animation: false,
    timeline: false,
    geocoder: false,
    homeButton: false,
    sceneModePicker: false,
    baseLayerPicker: false,
    navigationHelpButton: false,
    fullscreenButton: false,
    infoBox: false,
    selectionIndicator: false,
    scene3DOnly: false,
    requestRenderMode: true,
    maximumRenderTimeChange: 0.5,
    msaaSamples: 4
  });

  // Google-Earth-style camera controls: grab with left drag, orbit/tilt with
  // right drag, and zoom toward the pointer with wheel/pinch. Keeping these
  // bindings explicit prevents Cesium's default RIGHT_DRAG zoom from making
  // the globe appear to slide away while the view stays fixed.
  const controller = viewer.scene.screenSpaceCameraController;
  controller.minimumZoomDistance = 20.0; // 20 meters minimum altitude above terrain/ellipsoid
  controller.maximumZoomDistance = 60000000.0; // 60,000 km planetary envelope
  controller.enableCollisionDetection = true;
  controller.enableRotate = true;
  controller.enableTranslate = true;
  controller.enableZoom = true;
  controller.enableTilt = true;
  controller.enableLook = false;
  controller.rotateEventTypes = [Cesium.CameraEventType.LEFT_DRAG];
  controller.translateEventTypes = [Cesium.CameraEventType.LEFT_DRAG];
  controller.zoomEventTypes = [Cesium.CameraEventType.WHEEL, Cesium.CameraEventType.PINCH];
  controller.tiltEventTypes = [
    Cesium.CameraEventType.RIGHT_DRAG,
    Cesium.CameraEventType.MIDDLE_DRAG,
    { eventType: Cesium.CameraEventType.LEFT_DRAG, modifier: Cesium.KeyboardEventModifier.CTRL }
  ];
  controller.inertiaSpin = 0.62;
  controller.inertiaTranslate = 0.62;
  controller.inertiaZoom = 0.42;
  controller.maximumMovementRatio = 0.075;
  controller.bounceAnimationTime = 0.35;
  controller.zoomFactor = 2.0;
  controller.maximumTiltAngle = Cesium.Math.PI_OVER_TWO - Cesium.Math.toRadians(0.5);
  // Always retain trackball/grab behavior at the supported planetary heights.
  controller.minimumTrackBallHeight = controller.maximumZoomDistance;

  // Camera state memory for recovery and stress protection
  let lastSafeCamera = {
    position: Cesium.Cartesian3.fromDegrees(HOME_LON, HOME_LAT, HOME_ALT),
    heading: 0.0,
    pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
    roll: 0.0
  };

  // Horizon & subterranean guard: enforce altitude bounds and recover from NaN singularities
  viewer.scene.postRender.addEventListener(() => {
    const carto = viewer.camera.positionCartographic;
    if (!carto || isNaN(carto.height) || isNaN(carto.latitude) || isNaN(carto.longitude)) {
      console.warn('[Cesium Engine Guard] Invalid camera coordinates detected. Recovering to last safe view.');
      viewer.camera.setView({
        destination: lastSafeCamera.position,
        orientation: {
          heading: lastSafeCamera.heading,
          pitch: lastSafeCamera.pitch,
          roll: lastSafeCamera.roll
        }
      });
      return;
    }

    // Altitude sanity limits
    if (carto.height < 20.0) {
      carto.height = 20.0;
      viewer.camera.position = Cesium.Cartographic.toCartesian(carto);
    } else if (carto.height > 60000000.0) {
      carto.height = 60000000.0;
      viewer.camera.position = Cesium.Cartographic.toCartesian(carto);
    }

    // Record verified safe position
    if (carto.height >= 20.0 && carto.height <= 60000000.0) {
      lastSafeCamera.position = Cesium.Cartesian3.clone(viewer.camera.position);
      lastSafeCamera.heading = viewer.camera.heading;
      lastSafeCamera.pitch = viewer.camera.pitch;
      lastSafeCamera.roll = viewer.camera.roll;
    }
  });

  // 2. Imagery Provider Manager (ZE-048, ZE-049, ZE-052, AC-001, AC-025)
  const ImageryManager = {
    baseLayers: {},
    overlayLayers: new Map(),
    activeBasemap: 'offline-vector',
    showHybridLabels: true,
    graticuleLayer: null,
    hybridLabelsLayer: null,
    // Full provider attribution is available from the compact Credits dialog.
    // Keep no duplicate long text strip inside the viewport.
    attributionEl: null,

    init() {
      // Helper function to register UrlTemplate tile layers with robust error logging
      const registerTileLayer = (id, name, url, attribution, maxLevel = 19) => {
        const provider = new Cesium.UrlTemplateImageryProvider({
          url: url,
          maximumLevel: maxLevel,
          credit: attribution
        });
        const layer = viewer.imageryLayers.addImageryProvider(provider);
        layer.show = false;
        this.baseLayers[id] = {
          id: id,
          name: name,
          layer: layer,
          provider: provider,
          attribution: attribution
        };
        provider.errorEvent.addEventListener((tileError) => {
          console.warn(`[Cesium Tile Error: ${id}]`, tileError);
          sendHostMessage({
            event: 'imagery.error',
            provider: id,
            message: tileError?.message || `Failed to fetch tile for ${name}`,
            timesRetried: tileError?.timesRetried || 0
          });
        });
        return layer;
      };

      // A. High-quality offline Natural Earth II cartographic raster basemap (zero network required - ZE-048, Requirement 7)
      try {
        const neTmsUrl = Cesium.buildModuleUrl('Assets/Textures/NaturalEarthII');
        Cesium.TileMapServiceImageryProvider.fromUrl(neTmsUrl, {
          maximumLevel: 2,
          credit: 'Natural Earth II (Tom Patterson & Public Domain)'
        }).then((neProvider) => {
          // Replace a provisional local grid only after Natural Earth TMS is ready.
          // Preserve the currently selected basemap; a late load cannot steal focus.
          const provisional = this.baseLayers['offline-vector']?.layer;
          const neLayer = viewer.imageryLayers.addImageryProvider(neProvider);
          neLayer.show = (this.activeBasemap === 'offline-vector');
          if (provisional) viewer.imageryLayers.remove(provisional, true);
          this.baseLayers['offline-vector'] = {
            id: 'offline-vector',
            name: 'Natural Earth II (Offline Cartographic)',
            layer: neLayer,
            attribution: 'Natural Earth II (Public Domain)'
          };
          if (this.activeBasemap === 'offline-vector') {
            this.updateAttribution('Natural Earth II (Public Domain)');
          }
        }).catch((err) => {
          console.warn('[Cesium Engine] Natural Earth II TMS fallback to grid:', err);
          this.addFallbackGridLayer();
        });
      } catch (err) {
        console.warn('[Cesium Engine] TileMapService exception, falling back to grid:', err);
        this.addFallbackGridLayer();
      }

      // B. OpenStreetMap online tile layer with mandatory attribution (opt-in / online only)
      registerTileLayer(
        'osm-standard',
        'OpenStreetMap Standard',
        'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
        '© OpenStreetMap contributors',
        19
      );

      registerTileLayer(
        'osm-humanitarian',
        'OpenStreetMap Humanitarian',
        'https://a.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png',
        '© OpenStreetMap contributors • Tiles © HOT/OSM France',
        19
      );

      registerTileLayer(
        'osm-cycling',
        'CyclOSM Cycling Map',
        'https://a.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png',
        '© OpenStreetMap contributors • CyclOSM',
        20
      );

      // C. OpenTopoMap (Topographic Relief)
      registerTileLayer(
        'opentopo',
        'OpenTopoMap',
        'https://tile.opentopomap.org/{z}/{x}/{y}.png',
        'Map data: © OpenStreetMap contributors, SRTM | Map style: © OpenTopoMap',
        17
      );

      // D. CartoDB Dark Matter
      registerTileLayer(
        'carto-dark',
        'CartoDB Dark Matter',
        'https://basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png',
        '© OpenStreetMap, © CARTO',
        19
      );

      // E. CartoDB Positron (Light)
      registerTileLayer(
        'carto-light',
        'CartoDB Positron (Light)',
        'https://basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png',
        '© OpenStreetMap, © CARTO',
        19
      );

      registerTileLayer(
        'carto-voyager',
        'CARTO Voyager',
        'https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png',
        '© OpenStreetMap contributors • © CARTO',
        19
      );

      // F. Esri World Imagery (Satellite Base)
      const esriImageryLayer = registerTileLayer(
        'esri-imagery',
        'Esri World Imagery (No Labels)',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, Maxar, Earthstar Geographics, and GIS User Community',
        19
      );

      // G. Esri World Street Map
      registerTileLayer(
        'esri-streets',
        'Esri World Street Map',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, DeLorme, NAVTEQ, USGS, Intermap, iPC, NRCAN, Esri Japan, METI, TomTom',
        19
      );

      // H. Esri World Topographic Map
      registerTileLayer(
        'esri-topo',
        'Esri World Topographic Map',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, DeLorme, HERE, TomTom, Intermap, GEBCO, USGS, FAO, NPS, NRCAN, GeoBase, IGN, Ordnance Survey',
        19
      );

      // I. Esri Light Gray Canvas
      registerTileLayer(
        'esri-gray',
        'Esri Light Gray Canvas',
        'https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, HERE, DeLorme, MapmyIndia, © OpenStreetMap contributors, and GIS user community',
        16
      );

      // J. Physical Earth, shaded relief, and ocean are complete basemaps,
      // not Reference Layers. Keep them grouped in the Basemap gallery.
      registerTileLayer(
        'esri-physical',
        'Esri World Physical Map',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Physical_Map/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, US National Park Service',
        8
      );
      registerTileLayer(
        'esri-shaded-relief',
        'Esri World Shaded Relief',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Shaded_Relief/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, USGS',
        13
      );
      registerTileLayer(
        'esri-ocean',
        'Esri Ocean & Bathymetry',
        'https://services.arcgisonline.com/ArcGIS/rest/services/Ocean/World_Ocean_Base/MapServer/tile/{z}/{y}/{x}',
        'Source: Esri, GEBCO, NOAA, National Geographic',
        16
      );

      // K. Esri Reference Overlay (Boundaries and Places)
      const esriLabelsProvider = new Cesium.UrlTemplateImageryProvider({
        url: 'https://services.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}',
        maximumLevel: 19,
        credit: 'Esri Reference Overlay'
      });
      const esriLabelsLayer = viewer.imageryLayers.addImageryProvider(esriLabelsProvider);
      esriLabelsLayer.show = false;
      this.hybridLabelsLayer = esriLabelsLayer;

      // L. Esri Hybrid Basemap Definition (Satellite + Reference Overlay)
      this.baseLayers['esri-hybrid'] = {
        id: 'esri-hybrid',
        name: 'Esri World Imagery (Hybrid Labels)',
        layer: esriImageryLayer,
        overlayLayer: esriLabelsLayer,
        attribution: 'Source: Esri, Maxar, Earthstar Geographics, CNES/Airbus DS, USDA, USGS, AeroGRID, IGN, and GIS User Community'
      };

      // Free-first offline default: never initiate online Esri imagery requests
      // until the desktop user explicitly opts into an online basemap.
      this.setBasemap('offline-vector');
    },

    addFallbackGridLayer() {
      if (this.baseLayers['offline-vector']) return;
      const offlineGridProvider = new Cesium.GridImageryProvider({
        color: new Cesium.Color(0.13, 0.66, 0.87, 0.40),
        glowColor: new Cesium.Color(0.1, 0.45, 0.65, 0.20),
        backgroundColor: new Cesium.Color(0.08, 0.18, 0.28, 1.0),
        cells: 8
      });
      const offlineLayer = viewer.imageryLayers.addImageryProvider(offlineGridProvider);
      offlineLayer.show = (this.activeBasemap === 'offline-vector');
      this.baseLayers['offline-vector'] = {
        id: 'offline-vector',
        name: 'Natural Earth Fallback Grid (Offline)',
        layer: offlineLayer,
        attribution: 'Natural Earth Grid / Public Domain'
      };
    },

    registerDynamicBasemap(config) {
      if (!config || !config.id || !config.url) return { success: false, error: 'Invalid dynamic basemap' };
      const id = String(config.id).trim().toLowerCase();
      if (this.baseLayers[id] && this.baseLayers[id].layer) {
        viewer.imageryLayers.remove(this.baseLayers[id].layer, true);
      }
      try {
        const provider = new Cesium.UrlTemplateImageryProvider({
          url: config.url,
          maximumLevel: Number(config.maximumLevel || 23),
          credit: config.attribution || 'Esri World Imagery Wayback'
        });
        const layer = viewer.imageryLayers.addImageryProvider(provider);
        layer.show = false;
        this.baseLayers[id] = { id, name: config.name || id, layer, provider, attribution: config.attribution || 'Esri World Imagery Wayback' };
        if (config.setActive === true) this.setBasemap(id);
        return { success: true, id };
      } catch (e) {
        return { success: false, error: String(e?.message || e) };
      }
    },

    setBasemap(id) {
      if (!id) id = 'offline-vector';
      id = String(id).trim().toLowerCase();
      if (id === 'offline' || id === 'naturalearth') id = 'offline-vector';
      if (id === 'osm' || id === 'openstreetmap') id = 'osm-standard';
      if (id === 'esri' || id === 'satellite' || id === 'hybrid') id = 'esri-hybrid';

      if (!this.baseLayers[id]) {
        console.warn('[ImageryManager] Unknown basemap requested:', id);
        id = 'offline-vector';
      }
      this.activeBasemap = id;

      // If offline-vector requested before TMS loaded, activate grid immediately
      if (id === 'offline-vector' && !this.baseLayers['offline-vector']) {
        this.addFallbackGridLayer();
      }

      // Update visibility of base layers
      for (const [key, base] of Object.entries(this.baseLayers)) {
        if (!base || !base.layer) continue;
        if (key === 'esri-hybrid' || key === 'esri-imagery') {
          // Both rely on esri-imagery satellite base
          const showSat = (id === 'esri-hybrid' || id === 'esri-imagery');
          if (this.baseLayers['esri-imagery'] && this.baseLayers['esri-imagery'].layer) {
            this.baseLayers['esri-imagery'].layer.show = showSat;
          }
        } else {
          base.layer.show = (key === id);
        }
      }

      // Hybrid labels overlay
      if (this.hybridLabelsLayer) {
        this.hybridLabelsLayer.show = (id === 'esri-hybrid' && this.showHybridLabels !== false);
        if (this.hybridLabelsLayer.show) {
          viewer.imageryLayers.raiseToTop(this.hybridLabelsLayer);
        }
      }

      this.updateAttribution();
      CreditsManager.updateActiveBadges(id);
      sendHostMessage({ event: 'imagery.basemapChanged', basemap: id });
    },

    toggleHybridLabels(show) {
      this.showHybridLabels = (show !== false);
      if (this.hybridLabelsLayer) {
        this.hybridLabelsLayer.show = (this.activeBasemap === 'esri-hybrid' && this.showHybridLabels);
        if (this.hybridLabelsLayer.show) {
          viewer.imageryLayers.raiseToTop(this.hybridLabelsLayer);
          if (this.graticuleLayer) viewer.imageryLayers.raiseToTop(this.graticuleLayer);
        }
      }
      sendHostMessage({ event: 'imagery.hybridLabelsToggled', show: this.showHybridLabels });
    },

    setMode(mode) {
      // Compatibility alias
      if (mode === 'offline') this.setBasemap('offline-vector');
      else if (mode === 'osm') this.setBasemap('osm-standard');
      else this.setBasemap(mode);
    },

    addCustomLayer(config) {
      if (!config || !config.id || !config.url) return { success: false, error: 'Invalid config' };
      if (this.overlayLayers.has(config.id)) {
        this.removeLayer(config.id);
      }

      try {
        let provider;
        const normalizedType = String(config.type || config.layerType || 'RasterTile').toLowerCase();
        if (normalizedType === 'wmsserver' || normalizedType === 'wms') {
          provider = new Cesium.WebMapServiceImageryProvider({
            url: config.url,
            layers: config.layers || config.wmsLayer || '0',
            credit: config.attribution || ''
          });
        } else if (normalizedType === 'wmtsserver' || normalizedType === 'wmts') {
          provider = new Cesium.WebMapTileServiceImageryProvider({
            url: config.url,
            layer: config.layers || config.wmtsLayer || '0',
            style: 'default',
            format: 'image/png',
            tileMatrixSetID: config.tileMatrixSetID || 'default028mm',
            credit: config.attribution || ''
          });
        } else {
          let templateUrl = config.url;
          let tilingScheme;
          let maximumLevel = config.maximumLevel;
          if (normalizedType === 'wmtsgibs') {
            const date = config.dateOrTimestamp || new Date().toISOString().slice(0, 10);
            templateUrl = templateUrl
              .replace('{Time}', date)
              .replace('{TileMatrix}', '{z}')
              .replace('{TileRow}', '{y}')
              .replace('{TileCol}', '{x}');
            tilingScheme = new Cesium.GeographicTilingScheme();
            maximumLevel = maximumLevel || 8;
          } else if (normalizedType === 'arcgismapserver' && !templateUrl.includes('{z}')) {
            templateUrl = `${templateUrl.replace(/\/$/, '')}/tile/{z}/{y}/{x}`;
          }
          provider = new Cesium.UrlTemplateImageryProvider({
            url: templateUrl,
            credit: config.attribution || '',
            tilingScheme: tilingScheme,
            maximumLevel: maximumLevel
          });
        }

        const layer = viewer.imageryLayers.addImageryProvider(provider);
        layer.alpha = (typeof config.opacity === 'number') ? config.opacity : 1.0;
        layer.show = (config.visible !== false);

        provider.errorEvent.addEventListener((err) => {
          sendHostMessage({
            event: 'imagery.error',
            layerId: config.id,
            message: err?.message || 'Tile request failed'
          });
        });

        this.overlayLayers.set(config.id, {
          id: config.id,
          name: config.name,
          layer: layer,
          attribution: config.attribution || ''
        });

        this.updateAttribution();
        return { success: true, id: config.id };
      } catch (e) {
        console.error('[ImageryManager] Failed to add custom layer:', e);
        return { success: false, error: e.toString() };
      }
    },

    async addReferenceLayer(config) {
      if (!config || !config.id) return { success: false, error: 'Invalid reference layer' };
      if (config.visible === false) {
        return { success: true, removed: this.removeLayer(config.id), id: config.id };
      }

      const normalizedType = String(config.layerType || config.type || '').toLowerCase();
      if (normalizedType === 'overpassgeojson') {
        this.removeLayer(config.id);
        try {
          const rect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
          if (!rect) throw new Error('Move the globe over the study area, then enable this layer.');
          const west = Cesium.Math.toDegrees(rect.west);
          const east = Cesium.Math.toDegrees(rect.east);
          const south = Cesium.Math.toDegrees(rect.south);
          const north = Cesium.Math.toDegrees(rect.north);
          const lonSpan = east >= west ? east - west : 360 - west + east;
          const latSpan = north - south;
          if (lonSpan > 12 || latSpan > 12) {
            throw new Error('Zoom in to a regional view (under 12°) before loading detailed OpenStreetMap features.');
          }
          const bbox = `${south},${west},${north},${east}`;
          const query = String(config.query || '').replaceAll('{bbox}', bbox);
          if (!query) throw new Error('Reference-layer query is missing.');
          const response = await fetch(`${config.url}?data=${encodeURIComponent(query)}`);
          if (!response.ok) throw new Error(`Overpass returned HTTP ${response.status}`);
          const payload = await response.json();
          const features = [];
          for (const element of (payload.elements || [])) {
            if (!Array.isArray(element.geometry) || element.geometry.length < 2) continue;
            const coordinates = element.geometry.map(p => [p.lon, p.lat]);
            const first = coordinates[0];
            const last = coordinates[coordinates.length - 1];
            const closed = coordinates.length >= 4 && first[0] === last[0] && first[1] === last[1];
            features.push({
              type: 'Feature',
              id: `${element.type}/${element.id}`,
              properties: { ...(element.tags || {}), osm_id: element.id, osm_type: element.type },
              geometry: { type: closed ? 'Polygon' : 'LineString', coordinates: closed ? [coordinates] : coordinates }
            });
          }
          const geojson = { type: 'FeatureCollection', features };
          const source = await Cesium.GeoJsonDataSource.load(geojson, {
            clampToGround: true,
            stroke: Cesium.Color.fromCssColorString('#22c55e'),
            fill: Cesium.Color.fromCssColorString('#22c55e').withAlpha(0.22),
            strokeWidth: 2
          });
          source.name = config.name || config.id;
          await viewer.dataSources.add(source);
          this.overlayLayers.set(config.id, { id: config.id, name: config.name, dataSource: source, attribution: config.attribution || '', opacity: config.opacity ?? 1.0 });
          this.updateAttribution();
          viewer.scene.requestRender();
          return { success: true, id: config.id, featureCount: features.length };
        } catch (e) {
          sendHostMessage({ event: 'imagery.error', layerId: config.id, message: String(e?.message || e) });
          return { success: false, error: String(e?.message || e) };
        }
      }

      if (normalizedType === 'wikimediageosearch' || normalizedType === 'wikipediageosearch') {
        this.removeLayer(config.id);
        try {
          const center = getScreenCenterGround();
          if (!center.valid) throw new Error('Center the globe on a ground location first.');
          const coord = `${center.latitude}|${center.longitude}`;
          let requestUrl;
          if (normalizedType === 'wikimediageosearch') {
            requestUrl = `${config.url}?action=query&generator=geosearch&ggsprimary=all&ggsnamespace=6&ggslimit=75&ggsradius=10000&ggscoord=${encodeURIComponent(coord)}&prop=imageinfo|coordinates&iiprop=url&iiurlwidth=96&format=json&origin=*`;
          } else {
            requestUrl = `${config.url}?action=query&list=geosearch&gscoord=${encodeURIComponent(coord)}&gsradius=10000&gslimit=100&gsnamespace=0&format=json&origin=*`;
          }
          const response = await fetch(requestUrl);
          if (!response.ok) throw new Error(`GeoData API returned HTTP ${response.status}`);
          const payload = await response.json();
          const source = new Cesium.CustomDataSource(config.id);
          const pages = normalizedType === 'wikimediageosearch'
            ? Object.values(payload?.query?.pages || {})
            : (payload?.query?.geosearch || []);
          for (const page of pages) {
            const coordinate = page.coordinates?.[0] || page;
            const lat = Number(coordinate.lat);
            const lon = Number(coordinate.lon);
            if (!Number.isFinite(lat) || !Number.isFinite(lon)) continue;
            const title = String(page.title || 'Nearby place');
            const thumb = page.imageinfo?.[0]?.thumburl;
            source.entities.add({
              name: title,
              position: Cesium.Cartesian3.fromDegrees(lon, lat, 40),
              point: thumb ? undefined : { pixelSize: 9, color: Cesium.Color.fromCssColorString('#38bdf8'), outlineColor: Cesium.Color.WHITE, outlineWidth: 2, heightReference: Cesium.HeightReference.CLAMP_TO_GROUND },
              billboard: thumb ? { image: thumb, width: 48, height: 48, verticalOrigin: Cesium.VerticalOrigin.BOTTOM, heightReference: Cesium.HeightReference.CLAMP_TO_GROUND, disableDepthTestDistance: 0 } : undefined,
              label: { text: title, font: '11px Segoe UI, sans-serif', fillColor: Cesium.Color.WHITE, outlineColor: Cesium.Color.BLACK, outlineWidth: 3, style: Cesium.LabelStyle.FILL_AND_OUTLINE, pixelOffset: new Cesium.Cartesian2(0, thumb ? -54 : -14), distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 600000), disableDepthTestDistance: 0 },
              description: normalizedType === 'wikimediageosearch'
                ? `<a href="https://commons.wikimedia.org/wiki/${encodeURIComponent(title)}">Open on Wikimedia Commons</a>`
                : `<a href="https://en.wikipedia.org/wiki/${encodeURIComponent(title.replaceAll(' ', '_'))}">Open on Wikipedia</a>`
            });
          }
          await viewer.dataSources.add(source);
          this.overlayLayers.set(config.id, { id: config.id, name: config.name, dataSource: source, attribution: config.attribution || '', opacity: config.opacity ?? 1.0 });
          this.updateAttribution();
          viewer.scene.requestRender();
          return { success: true, id: config.id, featureCount: source.entities.values.length };
        } catch (e) {
          sendHostMessage({ event: 'imagery.error', layerId: config.id, message: String(e?.message || e) });
          return { success: false, error: String(e?.message || e) };
        }
      }

      if (normalizedType === 'geojson' || normalizedType === 'vectorgeojson') {
        this.removeLayer(config.id);
        try {
          const source = await Cesium.GeoJsonDataSource.load(config.url, {
            clampToGround: true,
            stroke: Cesium.Color.fromCssColorString('#f97316'),
            fill: Cesium.Color.fromCssColorString('#f97316').withAlpha(0.25),
            strokeWidth: 2,
            markerColor: Cesium.Color.fromCssColorString('#ef4444'),
            markerSize: 18
          });
          source.name = config.name || config.id;
          await viewer.dataSources.add(source);
          source.show = true;
          this.overlayLayers.set(config.id, {
            id: config.id,
            name: config.name,
            dataSource: source,
            attribution: config.attribution || '',
            opacity: typeof config.opacity === 'number' ? config.opacity : 1.0
          });
          this.updateAttribution();
          viewer.scene.requestRender();
          return { success: true, id: config.id };
        } catch (e) {
          sendHostMessage({ event: 'imagery.error', layerId: config.id, message: String(e?.message || e) });
          return { success: false, error: String(e?.message || e) };
        }
      }

      return this.addCustomLayer({
        ...config,
        type: config.layerType || config.type,
        layers: config.wmtsLayer || config.layers
      });
    },

    removeLayer(id) {
      if (this.overlayLayers.has(id)) {
        const item = this.overlayLayers.get(id);
        if (item && item.layer) {
          viewer.imageryLayers.remove(item.layer, true);
        }
        if (item && item.dataSource) {
          viewer.dataSources.remove(item.dataSource, true);
        }
        if (item && item.entity) {
          viewer.entities.remove(item.entity);
        }
        this.overlayLayers.delete(id);
        this.updateAttribution();
        return true;
      }
      return false;
    },

    setLayerVisibility(id, visible) {
      if (this.baseLayers[id]) {
        if (visible) this.setBasemap(id);
        else this.baseLayers[id].layer.show = false;
        return true;
      }
      if (this.overlayLayers.has(id)) {
        const item = this.overlayLayers.get(id);
        if (item.layer) item.layer.show = !!visible;
        if (item.dataSource) item.dataSource.show = !!visible;
        if (item.entity) item.entity.show = !!visible;
        this.updateAttribution();
        return true;
      }
      return false;
    },

    setLayerOpacity(id, opacity) {
      const alpha = Math.max(0.0, Math.min(1.0, opacity));
      if (this.baseLayers[id]) {
        this.baseLayers[id].layer.alpha = alpha;
        return true;
      }
      if (this.overlayLayers.has(id)) {
        const item = this.overlayLayers.get(id);
        if (item.layer) item.layer.alpha = alpha;
        if (item.dataSource) {
          item.opacity = alpha;
          for (const entity of item.dataSource.entities.values) {
            if (entity.billboard) entity.billboard.color = Cesium.Color.WHITE.withAlpha(alpha);
            if (entity.point) entity.point.color = Cesium.Color.fromCssColorString('#ef4444').withAlpha(alpha);
            if (entity.polygon) entity.polygon.material = Cesium.Color.fromCssColorString('#22c55e').withAlpha(alpha * 0.22);
            if (entity.polyline) entity.polyline.material = Cesium.Color.fromCssColorString('#22c55e').withAlpha(alpha);
            if (entity.label) entity.label.fillColor = Cesium.Color.WHITE.withAlpha(alpha);
          }
        }
        return true;
      }
      return false;
    },

    reorderLayer(id, direction) {
      const item = this.overlayLayers.get(id);
      if (!item || !item.layer) return false;
      if (direction === 'up') {
        viewer.imageryLayers.raise(item.layer);
      } else {
        viewer.imageryLayers.lower(item.layer);
      }
      return true;
    },

    setGraticules(visible) {
      GraticuleManager.setConfig({ visible: !!visible });
    },

    updateAttribution() {
      const attributions = [];
      const currentBase = this.baseLayers[this.activeBasemap];
      if (currentBase && currentBase.layer && currentBase.layer.show && currentBase.attribution) {
        attributions.push(currentBase.attribution);
      }
      for (const [_, item] of this.overlayLayers) {
        if (((item.layer && item.layer.show) || (item.dataSource && item.dataSource.show)) && item.attribution) {
          attributions.push(item.attribution);
        }
      }
      const combined = attributions.length > 0 ? attributions.join(' • ') : 'Z EARTH Desktop GIS | Ahmad Ibrahim';
      if (this.attributionEl) {
        this.attributionEl.textContent = combined;
      }
      sendHostMessage({ event: 'imagery.attribution', attribution: combined });
    }
  };

  // NOTE: ImageryManager.init() is deferred to the bottom initialization block. It references
  // CreditsManager (a `const` declared later in this module), so calling it here caused a
  // "Cannot access 'CreditsManager' before initialization" ReferenceError that aborted engine
  // startup (engine.ready was never sent).

  // 2b. Terrain Provider Manager (ZE-050, ZE-051, AC-019)
  const TerrainManager = {
    currentType: 'ellipsoid',
    currentExaggeration: 1.0,
    gridSize: 65,
    tileCache: new Map(),
    lastProbe: null,

    // Check numeric DEM data at the viewport, not the unrelated world root tile.
    async verifyViewportTerrainTile() {
      const ground = getScreenCenterGround();
      const camera = viewer.camera.positionCartographic;
      const lon = ground.valid ? ground.longitude : Cesium.Math.toDegrees(camera.longitude);
      const lat = ground.valid ? ground.latitude : Cesium.Math.toDegrees(camera.latitude);
      if (!Number.isFinite(lon) || !Number.isFinite(lat)) {
        throw new Error('Cannot locate viewport to verify terrain');
      }
      const level = 8, n = 2 ** level;
      const normalized = ((lon + 180) % 360 + 360) % 360;
      const x = Math.min(n - 1, Math.floor(normalized / 360 * n));
      const radians = Cesium.Math.toRadians(Math.max(-85.05112878, Math.min(85.05112878, lat)));
      const mercatorY = (1 - Math.log(Math.tan(radians) + 1 / Math.cos(radians)) / Math.PI) / 2;
      const y = Math.max(0, Math.min(n - 1, Math.floor(mercatorY * n)));
      const heights = await this.loadTerrariumTile(x, y, level);
      let minimumElevationM = Infinity, maximumElevationM = -Infinity, valid = 0;
      for (const elevation of heights) {
        if (!Number.isFinite(elevation)) continue;
        minimumElevationM = Math.min(minimumElevationM, elevation);
        maximumElevationM = Math.max(maximumElevationM, elevation);
        valid++;
      }
      if (valid !== this.gridSize * this.gridSize) {
        throw new Error('Invalid or incomplete numerical DEM samples at viewport');
      }
      this.lastProbe = { level, x, y, sampleCount: valid, minimumElevationM, maximumElevationM };
      return this.lastProbe;
    },

    async loadTerrariumTile(x, y, level) {
      const sourceLevel = level;
      const sourceX = x;
      const sourceY = y;
      const key = `${sourceLevel}/${sourceX}/${sourceY}`;
      if (this.tileCache.has(key)) return this.tileCache.get(key);

      const promise = (async () => {
        const url = `https://terrain.local/terrarium/${sourceLevel}/${sourceX}/${sourceY}.png`;
        const abortController = new AbortController();
        const timeoutId = window.setTimeout(() => abortController.abort(), 25000);
        let response;
        try {
          response = await fetch(url, {
            mode: 'cors',
            cache: 'force-cache',
            signal: abortController.signal
          });
        } finally {
          window.clearTimeout(timeoutId);
        }
        if (!response.ok) throw new Error(`Terrain tile HTTP ${response.status}`);
        const bitmap = await createImageBitmap(await response.blob());
        const canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 256;
        const context = canvas.getContext('2d', { willReadFrequently: true });
        if (!context) throw new Error('Terrain canvas decoder is unavailable');
        context.drawImage(bitmap, 0, 0, 256, 256);
        bitmap.close?.();
        const rgba = context.getImageData(0, 0, 256, 256).data;
        // A full 256x256 mesh creates 65,536 vertices per tile and can stall the
        // globe. Downsample to a regular 65x65 grid while retaining the source
        // tile's edge samples for continuous neighboring terrain.
        const size = this.gridSize;
        const heights = new Float32Array(size * size);
        for (let row = 0; row < size; row++) {
          const sourceRow = Math.round(row * 255 / (size - 1));
          for (let column = 0; column < size; column++) {
            const sourceColumn = Math.round(column * 255 / (size - 1));
            const sourceIndex = (sourceRow * 256 + sourceColumn) * 4;
            heights[row * size + column] =
              (rgba[sourceIndex] * 256 + rgba[sourceIndex + 1] + rgba[sourceIndex + 2] / 256) - 32768;
          }
        }
        return heights;
      })();

      this.tileCache.set(key, promise);
      promise.catch(() => this.tileCache.delete(key));
      if (this.tileCache.size > 160) {
        const oldestKey = this.tileCache.keys().next().value;
        this.tileCache.delete(oldestKey);
      }
      return promise;
    },

    async setProvider(type, url, ionToken) {
      type = type || 'ellipsoid';
      if (ionToken && Cesium.Ion) {
        Cesium.Ion.defaultAccessToken = String(ionToken).trim();
      }
      if (type === 'demTerrain' || type === 'globalDem' || (type === 'localDem' && !url)) {
        try {
          // Do not report Terrain as active until a real elevation tile has passed through
          // the host proxy and decoded successfully.
          // Fetch independently in parallel; both real numeric tiles must pass.
          const [, probe] = await Promise.all([
            this.loadTerrariumTile(0, 0, 0),
            this.verifyViewportTerrainTile()
          ]);
          const demProvider = new Cesium.CustomHeightmapTerrainProvider({
            width: this.gridSize,
            height: this.gridSize,
            tilingScheme: new Cesium.WebMercatorTilingScheme(),
            // Level 14 is already sub-10-metre sampling near the equator. Returning
            // undefined beyond it tells Cesium to retain the parent tile instead of
            // incorrectly repeating the whole parent heightmap in every child tile.
            callback: (x, y, level) => level > 14 ? undefined : this.loadTerrariumTile(x, y, level),
            credit: 'Elevation data © Mapzen/Tilezen and data providers, hosted by AWS Open Data'
          });
          viewer.terrainProvider = demProvider;
          viewer.scene.globe.depthTestAgainstTerrain = true;
          viewer.scene.globe.maximumScreenSpaceError = 1.5;
          if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) {
            viewer.scene.morphTo3D(0.6);
          }
          this.currentType = 'globalDem';
          this.setExaggeration(this.currentExaggeration);
          sendHostMessage({ event: 'terrain.changed', type: 'globalDem', label: 'Global Terrarium Elevation (AWS Open Data)', probe });
          viewer.scene.requestRender();
          return { success: true, type: 'globalDem', label: 'Global Terrarium Elevation (AWS Open Data)', probe };
        } catch (err) {
          console.warn('[TerrainManager] Global elevation provider failed:', err);
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Global elevation unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'worldTerrain') {
        try {
          const activeIonToken = String(ionToken || Cesium.Ion?.defaultAccessToken || '').trim();
          if (!activeIonToken) {
            throw new Error('Cesium ion token is required for Cesium World Terrain');
          }
          if (Cesium.CesiumTerrainProvider?.fromIonAssetId) {
            const tp = await Cesium.CesiumTerrainProvider.fromIonAssetId(1, {
              requestWaterMask: true,
              requestVertexNormals: true
            });
            viewer.terrainProvider = tp;
            viewer.scene.globe.depthTestAgainstTerrain = true;
            if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
            this.currentType = 'worldTerrain';
            this.setExaggeration(this.currentExaggeration);
            sendHostMessage({ event: 'terrain.changed', type: 'worldTerrain' });
            viewer.scene.requestRender();
            return { success: true, type: 'worldTerrain' };
          }
          throw new Error('Cesium World Terrain is not supported by this Cesium build');
        } catch (err) {
          console.warn('[TerrainManager] World terrain failed, falling back to Ellipsoid:', err);
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Terrain unreachable' });
          return { success: false, fallback: 'ellipsoid', error: err.toString() };
        }
      } else if (type === 'worldBathymetry') {
        try {
          const activeIonToken = String(ionToken || Cesium.Ion?.defaultAccessToken || '').trim();
          if (!activeIonToken) {
            throw new Error('Cesium ion token is required for Cesium World Bathymetry');
          }
          let tp;
          if (Cesium.createWorldBathymetryAsync) {
            tp = await Cesium.createWorldBathymetryAsync({
              requestWaterMask: true,
              requestVertexNormals: true
            });
          } else {
            tp = await Cesium.CesiumTerrainProvider.fromIonAssetId(2426648, {
              requestWaterMask: true,
              requestVertexNormals: true
            });
          }
          viewer.terrainProvider = tp;
          viewer.scene.globe.depthTestAgainstTerrain = true;
          if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
          this.currentType = 'worldBathymetry';
          this.setExaggeration(this.currentExaggeration);
          sendHostMessage({ event: 'terrain.changed', type: 'worldBathymetry', label: 'Cesium World Bathymetry / GEBCO 2023' });
          viewer.scene.requestRender();
          return { success: true, type: 'worldBathymetry', label: 'Cesium World Bathymetry / GEBCO 2023' };
        } catch (err) {
          console.warn('[TerrainManager] World bathymetry failed, falling back to Ellipsoid:', err);
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Bathymetry unavailable' });
          return { success: false, fallback: 'ellipsoid', error: err.toString() };
        }
      } else if (type === 'custom' && url) {
        try {
          const tp = await Cesium.CesiumTerrainProvider.fromUrl(url, {
            requestWaterMask: true,
            requestVertexNormals: true
          });
          viewer.terrainProvider = tp;
          viewer.scene.globe.depthTestAgainstTerrain = true;
          if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
          this.currentType = 'custom';
          this.setExaggeration(this.currentExaggeration);
          sendHostMessage({ event: 'terrain.changed', type: 'custom' });
          viewer.scene.requestRender();
          return { success: true, type: 'custom' };
        } catch (err) {
          console.warn('[TerrainManager] Custom terrain failed, falling back to Ellipsoid:', err);
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Custom terrain error' });
          return { success: false, fallback: 'ellipsoid', error: err.toString() };
        }
      } else if (type === 'custom') {
        const error = 'A Cesium terrain service URL is required for Custom Terrain';
        viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
        viewer.scene.globe.depthTestAgainstTerrain = false;
        this.currentType = 'ellipsoid';
        sendHostMessage({ event: 'terrain.fallback', reason: error });
        return { success: false, fallback: 'ellipsoid', error };
      }

      // Default: Ellipsoid
      viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
      viewer.scene.globe.depthTestAgainstTerrain = false;
      this.currentType = 'ellipsoid';
      sendHostMessage({ event: 'terrain.changed', type: 'ellipsoid' });
      return { success: true, type: 'ellipsoid' };
    },

    setExaggeration(factor) {
      let f = typeof factor === 'number' ? factor : 1.0;
      if (f < 0.2) f = 0.2;
      if (f > 10.0) f = 10.0;
      this.currentExaggeration = f;
      if (viewer.scene) {
        viewer.scene.verticalExaggeration = f;
        viewer.scene.verticalExaggerationRelativeHeight = 0.0;
        viewer.scene.requestRender();
      }
      return f;
    },

    resetExaggeration() {
      return this.setExaggeration(1.0);
    }
  };

  // 3. Initial Camera Scene (Egypt & Nile Basin in Continental Context)
  viewer.camera.setView({
    destination: Cesium.Cartesian3.fromDegrees(HOME_LON, HOME_LAT, HOME_ALT),
    orientation: {
      heading: 0.0,
      pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
      roll: 0.0
    }
  });

  // User/project features only. New projects intentionally contain no demo placemarks.
  const defaultPlacemarks = [];

  const placemarkEntities = [];
  let lastPlacemarkSignature = '';

  function isolatedLabelText(value) {
    const text = String(value || '');
    return /[\u0600-\u06ff\u0750-\u077f\u08a0-\u08ff]/.test(text)
      ? `\u2067${text}\u2069`
      : text;
  }

  function getHeightReference(altitudeMode) {
    const mode = String(altitudeMode || 'ClampToGround').toLowerCase();
    if (mode === 'absolute') return Cesium.HeightReference.NONE;
    if (mode === 'relativetoground') return Cesium.HeightReference.RELATIVE_TO_GROUND;
    return Cesium.HeightReference.CLAMP_TO_GROUND;
  }

  function createFeatureSymbolCanvas(shape, color) {
    const canvas = document.createElement('canvas');
    canvas.width = 40;
    canvas.height = 40;
    const ctx = canvas.getContext('2d');
    const css = color?.toCssColorString ? color.toCssColorString() : '#22a8de';
    const kind = String(shape || 'Circle').toLowerCase();
    ctx.fillStyle = css;
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 3;
    ctx.lineJoin = 'round';
    ctx.beginPath();
    if (kind === 'square') {
      ctx.rect(6, 6, 28, 28);
    } else if (kind === 'triangle') {
      ctx.moveTo(20, 4); ctx.lineTo(36, 34); ctx.lineTo(4, 34); ctx.closePath();
    } else if (kind === 'star') {
      for (let i = 0; i < 10; i++) {
        const radius = i % 2 === 0 ? 17 : 7;
        const angle = -Math.PI / 2 + i * Math.PI / 5;
        const x = 20 + Math.cos(angle) * radius;
        const y = 20 + Math.sin(angle) * radius;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath();
    } else if (kind === 'pin') {
      ctx.moveTo(20, 37);
      ctx.bezierCurveTo(17, 30, 7, 24, 7, 15);
      ctx.arc(20, 15, 13, Math.PI, 0, false);
      ctx.bezierCurveTo(33, 24, 23, 30, 20, 37);
      ctx.closePath();
    } else {
      ctx.arc(20, 20, 14, 0, Math.PI * 2);
    }
    ctx.fill();
    ctx.stroke();
    if (kind === 'pin') {
      ctx.fillStyle = '#ffffff';
      ctx.beginPath();
      ctx.arc(20, 15, 4.5, 0, Math.PI * 2);
      ctx.fill();
    }
    return canvas;
  }

  function loadPlacemarks(items) {
    const signature = JSON.stringify(items || []);
    if (signature === lastPlacemarkSignature) return;
    lastPlacemarkSignature = signature;

    viewer.entities.suspendEvents();
    for (const ent of placemarkEntities) {
      viewer.entities.remove(ent);
    }
    placemarkEntities.length = 0;

    for (const p of items) {
      let ent;
      const colorVal = typeof p.color === 'string' ? Cesium.Color.fromCssColorString(p.color) : (p.color || Cesium.Color.fromCssColorString('#22A8DE'));

      if (p.type === 'polygon' && Array.isArray(p.coordinates) && p.coordinates.length >= 3) {
        const outerPositions = p.coordinates.map(c => Cesium.Cartesian3.fromDegrees(c.lon, c.lat, c.alt || 0));
        const holes = [];
        if (Array.isArray(p.innerRings)) {
          for (const ring of p.innerRings) {
            if (ring.length >= 3) {
              holes.push(new Cesium.PolygonHierarchy(ring.map(c => Cesium.Cartesian3.fromDegrees(c.lon, c.lat, c.alt || 0))));
            }
          }
        }

        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          polygon: {
            hierarchy: new Cesium.PolygonHierarchy(outerPositions, holes),
            material: colorVal.withAlpha(Number.isFinite(Number(p.fillOpacity)) ? Math.max(0, Math.min(1, Number(p.fillOpacity))) : 0.4),
            outline: true,
            outlineColor: colorVal,
            outlineWidth: Number(p.outlineWidth || 2),
            heightReference: getHeightReference(p.altitudeMode)
          }
        });
      } else if (p.type === 'path' && Array.isArray(p.coordinates) && p.coordinates.length >= 2) {
        const positions = p.coordinates.map(c => Cesium.Cartesian3.fromDegrees(c.lon, c.lat, c.alt || 0));
        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          polyline: {
            positions: positions,
            width: Number(p.lineWidth || 3),
            material: colorVal,
            clampToGround: String(p.altitudeMode || 'ClampToGround').toLowerCase() !== 'absolute'
          }
        });
      } else if (p.type === 'circle' && Number(p.radiusMeters) > 0) {
        const lon = Number(p.lon || 0);
        const lat = Number(p.lat || 0);
        const radius = Math.max(0.1, Number(p.radiusMeters));
        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          position: Cesium.Cartesian3.fromDegrees(lon, lat, Number(p.alt || 0)),
          ellipse: {
            semiMajorAxis: radius,
            semiMinorAxis: radius,
            material: colorVal.withAlpha(Number.isFinite(Number(p.fillOpacity)) ? Number(p.fillOpacity) : 0.35),
            outline: true,
            outlineColor: colorVal,
            heightReference: getHeightReference(p.altitudeMode)
          }
        });
      } else if (p.type === 'groundOverlay' && p.imageUrl) {
        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          rectangle: {
            coordinates: Cesium.Rectangle.fromDegrees(Number(p.west), Number(p.south), Number(p.east), Number(p.north)),
            material: new Cesium.ImageMaterialProperty({
              image: p.imageUrl,
              transparent: true,
              color: Cesium.Color.WHITE.withAlpha(Number.isFinite(Number(p.opacity)) ? Number(p.opacity) : 1.0)
            })
          }
        });
      } else {
        const lon = p.lon !== undefined ? p.lon : (p.coordinates && p.coordinates[0] ? p.coordinates[0].lon : 0);
        const lat = p.lat !== undefined ? p.lat : (p.coordinates && p.coordinates[0] ? p.coordinates[0].lat : 0);
        const alt = p.alt || 0;
        const symbolShape = String(p.symbolShape || 'Circle');
        const iconImage = typeof p.iconDataUri === 'string' &&
          /^data:image\/(png|jpeg|webp|gif);base64,[a-zA-Z0-9+/=]+$/.test(p.iconDataUri) &&
          p.iconDataUri.length < 1400100 ? p.iconDataUri : null;
        const useCesiumPoint = !iconImage && symbolShape.toLowerCase() === 'circle';

        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          position: Cesium.Cartesian3.fromDegrees(lon, lat, alt),
          point: {
            show: useCesiumPoint,
            pixelSize: 10,
            color: colorVal,
            outlineColor: Cesium.Color.WHITE,
            outlineWidth: 2,
            heightReference: getHeightReference(p.altitudeMode),
            disableDepthTestDistance: 0,
            distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 25000000)
          },
          billboard: useCesiumPoint ? undefined : {
            image: iconImage || createFeatureSymbolCanvas(symbolShape, colorVal),
            width: 30,
            height: 30,
            verticalOrigin: symbolShape.toLowerCase() === 'pin' ? Cesium.VerticalOrigin.BOTTOM : Cesium.VerticalOrigin.CENTER,
            heightReference: getHeightReference(p.altitudeMode),
            disableDepthTestDistance: 0,
            distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 25000000),
            scaleByDistance: new Cesium.NearFarScalar(1000, 1.0, 12000000, 0.55)
          },
          label: {
            show: p.showLabel !== false,
            text: isolatedLabelText(p.name),
            font: `${Number(p.labelSize || 12)}px "Segoe UI", Tahoma, "Noto Sans Arabic", sans-serif`,
            style: Cesium.LabelStyle.FILL_AND_OUTLINE,
            fillColor: Cesium.Color.WHITE,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 3,
            verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
            pixelOffset: new Cesium.Cartesian2(0, -12),
            heightReference: getHeightReference(p.altitudeMode),
            disableDepthTestDistance: 0,
            distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 12000000),
            scaleByDistance: new Cesium.NearFarScalar(1000, 1.0, 12000000, 0.45),
            translucencyByDistance: new Cesium.NearFarScalar(5000000, 1.0, 15000000, 0.0)
          }
        });
      }

      if (ent) {
        ent._zearthId = p.id;
        placemarkEntities.push(ent);
      }
    }
    viewer.entities.resumeEvents();
    viewer.scene.requestRender();
  }

  loadPlacemarks(defaultPlacemarks);

  // 4.5. Spatial Snapping Engine (ZE-042, ZE-043, ZE-044, ZE-045)
  const SnapManager = {
    enabled: true,
    mode: 'GroundDistance',
    tolerancePixels: 12.0,
    groundToleranceMeters: 5.0,
    snapVertices: true,
    snapMidpoints: true,
    snapEdges: true,
    activeSnap: null,

    glyphEl: document.getElementById('snap-glyph'),
    markerEl: document.getElementById('snap-marker'),
    tooltipEl: document.getElementById('snap-tooltip'),

    setConfig: function (cfg) {
      if (!cfg) return;
      if (typeof cfg.enabled === 'boolean') this.enabled = cfg.enabled;
      if (typeof cfg.mode === 'string') this.mode = 'GroundDistance';
      if (typeof cfg.tolerancePixels === 'number' && cfg.tolerancePixels > 0) this.tolerancePixels = cfg.tolerancePixels;
      if (typeof cfg.groundToleranceMeters === 'number' && cfg.groundToleranceMeters > 0) this.groundToleranceMeters = cfg.groundToleranceMeters;
      if (typeof cfg.snapVertices === 'boolean') this.snapVertices = cfg.snapVertices;
      if (typeof cfg.snapMidpoints === 'boolean') this.snapMidpoints = cfg.snapMidpoints;
      if (typeof cfg.snapEdges === 'boolean') this.snapEdges = cfg.snapEdges;
    },

    showGlyph: function (screenX, screenY, type, label) {
      if (!this.glyphEl) {
        this.glyphEl = document.getElementById('snap-glyph');
        this.tooltipEl = document.getElementById('snap-tooltip');
      }
      if (!this.glyphEl) return;
      this.glyphEl.style.left = `${screenX}px`;
      this.glyphEl.style.top = `${screenY}px`;
      this.glyphEl.className = `snap-glyph snap-${type}`;
      this.glyphEl.style.display = 'block';
      if (this.tooltipEl) {
        this.tooltipEl.textContent = label || `Snap: ${type}`;
      }
    },

    hideGlyph: function () {
      if (!this.glyphEl) {
        this.glyphEl = document.getElementById('snap-glyph');
      }
      if (this.glyphEl) {
        this.glyphEl.style.display = 'none';
      }
      this.activeSnap = null;
    },

    collectGeometryCandidates: function () {
      const candidates = { vertices: [], segments: [] };

      function addRing(positions, isClosed = false) {
        if (!positions || positions.length === 0) return;
        for (let i = 0; i < positions.length; i++) {
          candidates.vertices.push(positions[i]);
          if (i > 0) {
            candidates.segments.push({ p1: positions[i - 1], p2: positions[i] });
          }
        }
        if (isClosed && positions.length >= 3) {
          candidates.segments.push({ p1: positions[positions.length - 1], p2: positions[0] });
        }
      }

      // 1. From placed entities in scene
      if (Array.isArray(placemarkEntities)) {
        for (const ent of placemarkEntities) {
          if (ent.position) {
            const pos = ent.position.getValue(viewer.clock.currentTime);
            if (pos) candidates.vertices.push(pos);
          } else if (ent.polyline && ent.polyline.positions) {
            const posList = ent.polyline.positions.getValue(viewer.clock.currentTime);
            if (Array.isArray(posList)) addRing(posList, false);
          } else if (ent.polygon && ent.polygon.hierarchy) {
            const hier = ent.polygon.hierarchy.getValue(viewer.clock.currentTime);
            if (hier && hier.positions) {
              addRing(hier.positions, true);
              if (Array.isArray(hier.holes)) {
                for (const hole of hier.holes) {
                  if (hole && hole.positions) addRing(hole.positions, true);
                }
              }
            }
          }
        }
      }

      // 2. From active drawing vertices
      if (DrawingManager && DrawingManager.vertices && DrawingManager.vertices.length > 0) {
        addRing(DrawingManager.vertices, false);
      }

      // 3. From active measurement vertices
      if (MeasurementManager && MeasurementManager.vertices && MeasurementManager.vertices.length > 0) {
        addRing(MeasurementManager.vertices, false);
      }

      return candidates;
    },

    query: function (screenPos) {
      if (!this.enabled || !screenPos) {
        this.hideGlyph();
        return { snapped: false, cartesian: null };
      }

      const cursorCartesian = pickGroundAtScreenPosition(screenPos);
      if (!cursorCartesian) {
        this.hideGlyph();
        return { snapped: false, cartesian: null };
      }

      const groundDistance = (a, b) => {
        const ca = Cesium.Cartographic.fromCartesian(a);
        const cb = Cesium.Cartographic.fromCartesian(b);
        if (!ca || !cb) return Infinity;
        try {
          const geodesic = new Cesium.EllipsoidGeodesic(ca, cb);
          const surface = geodesic.surfaceDistance;
          const vertical = (cb.height || 0) - (ca.height || 0);
          return Math.sqrt(surface * surface + vertical * vertical);
        } catch (_) {
          return Cesium.Cartesian3.distance(a, b);
        }
      };

      const geom = this.collectGeometryCandidates();
      const tol = this.groundToleranceMeters;
      let bestTarget = null;
      let minDistance = Infinity;

      // 1. Check Vertices (Priority 1)
      if (this.snapVertices) {
        for (const v of geom.vertices) {
          const winPos = Cesium.SceneTransforms.worldToWindowCoordinates(viewer.scene, v);
          if (!winPos) continue;
          const dist = groundDistance(cursorCartesian, v);
          if (dist <= tol && dist < minDistance) {
            minDistance = dist;
            bestTarget = {
              type: 'vertex',
              label: `Vertex • ${dist.toFixed(2)} m`,
              cartesian: v,
              screenX: winPos.x,
              screenY: winPos.y,
              distance: dist
            };
          }
        }

        if (bestTarget) {
          this.activeSnap = bestTarget;
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label);
          return { snapped: true, ...bestTarget };
        }
      }

      // 2. Check Midpoints (Priority 2)
      if (this.snapMidpoints) {
        for (const seg of geom.segments) {
          const midCart = Cesium.Cartesian3.midpoint(seg.p1, seg.p2, new Cesium.Cartesian3());
          const winPos = Cesium.SceneTransforms.worldToWindowCoordinates(viewer.scene, midCart);
          if (!winPos) continue;
          const dist = groundDistance(cursorCartesian, midCart);
          if (dist <= tol && dist < minDistance) {
            minDistance = dist;
            bestTarget = {
              type: 'midpoint',
              label: `Midpoint • ${dist.toFixed(2)} m`,
              cartesian: midCart,
              screenX: winPos.x,
              screenY: winPos.y,
              distance: dist
            };
          }
        }

        if (bestTarget) {
          this.activeSnap = bestTarget;
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label);
          return { snapped: true, ...bestTarget };
        }
      }

      // 3. Check Edge Nearest Projection (Priority 3)
      if (this.snapEdges) {
        for (const seg of geom.segments) {
          const segment = Cesium.Cartesian3.subtract(seg.p2, seg.p1, new Cesium.Cartesian3());
          const fromStart = Cesium.Cartesian3.subtract(cursorCartesian, seg.p1, new Cesium.Cartesian3());
          const lenSq = Cesium.Cartesian3.magnitudeSquared(segment);
          if (lenSq < 1e-4) continue;

          let t = Cesium.Cartesian3.dot(fromStart, segment) / lenSq;
          if (t < 0.0 || t > 1.0) continue; // outside segment

          const projCart = Cesium.Cartesian3.lerp(seg.p1, seg.p2, t, new Cesium.Cartesian3());
          const winPos = Cesium.SceneTransforms.worldToWindowCoordinates(viewer.scene, projCart);
          if (!winPos) continue;
          const dist = groundDistance(cursorCartesian, projCart);

          if (dist <= tol && dist < minDistance) {
            minDistance = dist;
            bestTarget = {
              type: 'edge',
              label: `Edge • ${dist.toFixed(2)} m`,
              cartesian: projCart,
              screenX: winPos.x,
              screenY: winPos.y,
              distance: dist
            };
          }
        }

        if (bestTarget) {
          this.activeSnap = bestTarget;
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label);
          return { snapped: true, ...bestTarget };
        }
      }

      this.hideGlyph();
      return { snapped: false, cartesian: null };
    }
  };

  // 4.6. Interactive Drawing Engine with Snapping Integration (ZE-040, ZE-041, ZE-045)
  const DrawingManager = {
    activeMode: null, // 'point' | 'path' | 'polygon' | 'rectangle' | 'circle'
    vertices: [],
    tempPoints: [],
    previewEntity: null,
    handler: null,
    style: { color: '#22A8DE', width: 3, opacity: 0.4 },

    pickGroundPosition: function (screenPos) {
      const ray = viewer.camera.getPickRay(screenPos);
      if (!ray) return null;
      let pos = viewer.scene.globe.pick(ray, viewer.scene);
      if (!pos) {
        pos = viewer.camera.pickEllipsoid(screenPos, viewer.scene.globe.ellipsoid);
      }
      return pos;
    },

    getRectanglePositions: function (edge) {
      if (!edge || !this.vertices.length) return [];
      const start = Cesium.Cartographic.fromCartesian(this.vertices[0]);
      const finish = Cesium.Cartographic.fromCartesian(edge);
      if (!start || !finish) return [];
      const lon1 = Cesium.Math.toDegrees(start.longitude);
      const lon2 = Cesium.Math.toDegrees(finish.longitude);
      const lat1 = Cesium.Math.toDegrees(start.latitude);
      const lat2 = Cesium.Math.toDegrees(finish.latitude);
      const degrees = globalThis.ZEarthGeometry?.rectangleCorners(lon1, lat1, lon2, lat2);
      return degrees ? Cesium.Cartesian3.fromDegreesArray(degrees) : [];
    },

    start: function (type, styleOptions) {
      this.cancel();
      this.activeMode = type || 'point';
      this.vertices = [];
      this.style = Object.assign({ color: '#22A8DE', width: 3, opacity: 0.4 }, styleOptions);

      document.body.style.cursor = 'crosshair';

      this.handler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);

      // Left Click: Place vertex with snap support (ZE-045)
      this.handler.setInputAction((click) => {
        const snap = SnapManager.query(click.position);
        const cartesian = snap.snapped ? snap.cartesian : this.pickGroundPosition(click.position);
        if (!cartesian) return;

        const carto = Cesium.Cartographic.fromCartesian(cartesian);
        const lon = Cesium.Math.toDegrees(carto.longitude);
        const lat = Cesium.Math.toDegrees(carto.latitude);
        const alt = carto.height;

        if (this.activeMode === 'point') {
          this.vertices.push(cartesian);
          const pointNumber = this.vertices.length;
          const marker = viewer.entities.add({
            position: cartesian,
            point: {
              pixelSize: 24,
              color: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
              outlineColor: Cesium.Color.WHITE,
              outlineWidth: 2,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
              disableDepthTestDistance: 0
            },
            label: {
              text: String(pointNumber),
              font: 'bold 12px "Segoe UI", sans-serif',
              fillColor: Cesium.Color.WHITE,
              outlineColor: Cesium.Color.BLACK,
              outlineWidth: 2,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
              verticalOrigin: Cesium.VerticalOrigin.CENTER,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
              disableDepthTestDistance: 0
            }
          });
          this.tempPoints.push(marker);
          sendHostMessage({
            event: 'draw.completed',
            geometryType: 'point',
            coordinates: [{ longitude: lon, latitude: lat, altitude: alt }],
            color: this.style.color,
            pointNumber: pointNumber,
            continueDrawing: true,
            snapped: snap.snapped,
            snapType: snap.type
          });
          return;
        }

        this.vertices.push(cartesian);

        const ptEnt = viewer.entities.add({
          position: cartesian,
          point: {
            pixelSize: 8,
            color: Cesium.Color.YELLOW,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 2,
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            disableDepthTestDistance: 0
          },
          label: {
            text: String(this.vertices.length),
            font: 'bold 11px "Segoe UI", sans-serif',
            fillColor: Cesium.Color.WHITE,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 2,
            style: Cesium.LabelStyle.FILL_AND_OUTLINE,
            verticalOrigin: Cesium.VerticalOrigin.CENTER,
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            disableDepthTestDistance: 0
          }
        });
        this.tempPoints.push(ptEnt);

        // Two diagonal corners completely define a geographic rectangle.
        if (this.activeMode === 'rectangle' && this.vertices.length === 2) {
          this.finish();
          return;
        }

        if (this.vertices.length === 1) {
          if (this.activeMode === 'path') {
            this.previewEntity = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => this.getDynamicPositions(), false),
                width: this.style.width || 3,
                material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE')
              }
            });
          } else if (this.activeMode === 'polygon') {
            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => new Cesium.PolygonHierarchy(this.getDynamicPositions()), false),
                material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE').withAlpha(this.style.opacity || 0.4),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
                outlineWidth: 2
              }
            });
          } else if (this.activeMode === 'rectangle') {
            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                  const corners = this.getRectanglePositions(currentMousePos);
                  return new Cesium.PolygonHierarchy(corners);
                }, false),
                material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE').withAlpha(this.style.opacity || 0.4),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
          } else if (this.activeMode === 'circle') {
            this.previewEntity = viewer.entities.add({
              position: this.vertices[0],
              ellipse: {
                semiMajorAxis: new Cesium.CallbackProperty(() => this.getCircleRadius(currentMousePos), false),
                semiMinorAxis: new Cesium.CallbackProperty(() => this.getCircleRadius(currentMousePos), false),
                material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE').withAlpha(this.style.opacity || 0.4),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
          }
        }
      }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

      // Mouse Move: Dynamic Rubber-band preview with live snap query
      let currentMousePos = null;
      this.handler.setInputAction((movement) => {
        const snap = SnapManager.query(movement.endPosition);
        const cartesian = snap.snapped ? snap.cartesian : this.pickGroundPosition(movement.endPosition);
        if (cartesian) {
          currentMousePos = cartesian;
        }
      }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

      this.getDynamicPositions = () => {
        if (!currentMousePos || this.vertices.length === 0) return this.vertices;
        return [...this.vertices, currentMousePos];
      };

      // Double Click or Right Click: Finish sketch
      const finishHandler = () => this.finish();
      this.handler.setInputAction(finishHandler, Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
      this.handler.setInputAction(finishHandler, Cesium.ScreenSpaceEventType.RIGHT_CLICK);
    },

    finish: function () {
      SnapManager.hideGlyph();
      if (!this.activeMode || this.vertices.length === 0) {
        this.cancel();
        return;
      }

      if (this.activeMode === 'point') {
        sendHostMessage({ event: 'draw.sessionEnded', geometryType: 'point', pointCount: this.vertices.length });
        this.cancel();
        return;
      }

      const minVertices = this.activeMode === 'polygon' ? 3 : 2;
      if (this.vertices.length < minVertices) {
        this.cancel();
        return;
      }

      // Rectangle is persisted as an ordinary 4-corner polygon plus a shapeKind marker.
      // A flat 2D bbox polygon cannot cross the antimeridian in this initial tool;
      // reject rather than creating a scientifically incorrect spanning polygon.
      const finalVertices = this.activeMode === 'rectangle'
        ? this.getRectanglePositions(this.vertices[1]) : this.vertices;
      if (finalVertices.length < 2) {
        this.cancel();
        return;
      }

      const coords = finalVertices.map((cart) => {
        const carto = Cesium.Cartographic.fromCartesian(cart);
        return {
          longitude: Cesium.Math.toDegrees(carto.longitude),
          latitude: Cesium.Math.toDegrees(carto.latitude),
          altitude: carto.height
        };
      });

      if (this.activeMode === 'circle') {
        const radiusMeters = this.getCircleRadius(this.vertices[1]);
        sendHostMessage({
          event: 'draw.completed',
          geometryType: 'circle',
          coordinates: coords,
          center: coords[0],
          radiusMeters,
          color: this.style.color
        });
      } else {
        sendHostMessage({
          event: 'draw.completed',
          geometryType: this.activeMode === 'rectangle' ? 'polygon' : this.activeMode,
          shapeKind: this.activeMode === 'rectangle' ? 'rectangle' : undefined,
          coordinates: coords,
          color: this.style.color
        });
      }

      this.cancel();
    },

    getCircleRadius: function (edge) {
      if (!edge || this.vertices.length === 0) return 0.1;
      const center = Cesium.Cartographic.fromCartesian(this.vertices[0]);
      const target = Cesium.Cartographic.fromCartesian(edge);
      const geodesic = new Cesium.EllipsoidGeodesic(center, target, Cesium.Ellipsoid.WGS84);
      return Math.max(0.1, geodesic.surfaceDistance || 0.1);
    },

    cancel: function () {
      this.activeMode = null;
      this.vertices = [];
      document.body.style.cursor = 'default';
      SnapManager.hideGlyph();

      if (this.handler) {
        this.handler.destroy();
        this.handler = null;
      }

      if (this.previewEntity) {
        viewer.entities.remove(this.previewEntity);
        this.previewEntity = null;
      }

      for (const ent of this.tempPoints) {
        viewer.entities.remove(ent);
      }
      this.tempPoints = [];
    }
  };

  // 4.7. Geodesic Measurement Engine (ZE-046, ZE-047, AC-018, AC-019, AC-035)
  const MeasurementManager = {
    activeMode: null, // 'distance' | 'area'
    vertices: [],
    tempEntities: [],
    previewEntity: null,
    handler: null,
    lastResult: null,

    hudEl: document.getElementById('measurement-hud'),
    titleEl: document.getElementById('measure-title'),
    bodyEl: document.getElementById('measure-body'),
    algEl: document.getElementById('measure-algorithm'),

    // High-precision WGS84 geodesic distance
    calculateGeodesicDistance: function (c1, c2) {
      const carto1 = Cesium.Cartographic.fromCartesian(c1);
      const carto2 = Cesium.Cartographic.fromCartesian(c2);
      try {
        const geodesic = new Cesium.EllipsoidGeodesic(carto1, carto2, Cesium.Ellipsoid.WGS84);
        return geodesic.surfaceDistance;
      } catch (e) {
        // Fallback Haversine with authalic radius
        const dLat = carto2.latitude - carto1.latitude;
        const dLon = carto2.longitude - carto1.longitude;
        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
          Math.cos(carto1.latitude) * Math.cos(carto2.latitude) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
        return 6371007.2 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
      }
    },

    // Spherical excess on WGS84 authalic sphere
    calculatePolygonArea: function (cartesianList) {
      if (!cartesianList || cartesianList.length < 3) return { area: 0, perimeter: 0 };
      const R = 6371007.181;
      let sum = 0.0;
      let perimeter = 0.0;
      const n = cartesianList.length;

      for (let i = 0; i < n; i++) {
        const p1 = Cesium.Cartographic.fromCartesian(cartesianList[i]);
        const p2 = Cesium.Cartographic.fromCartesian(cartesianList[(i + 1) % n]);
        perimeter += this.calculateGeodesicDistance(cartesianList[i], cartesianList[(i + 1) % n]);

        let dLon = p2.longitude - p1.longitude;
        if (dLon > Math.PI) dLon -= 2 * Math.PI;
        else if (dLon < -Math.PI) dLon += 2 * Math.PI;

        sum += dLon * (2.0 + Math.sin(p1.latitude) + Math.sin(p2.latitude));
      }

      const area = Math.abs(sum / 2.0) * R * R;
      return { area, perimeter };
    },

    formatDistance: function (meters) {
      const unit = String(UnitSettings.distanceUnit || 'AutoMetric').toLowerCase();
      if (unit === 'kilometers') return `${(meters / 1000).toFixed(3)} km`;
      if (unit === 'meters') return `${meters.toFixed(2)} m`;
      if (unit === 'feet') return `${(meters / 0.3048).toFixed(2)} ft`;
      if (unit === 'miles') return `${(meters / 1609.344).toFixed(3)} mi`;
      if (unit === 'nauticalmiles') return `${(meters / 1852).toFixed(3)} nmi`;
      if (unit === 'autoimperial') {
        return meters >= 1609.344
          ? `${(meters / 1609.344).toFixed(3)} mi`
          : `${(meters / 0.3048).toFixed(2)} ft`;
      }
      if (meters >= 1000) {
        return `${(meters / 1000).toFixed(3)} km (${meters.toFixed(1)} m)`;
      }
      return `${meters.toFixed(2)} m`;
    },

    formatArea: function (sqMeters) {
      const unit = String(UnitSettings.areaUnit || 'AutoMetric').toLowerCase();
      if (unit === 'squarekilometers') return `${(sqMeters / 1000000).toFixed(3)} km²`;
      if (unit === 'squaremeters') return `${sqMeters.toFixed(2)} m²`;
      if (unit === 'hectares') return `${(sqMeters / 10000).toFixed(3)} ha`;
      if (unit === 'egyptianfeddan') return `${(sqMeters / 4200.8335).toFixed(3)} fed`;
      if (unit === 'acres') return `${(sqMeters / 4046.8564224).toFixed(3)} ac`;
      if (unit === 'squaremiles') return `${(sqMeters / 2589988.110336).toFixed(3)} mi²`;
      if (unit === 'autoimperial') {
        return sqMeters >= 2589988.110336
          ? `${(sqMeters / 2589988.110336).toFixed(3)} mi²`
          : `${(sqMeters / 4046.8564224).toFixed(3)} ac`;
      }
      if (sqMeters >= 1000000) {
        const kmSq = sqMeters / 1000000;
        const ha = sqMeters / 10000;
        return `${kmSq.toFixed(3)} km² (${ha.toFixed(1)} ha)`;
      }
      if (sqMeters >= 10000) {
        const ha = sqMeters / 10000;
        const acres = sqMeters * 0.000247105;
        return `${sqMeters.toFixed(1)} m² (${ha.toFixed(2)} ha / ${acres.toFixed(2)} ac)`;
      }
      return `${sqMeters.toFixed(2)} m²`;
    },

    toDms: function (value, isLatitude) {
      const absolute = Math.abs(value);
      const degrees = Math.floor(absolute);
      const minuteValue = (absolute - degrees) * 60;
      const minutes = Math.floor(minuteValue);
      const seconds = (minuteValue - minutes) * 60;
      const direction = isLatitude ? (value >= 0 ? 'N' : 'S') : (value >= 0 ? 'E' : 'W');
      return `${degrees}°${minutes}'${seconds.toFixed(2)}"${direction}`;
    },

    toUtm: function (latitude, longitude) {
      const zone = Math.max(1, Math.min(60, Math.floor((longitude + 180) / 6) + 1));
      const a = 6378137.0;
      const eccSquared = 0.00669438;
      const k0 = 0.9996;
      const latRad = Cesium.Math.toRadians(latitude);
      const lonRad = Cesium.Math.toRadians(longitude);
      const longOrigin = (zone - 1) * 6 - 180 + 3;
      const longOriginRad = Cesium.Math.toRadians(longOrigin);
      const eccPrimeSquared = eccSquared / (1 - eccSquared);
      const n = a / Math.sqrt(1 - eccSquared * Math.sin(latRad) ** 2);
      const t = Math.tan(latRad) ** 2;
      const c = eccPrimeSquared * Math.cos(latRad) ** 2;
      const aa = Math.cos(latRad) * (lonRad - longOriginRad);
      const m = a * ((1 - eccSquared / 4 - 3 * eccSquared ** 2 / 64 - 5 * eccSquared ** 3 / 256) * latRad
        - (3 * eccSquared / 8 + 3 * eccSquared ** 2 / 32 + 45 * eccSquared ** 3 / 1024) * Math.sin(2 * latRad)
        + (15 * eccSquared ** 2 / 256 + 45 * eccSquared ** 3 / 1024) * Math.sin(4 * latRad)
        - (35 * eccSquared ** 3 / 3072) * Math.sin(6 * latRad));
      const easting = k0 * n * (aa + (1 - t + c) * aa ** 3 / 6 +
        (5 - 18 * t + t ** 2 + 72 * c - 58 * eccPrimeSquared) * aa ** 5 / 120) + 500000;
      let northing = k0 * (m + n * Math.tan(latRad) * (aa ** 2 / 2 +
        (5 - t + 9 * c + 4 * c ** 2) * aa ** 4 / 24 +
        (61 - 58 * t + t ** 2 + 600 * c - 330 * eccPrimeSquared) * aa ** 6 / 720));
      if (latitude < 0) northing += 10000000;
      return `${zone}${latitude >= 0 ? 'N' : 'S'} ${easting.toFixed(2)}E ${northing.toFixed(2)}N`;
    },

    buildVertexTable: function (positions) {
      if (!positions || positions.length === 0) return '';
      let previous = null;
      const rows = positions.map((position, index) => {
        const carto = Cesium.Cartographic.fromCartesian(position);
        const lat = Cesium.Math.toDegrees(carto.latitude);
        const lon = Cesium.Math.toDegrees(carto.longitude);
        const segment = previous ? this.formatDistance(this.calculateGeodesicDistance(previous, position)) : '—';
        previous = position;
        return `<tr><td>${index + 1}</td><td>${lon.toFixed(6)}, ${lat.toFixed(6)}</td>` +
          `<td>${this.toDms(lon, false)} ${this.toDms(lat, true)}</td>` +
          `<td>${this.toUtm(lat, lon)}</td><td>${segment}</td></tr>`;
      }).join('');
      return `<div style="max-height:150px;overflow:auto;margin-top:7px"><table class="measure-coord-table">` +
        `<thead><tr><th>#</th><th>DD (X,Y)</th><th>DMS</th><th>UTM</th><th>Segment</th></tr></thead>` +
        `<tbody>${rows}</tbody></table></div><button id="btn-copy-measure-coordinates" class="measure-copy-btn">Copy coordinates</button>`;
    },

    updateHUD: function (candidateCartesian = null) {
      if (!this.hudEl) {
        this.hudEl = document.getElementById('measurement-hud');
        this.titleEl = document.getElementById('measure-title');
        this.bodyEl = document.getElementById('measure-body');
        this.algEl = document.getElementById('measure-algorithm');
      }
      if (!this.hudEl || !this.activeMode) return;

      const activeList = candidateCartesian ? [...this.vertices, candidateCartesian] : this.vertices;

      if (this.activeMode === 'distance') {
        this.titleEl.textContent = '📏 Geodesic Distance Measurement';
        if (activeList.length < 2) {
          this.bodyEl.textContent = `Click to add points. Ground snap active (${SnapManager.groundToleranceMeters} m).`;
        } else {
          let totalHoriz = 0.0;
          let totalSlope = 0.0;
          let deltaZ = 0.0;

          for (let i = 0; i < activeList.length - 1; i++) {
            const hDist = this.calculateGeodesicDistance(activeList[i], activeList[i + 1]);
            const cartoA = Cesium.Cartographic.fromCartesian(activeList[i]);
            const cartoB = Cesium.Cartographic.fromCartesian(activeList[i + 1]);
            const dz = Math.abs(cartoB.height - cartoA.height);
            const sDist = Math.sqrt(hDist * hDist + dz * dz);

            totalHoriz += hDist;
            totalSlope += sDist;
          }

          const firstCarto = Cesium.Cartographic.fromCartesian(activeList[0]);
          const lastCarto = Cesium.Cartographic.fromCartesian(activeList[activeList.length - 1]);
          deltaZ = Math.abs(lastCarto.height - firstCarto.height);
          const slopePct = totalHoriz > 1e-4 ? (deltaZ / totalHoriz) * 100 : 0;
          const slopeAngle = totalHoriz > 1e-4 ? Math.atan2(deltaZ, totalHoriz) * (180 / Math.PI) : 0;

          this.bodyEl.innerHTML = `
            <strong>Total Distance:</strong> ${this.formatDistance(totalHoriz)}<br/>
            <strong>3D Slope Distance:</strong> ${this.formatDistance(totalSlope)}<br/>
            <strong>Elevation ΔZ:</strong> ${deltaZ.toFixed(1)} m | <strong>Slope:</strong> ${slopePct.toFixed(1)}% (${slopeAngle.toFixed(1)}°)<br/>
            <strong>Points:</strong> ${activeList.length}
          `;

          this.lastResult = {
            mode: 'distance',
            horizontalDistanceMeters: totalHoriz,
            slopeDistanceMeters: totalSlope,
            elevationDeltaMeters: deltaZ,
            slopePercent: slopePct,
            slopeAngleDegrees: slopeAngle,
            pointCount: activeList.length
          };
        }
        this.algEl.textContent = 'Algorithm: WGS84 Ellipsoidal Geodesic (Vincenty / EllipsoidGeodesic)';
      } else if (this.activeMode === 'area') {
        this.titleEl.textContent = '⬢ Surface Area & Perimeter Measurement';
        if (activeList.length < 3) {
          this.bodyEl.textContent = `Click at least 3 points to form a polygon. Current vertices: ${activeList.length}`;
        } else {
          const { area, perimeter } = this.calculatePolygonArea(activeList);
          this.bodyEl.innerHTML = `
            <strong>Geodesic Area:</strong> ${this.formatArea(area)}<br/>
            <strong>Perimeter:</strong> ${this.formatDistance(perimeter)}<br/>
            <strong>Vertices:</strong> ${activeList.length}
          `;

          this.lastResult = {
            mode: 'area',
            areaSquareMeters: area,
            perimeterMeters: perimeter,
            pointCount: activeList.length
          };
        }
        this.algEl.textContent = 'Algorithm: WGS84 Authalic Spherical Excess';
      }

      if (activeList.length > 0 && this.bodyEl) {
        this.bodyEl.insertAdjacentHTML('beforeend', this.buildVertexTable(activeList));
        this.bodyEl.querySelector('.measure-copy-btn')?.addEventListener('click', () => {
          const lines = activeList.map((position, index) => {
            const c = Cesium.Cartographic.fromCartesian(position);
            const lat = Cesium.Math.toDegrees(c.latitude);
            const lon = Cesium.Math.toDegrees(c.longitude);
            return `${index + 1}\t${lon.toFixed(8)}\t${lat.toFixed(8)}\t${this.toUtm(lat, lon)}`;
          });
          navigator.clipboard?.writeText(`Point\tLongitude\tLatitude\tUTM\n${lines.join('\n')}`);
        }, { once: true });
      }

      this.hudEl.style.display = 'block';

      if (this.lastResult) {
        sendHostMessage({
          event: 'measurement.updated',
          ...this.lastResult
        });
      }
    },

    start: function (type, options) {
      this.clear();
      this.activeMode = type || 'distance';
      this.vertices = [];
      document.body.style.cursor = 'crosshair';

      this.updateHUD();

      this.handler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);

      // LEFT_CLICK: Add measurement vertex with snapping! (ZE-045, AC-018)
      this.handler.setInputAction((click) => {
        const snap = SnapManager.query(click.position);
        let cartesian = snap.snapped ? snap.cartesian : DrawingManager.pickGroundPosition(click.position);
        if (!cartesian) return;

        this.vertices.push(cartesian);

        // Visual vertex point
        const pt = viewer.entities.add({
          position: cartesian,
          point: {
            pixelSize: 22,
            color: Cesium.Color.fromCssColorString('#334155'),
            outlineColor: Cesium.Color.fromCssColorString('#b8c5d1'),
            outlineWidth: 2,
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            disableDepthTestDistance: 0
          },
          label: {
            text: String(this.vertices.length),
            font: 'bold 11px "Segoe UI", sans-serif',
            fillColor: Cesium.Color.WHITE,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 2,
            style: Cesium.LabelStyle.FILL_AND_OUTLINE,
            horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
            verticalOrigin: Cesium.VerticalOrigin.CENTER,
            pixelOffset: new Cesium.Cartesian2(0, 0),
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            disableDepthTestDistance: 0
          }
        });
        this.tempEntities.push(pt);

        // Dynamic rubber-band entity
        if (this.vertices.length === 1) {
          if (this.activeMode === 'distance') {
            this.previewEntity = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => this.getDynamicPositions(), false),
                width: 3,
                material: new Cesium.PolylineGlowMaterialProperty({
                  glowPower: 0.25,
                  color: Cesium.Color.fromCssColorString('#38bdf8')
                })
              }
            });
            this.tempEntities.push(this.previewEntity);
          } else if (this.activeMode === 'area') {
            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => new Cesium.PolygonHierarchy(this.getDynamicPositions()), false),
                material: Cesium.Color.fromCssColorString('#34d399').withAlpha(0.35),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString('#34d399'),
                outlineWidth: 2
              }
            });
            this.tempEntities.push(this.previewEntity);
          }
        }

        this.updateHUD();
      }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

      // MOUSE_MOVE: Live snapping & rubber-banding
      let currentMousePos = null;
      this.handler.setInputAction((movement) => {
        const snap = SnapManager.query(movement.endPosition);
        currentMousePos = snap.snapped ? snap.cartesian : DrawingManager.pickGroundPosition(movement.endPosition);
        if (this.vertices.length > 0 && currentMousePos) {
          this.updateHUD(currentMousePos);
        }
      }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

      this.getDynamicPositions = () => {
        if (!currentMousePos || this.vertices.length === 0) return this.vertices;
        return [...this.vertices, currentMousePos];
      };

      // Finish triggers
      const finishFn = () => this.finish();
      this.handler.setInputAction(finishFn, Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
      this.handler.setInputAction(finishFn, Cesium.ScreenSpaceEventType.RIGHT_CLICK);
    },

    finish: function () {
      if (!this.activeMode || this.vertices.length === 0) {
        this.clear();
        return;
      }
      SnapManager.hideGlyph();
      if (this.handler) {
        this.handler.destroy();
        this.handler = null;
      }
      document.body.style.cursor = 'default';

      this.updateHUD();
      if (this.lastResult) {
        sendHostMessage({
          event: 'measurement.completed',
          ...this.lastResult,
          coordinates: this.vertices.map(v => {
            const c = Cesium.Cartographic.fromCartesian(v);
            return {
              latitude: Cesium.Math.toDegrees(c.latitude),
              longitude: Cesium.Math.toDegrees(c.longitude),
              altitude: c.height
            };
          })
        });
      }
    },

    clear: function () {
      this.activeMode = null;
      this.vertices = [];
      this.lastResult = null;
      document.body.style.cursor = 'default';
      SnapManager.hideGlyph();

      if (this.handler) {
        this.handler.destroy();
        this.handler = null;
      }

      for (const ent of this.tempEntities) {
        viewer.entities.remove(ent);
      }
      this.tempEntities = [];
      this.previewEntity = null;

      if (!this.hudEl) {
        this.hudEl = document.getElementById('measurement-hud');
      }
      if (this.hudEl) {
        this.hudEl.style.display = 'none';
      }
    }
  };

  function pickGroundAtScreenPosition(screenPosition) {
    if (!screenPosition) return null;
    if (viewer.scene.mode === Cesium.SceneMode.SCENE2D) {
      return viewer.camera.pickEllipsoid(screenPosition, viewer.scene.globe.ellipsoid) || null;
    }
    const ray = viewer.camera.getPickRay(screenPosition);
    let position = ray && viewer.scene.globe
      ? viewer.scene.globe.pick(ray, viewer.scene)
      : null;
    if (!position) {
      position = viewer.camera.pickEllipsoid(screenPosition, viewer.scene.globe.ellipsoid);
    }
    return position || null;
  }

  function getScreenCenterGroundCartesian() {
    const canvas = viewer.canvas;
    if (!canvas || canvas.clientWidth === 0 || canvas.clientHeight === 0) return null;
    return pickGroundAtScreenPosition(
      new Cesium.Cartesian2(canvas.clientWidth / 2, canvas.clientHeight / 2));
  }

  // Ground intersection under viewport screen center (NAV-03, CRS-01)
  function getScreenCenterGround() {
    const canvas = viewer.canvas;
    if (!canvas || canvas.clientWidth === 0 || canvas.clientHeight === 0) {
      return { valid: false, latitude: 0, longitude: 0, altitude: 0 };
    }
    const pos = getScreenCenterGroundCartesian();
    if (pos) {
      const c = Cesium.Cartographic.fromCartesian(pos);
      if (!isNaN(c.latitude) && !isNaN(c.longitude)) {
        return {
          valid: true,
          latitude: Cesium.Math.toDegrees(c.latitude),
          longitude: Cesium.Math.toDegrees(c.longitude),
          altitude: isNaN(c.height) ? 0 : c.height
        };
      }
    }
    return { valid: false, latitude: 0, longitude: 0, altitude: 0 };
  }

  // Dedicated Geographic Graticules & Key Parallels (GRID-01, GRID-02, GRID-03)
  const GraticuleManager = {
    dataSource: null,
    lastViewKey: '',
    config: {
      visible: false,
      showLabels: true,
      showKeyParallels: true,
      showUtmZones: false,
      showLongitudeLines: true,
      showLatitudeLines: true,
      longitudeIntervalDegrees: 0,
      latitudeIntervalDegrees: 0,
      gridLabelFormat: "DMS"
    },

    init() {
      this.dataSource = new Cesium.CustomDataSource('geographic-graticules');
      this.dataSource.show = false;
      viewer.dataSources.add(this.dataSource);
      this.buildGraticules();
    },

    buildGraticules() {
      this.dataSource.entities.removeAll();
      // An explicit data-source visibility gate also hides pending primitives.
      this.dataSource.show = this.config.visible;
      if (!this.config.visible) {
        viewer.scene.requestRender();
        return;
      }

      const viewRect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      const full = { west: -180, east: 180, south: -85, north: 85 };
      let bounds = full;
      if (viewRect) {
        bounds = {
          west: Cesium.Math.toDegrees(viewRect.west),
          east: Cesium.Math.toDegrees(viewRect.east),
          south: Math.max(-85, Cesium.Math.toDegrees(viewRect.south)),
          north: Math.min(85, Cesium.Math.toDegrees(viewRect.north))
        };
      }

      const longitudeWindows = window.ZEarthGraticuleGeometry.longitudeWindows(bounds);
      const lonSpan = Math.max(0.01, longitudeWindows.reduce((sum, w) => sum + (w.east - w.west), 0));
      const latSpan = Math.max(0.01, bounds.north - bounds.south);
      // Previous descending candidates chose 60° almost everywhere.
      const candidates = [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60];
      const baseLon = this.config.longitudeIntervalDegrees > 0
        ? Math.max(0.05, this.config.longitudeIntervalDegrees)
        : (candidates.find(v => lonSpan / v <= 10) || 60);
      const baseLat = this.config.latitudeIntervalDegrees > 0
        ? Math.max(0.05, this.config.latitudeIntervalDegrees)
        : (candidates.find(v => latSpan / v <= 10) || 60);
      // Avoid thousands of line entities at global scale for fine user spacing.
      const lonInterval = baseLon * Math.max(1, Math.ceil(lonSpan / (baseLon * 120)));
      const latInterval = baseLat * Math.max(1, Math.ceil(latSpan / (baseLat * 120)));
      let centerLon = bounds.west + lonSpan / 2;
      if (centerLon > 180) centerLon -= 360;
      const centerLat = (bounds.south + bounds.north) / 2;

      const labelOutline = Cesium.Color.fromCssColorString('#0F172A');
      const stdLineColor = Cesium.Color.fromCssColorString('rgba(56, 189, 248, 0.40)');
      const keyLineColor = Cesium.Color.fromCssColorString('rgba(251, 191, 36, 0.85)');
      const equatorColor = Cesium.Color.fromCssColorString('rgba(239, 68, 68, 0.90)');
      const primeColor = Cesium.Color.fromCssColorString('rgba(59, 130, 246, 0.90)');
      const utmColor = Cesium.Color.fromCssColorString('rgba(168, 85, 247, 0.50)');

      // Independent key geographic reference lines and points.
      if (this.config.showKeyParallels) {
        const names = this.config.showLabels;
        this.addParallel(0, equatorColor, 2.5, 'Equator 0°', names, centerLon);
        this.addParallel(23.4365, keyLineColor, 2, 'Tropic of Cancer 23.44° N', names, centerLon);
        this.addParallel(-23.4365, keyLineColor, 2, 'Tropic of Capricorn 23.44° S', names, centerLon);
        this.addParallel(66.5635, keyLineColor, 1.8, 'Arctic Circle 66.56° N', names, centerLon);
        this.addParallel(-66.5635, keyLineColor, 1.8, 'Antarctic Circle 66.56° S', names, centerLon);
        this.addMeridian(0, primeColor, 2.5, 'Greenwich / Prime Meridian 0°', names, centerLat);
        this.addMeridian(180, primeColor, 2, 'International Date Line 180°', names, centerLat);
        this.addKeyPoint(0, 89.999, 'North Pole 90° N', keyLineColor);
        this.addKeyPoint(0, -89.999, 'South Pole 90° S', keyLineColor);
        this.addKeyPoint(0, 0, 'Grid Origin 0°, 0°', equatorColor);
      }

      if (this.config.showLatitudeLines) {
        const startLat = Math.ceil(bounds.south / latInterval) * latInterval;
        const endLat = Math.floor(bounds.north / latInterval) * latInterval;
        for (let lat = startLat; lat <= endLat + latInterval * 0.1; lat += latInterval) {
          if ((this.config.showKeyParallels &&
              [0, 23.4365, -23.4365, 66.5635, -66.5635].some(v => Math.abs(lat - v) < 1e-6)) ||
              Math.abs(lat) >= 89.9) continue;
          this.addParallel(lat, stdLineColor, 1,
            window.ZEarthGraticuleLabels.formatDegrees(lat, 'lat', this.config.gridLabelFormat),
            this.config.showLabels, centerLon);
        }
      }

      if (this.config.showLongitudeLines) {
        for (const windowBounds of longitudeWindows) {
          const startLon = Math.ceil(windowBounds.west / lonInterval) * lonInterval;
          const endLon = Math.floor(windowBounds.east / lonInterval) * lonInterval;
          for (let lon = startLon; lon <= endLon + lonInterval * 0.1; lon += lonInterval) {
            if (this.config.showKeyParallels &&
                (Math.abs(lon) < 1e-6 || Math.abs(Math.abs(lon) - 180) < 1e-6)) continue;
            this.addMeridian(lon, stdLineColor, 1,
              window.ZEarthGraticuleLabels.formatDegrees(lon, 'lon', this.config.gridLabelFormat),
              this.config.showLabels, centerLat);
          }
        }
      }

      // UTM bands: latitude -80..84, zone 1..60, and Norway/Svalbard exceptions.
      // Labels are attached to the visible zone, not hardcoded to the Equator.
      if (this.config.showUtmZones) {
        const segments = window.ZEarthGraticuleGeometry.visibleUtmSegments(bounds);
        const showZoneLabels = this.config.showLabels && lonSpan <= 45 && latSpan <= 36;
        for (const segment of segments) {
          // Never draw an extra UTM boundary over a major or ordinary meridian.
          const isMajor = this.config.showKeyParallels &&
            (Math.abs(segment.west) < 1e-6 || Math.abs(Math.abs(segment.west) - 180) < 1e-6);
          const regularOverlap = this.config.showLongitudeLines &&
            Math.abs(segment.west / lonInterval - Math.round(segment.west / lonInterval)) < 1e-7;
          if (!isMajor && !regularOverlap &&
              longitudeWindows.some(w => segment.west >= w.west && segment.west <= w.east)) {
            this.addMeridian(segment.west, utmColor, 1.2, '', false, centerLat,
              segment.south, segment.north);
          }
          if (!showZoneLabels || centerLat < segment.south || centerLat >= segment.north) continue;
          const visibleWindow = longitudeWindows.find(w =>
            segment.east >= w.west && segment.west <= w.east);
          if (!visibleWindow) continue;
          const labelLon = (Math.max(segment.west, visibleWindow.west) +
                            Math.min(segment.east, visibleWindow.east)) / 2;
          const labelLat = Math.max(segment.south + 0.001,
            Math.min(segment.north - 0.001, centerLat));
          this.dataSource.entities.add({
            position: new Cesium.CallbackPositionProperty(() => {
              const center = getScreenCenterGround();
              const longitude = center.valid ? center.longitude : labelLon;
              const latitude = center.valid ? center.latitude : labelLat;
              return Cesium.Cartesian3.fromDegrees(
                Math.max(segment.west + 0.001, Math.min(segment.east - 0.001, longitude)),
                Math.max(segment.south + 0.001, Math.min(segment.north - 0.001, latitude))
              );
            }, false),
            label: {
              text: 'UTM ' + segment.zone + (labelLat >= 0 ? 'N' : 'S'),
              font: 'bold 11px Segoe UI, sans-serif',
              fillColor: Cesium.Color.fromCssColorString('#D8B4FE'),
              outlineColor: labelOutline,
              outlineWidth: 3,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
              disableDepthTestDistance: 0,
              pixelOffset: new Cesium.Cartesian2(0, -10),
              distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 8000000)
            }
          });
        }
      }
      viewer.scene.requestRender();
    },

    addParallel(lat, color, width, label, showLabel, preferredLon) {
      const coords = [];
      for (let lon = -180; lon <= 180; lon += 2) {
        coords.push(Cesium.Cartesian3.fromDegrees(lon, lat));
      }
      this.dataSource.entities.add({
        polyline: {
          positions: coords,
          width: width,
          material: color,
          clampToGround: true
        }
      });
      if (showLabel && label) {
        [Number.isFinite(preferredLon) ? preferredLon : 0].forEach(labelLon => {
          this.dataSource.entities.add({
            position: new Cesium.CallbackPositionProperty(() => {
              const center = getScreenCenterGround();
              return Cesium.Cartesian3.fromDegrees(center.valid ? center.longitude : labelLon, lat);
            }, false),
            label: {
              text: label,
              font: '11px Segoe UI, sans-serif',
              fillColor: Cesium.Color.WHITE,
              outlineColor: Cesium.Color.BLACK,
              outlineWidth: 2,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
              disableDepthTestDistance: 0,
              distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 8000000),
              translucencyByDistance: new Cesium.NearFarScalar(1000000, 1.0, 9000000, 0.0)
            }
          });
        });
      }
    },

    addMeridian(lon, color, width, label, showLabel, preferredLat, south = -85, north = 85) {
      const coords = [];
      for (let lat = south; lat < north; lat += 2) {
        coords.push(Cesium.Cartesian3.fromDegrees(lon, lat));
      }
      coords.push(Cesium.Cartesian3.fromDegrees(lon, north));
      this.dataSource.entities.add({
        polyline: {
          positions: coords,
          width: width,
          material: color,
          clampToGround: true
        }
      });
      if (showLabel && label) {
        [Number.isFinite(preferredLat) ? preferredLat : 0].forEach(labelLat => {
          this.dataSource.entities.add({
            position: new Cesium.CallbackPositionProperty(() => {
              const center = getScreenCenterGround();
              return Cesium.Cartesian3.fromDegrees(lon,
                center.valid ? Math.max(south, Math.min(north, center.latitude)) : labelLat);
            }, false),
            label: {
              text: label,
              font: '11px Segoe UI, sans-serif',
              fillColor: Cesium.Color.WHITE,
              outlineColor: Cesium.Color.BLACK,
              outlineWidth: 2,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
              disableDepthTestDistance: 0,
              distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 8000000),
              translucencyByDistance: new Cesium.NearFarScalar(1000000, 1.0, 9000000, 0.0)
            }
          });
        });
      }
    },

    addKeyPoint(lon, lat, label, color) {
      const anchor = Cesium.Cartesian3.fromDegrees(lon, lat);
      const normal = Cesium.Ellipsoid.WGS84.geodeticSurfaceNormal(anchor, new Cesium.Cartesian3());
      const visibleFromCamera = new Cesium.CallbackProperty(() =>
        window.ZEarthGraticuleGeometry.isSurfaceAnchorVisible(
          anchor, normal, viewer.camera.positionWC), false);
      this.dataSource.entities.add({
        position: anchor,
        point: {
          show: visibleFromCamera,
          pixelSize: 9,
          color: color,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 2,
          heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
          disableDepthTestDistance: 0
        },
        label: {
          show: visibleFromCamera,
          text: this.config.showLabels ? label : '',
          font: 'bold 11px Segoe UI, sans-serif',
          fillColor: Cesium.Color.WHITE,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 3,
          style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
          pixelOffset: new Cesium.Cartesian2(0, -18),
          disableDepthTestDistance: 0,
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 12000000)
        }
      });
    },

    setConfig(cfg) {
      if (!cfg) return;
      if (typeof cfg.visible === 'boolean') this.config.visible = cfg.visible;
      if (typeof cfg.showLabels === 'boolean') this.config.showLabels = cfg.showLabels;
      if (['DD', 'DM', 'DMS', 'Degrees'].includes(cfg.gridLabelFormat)) this.config.gridLabelFormat = cfg.gridLabelFormat;
      if (typeof cfg.showKeyParallels === 'boolean') this.config.showKeyParallels = cfg.showKeyParallels;
      if (typeof cfg.showUtmZones === 'boolean') this.config.showUtmZones = cfg.showUtmZones;
      if (typeof cfg.showLongitudeLines === 'boolean') this.config.showLongitudeLines = cfg.showLongitudeLines;
      if (typeof cfg.showLatitudeLines === 'boolean') this.config.showLatitudeLines = cfg.showLatitudeLines;
      if (Number.isFinite(cfg.longitudeIntervalDegrees) && cfg.longitudeIntervalDegrees >= 0 && cfg.longitudeIntervalDegrees <= 60) this.config.longitudeIntervalDegrees = cfg.longitudeIntervalDegrees;
      if (Number.isFinite(cfg.latitudeIntervalDegrees) && cfg.latitudeIntervalDegrees >= 0 && cfg.latitudeIntervalDegrees <= 60) this.config.latitudeIntervalDegrees = cfg.latitudeIntervalDegrees;
      if (typeof cfg.intervalDegrees === 'number' && cfg.intervalDegrees >= 0) {
        this.config.longitudeIntervalDegrees = cfg.intervalDegrees;
        this.config.latitudeIntervalDegrees = cfg.intervalDegrees;
      }
      this.buildGraticules();
      viewer.scene.requestRender();
    },

    refreshForCamera() {
      if (!this.config.visible) return;
      const center = getScreenCenterGround();
      const altitude = Math.round((viewer.camera.positionCartographic?.height || 0) / 50000);
      const key = center.valid
        ? `${Math.round(center.latitude * 4)}/${Math.round(center.longitude * 4)}/${altitude}`
        : `space/${altitude}`;
      if (key !== this.lastViewKey) {
        this.lastViewKey = key;
        this.buildGraticules();
      }
    }
  };

  // Compact Compliant Credits & Lightbox (CR-01, CR-02, CR-03)
  const CreditsManager = {
    dockEl: null,
    modalEl: null,
    badgeEsri: null,
    badgeOsm: null,
    badgeCarto: null,
    showLogos: false,

    init() {
      this.dockEl = document.getElementById('credits-dock');
      this.modalEl = document.getElementById('credits-modal');
      this.badgeEsri = document.getElementById('badge-esri');
      this.badgeOsm = document.getElementById('badge-osm');
      this.badgeCarto = document.getElementById('badge-carto');

      document.getElementById('btn-credits-modal')?.addEventListener('click', () => {
        if (this.modalEl) this.modalEl.style.display = 'flex';
      });
      document.getElementById('btn-credits-close')?.addEventListener('click', () => {
        if (this.modalEl) this.modalEl.style.display = 'none';
      });
      this.modalEl?.addEventListener('click', (e) => {
        if (e.target === this.modalEl) this.modalEl.style.display = 'none';
      });
      this.updateActiveBadges('osm-standard');
      this.setConfig({ mode: 'Compact', showOptionalLogos: false });
    },

    updateActiveBadges(basemapId) {
      const id = basemapId || ImageryManager.activeBasemap;
      if (this.badgeOsm) this.badgeOsm.classList.toggle('active', id.includes('osm') || id.includes('opentopo'));
      if (this.badgeCarto) this.badgeCarto.classList.toggle('active', id.includes('carto'));
      if (this.badgeEsri) this.badgeEsri.classList.toggle('active', id.includes('esri'));
    },

    setConfig(cfg) {
      if (!cfg) return;
      if (typeof cfg.showLogos === 'boolean' || typeof cfg.showOptionalLogos === 'boolean') {
        this.showLogos = typeof cfg.showLogos === 'boolean' ? cfg.showLogos : cfg.showOptionalLogos;
        const row = document.getElementById('provider-logos-row');
        if (row) {
          const optionalBadges = row.querySelectorAll('.provider-badge:not(.logo-cesium)');
          optionalBadges.forEach(b => b.style.display = this.showLogos ? 'inline-block' : 'none');
        }
      }
      if (typeof cfg.compactMode === 'boolean' && this.dockEl) {
        this.dockEl.classList.toggle('compact', cfg.compactMode);
      } else if (typeof cfg.mode === 'string' && this.dockEl) {
        this.dockEl.classList.toggle('compact', cfg.mode === 'Compact');
      }
    }
  };

  // 3-Mode Compass Engine (Cardinal, Heading, Qibla) (CMP-01, CMP-02, CMP-03, CMP-04, CMP-05)
  const CompassManager = {
    activeMode: 'cardinal',
    viewCardinal: null,
    viewHeading: null,
    viewQibla: null,
    cardinalRose: null,
    headingRose: null,
    headingBadge: null,
    qiblaRose: null,
    qiblaNeedle: null,
    qiblaText: null,
    qiblaWarning: null,
    qiblaDial: null,
    btnCardinal: null,
    btnHeading: null,
    btnQibla: null,
    pendingFrame: null,
    pendingCenter: null,
    pendingHeading: 0,
    isPointerDragging: false,
    dragMoved: false,
    dragStartX: 0,
    dragLastX: 0,
    dragTarget: null,
    dragRange: 0,

    init() {
      this.viewCardinal = document.getElementById('view-cardinal');
      this.viewHeading = document.getElementById('view-heading');
      this.viewQibla = document.getElementById('view-qibla');
      this.cardinalRose = document.getElementById('cardinal-rose');
      this.headingRose = document.getElementById('heading-rose');
      this.headingBadge = document.getElementById('heading-value-badge');
      this.qiblaRose = document.getElementById('qibla-rose');
      this.qiblaNeedle = document.getElementById('qibla-needle');
      this.qiblaText = document.getElementById('qibla-bearing-text');
      this.qiblaWarning = document.getElementById('qibla-warning');
      this.qiblaDial = document.getElementById('qibla-dial');
      this.btnCardinal = document.getElementById('btn-mode-cardinal');
      this.btnHeading = document.getElementById('btn-mode-heading');
      this.btnQibla = document.getElementById('btn-mode-qibla');

      this.btnCardinal?.addEventListener('click', () => this.setMode('cardinal'));
      this.btnHeading?.addEventListener('click', () => this.setMode('heading'));
      this.btnQibla?.addEventListener('click', () => this.setMode('qibla'));

      const container = document.getElementById('compass-dial-container');
      container?.addEventListener('pointerdown', (event) => this.beginPointerDrag(event));
      container?.addEventListener('pointermove', (event) => this.continuePointerDrag(event));
      container?.addEventListener('pointerup', (event) => this.endPointerDrag(event));
      container?.addEventListener('pointercancel', (event) => this.endPointerDrag(event));
      container?.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        resetPitch();
      });
      document.getElementById('btn-north-reset')?.addEventListener('click', () => resetNorth());

      this.setMode('cardinal');
    },

    beginPointerDrag(event) {
      if (event.button !== 0) return;
      const container = event.currentTarget;
      this.isPointerDragging = true;
      this.dragMoved = false;
      this.dragStartX = event.clientX;
      this.dragLastX = event.clientX;
      this.dragTarget = getScreenCenterGroundCartesian();
      this.dragRange = this.dragTarget
        ? Cesium.Cartesian3.distance(viewer.camera.positionWC, this.dragTarget)
        : 0;
      container?.setPointerCapture?.(event.pointerId);
      container?.classList.add('dragging');
      event.preventDefault();
      event.stopPropagation();
    },

    continuePointerDrag(event) {
      if (!this.isPointerDragging) return;
      const deltaX = event.clientX - this.dragLastX;
      this.dragLastX = event.clientX;
      if (Math.abs(event.clientX - this.dragStartX) > 3) this.dragMoved = true;
      if (Math.abs(deltaX) < 0.01) return;

      const deltaHeading = Cesium.Math.toRadians(deltaX * 0.45);
      if (viewer.scene.mode === Cesium.SceneMode.SCENE3D && this.dragTarget && this.dragRange > 0) {
        const heading = viewer.camera.heading + deltaHeading;
        const pitch = Cesium.Math.clamp(
          viewer.camera.pitch,
          -Cesium.Math.PI_OVER_TWO + Cesium.Math.toRadians(0.25),
          -Cesium.Math.toRadians(0.5));
        viewer.camera.lookAt(
          this.dragTarget,
          new Cesium.HeadingPitchRange(heading, pitch, this.dragRange));
        viewer.camera.lookAtTransform(Cesium.Matrix4.IDENTITY);
      } else {
        // In 2D the same gesture rotates the map without translating it.
        viewer.camera.twistRight(deltaHeading);
      }
      viewer.scene.requestRender();
      event.preventDefault();
      event.stopPropagation();
    },

    endPointerDrag(event) {
      if (!this.isPointerDragging) return;
      const container = event.currentTarget;
      this.isPointerDragging = false;
      container?.classList.remove('dragging');
      if (container?.hasPointerCapture?.(event.pointerId)) {
        container.releasePointerCapture(event.pointerId);
      }
      if (!this.dragMoved) resetNorth();
      this.dragTarget = null;
      this.dragRange = 0;
      event.preventDefault();
      event.stopPropagation();
    },

    setMode(mode) {
      this.activeMode = mode;
      // Keep the active face as a flex container so all three dial variants
      // remain centered inside the same 86 x 86 interaction target.
      if (this.viewCardinal) this.viewCardinal.style.display = (mode === 'cardinal' ? 'flex' : 'none');
      if (this.viewHeading) this.viewHeading.style.display = (mode === 'heading' ? 'flex' : 'none');
      if (this.viewQibla) this.viewQibla.style.display = (mode === 'qibla' ? 'flex' : 'none');

      this.btnCardinal?.classList.toggle('active', mode === 'cardinal');
      this.btnHeading?.classList.toggle('active', mode === 'heading');
      this.btnQibla?.classList.toggle('active', mode === 'qibla');

      const center = getScreenCenterGround();
      let heading = ((Cesium.Math.toDegrees(viewer.camera.heading) % 360) + 360) % 360;
      if (Math.abs(heading - 360) < 0.001) heading = 0.0;
      this.update(center, heading);
    },

    calculateQibla(lat, lon) {
      const phi1 = Cesium.Math.toRadians(lat);
      const lam1 = Cesium.Math.toRadians(lon);
      const phi2 = Cesium.Math.toRadians(21.422487);
      const lam2 = Cesium.Math.toRadians(39.826206);
      const dLam = lam2 - lam1;
      const y = Math.sin(dLam);
      const x = Math.cos(phi1) * Math.tan(phi2) - Math.sin(phi1) * Math.cos(dLam);
      let az = Cesium.Math.toDegrees(Math.atan2(y, x));
      return ((az % 360) + 360) % 360;
    },

    requestUpdate(center, heading) {
      this.pendingCenter = center;
      this.pendingHeading = heading;
      if (this.pendingFrame !== null) return;
      this.pendingFrame = requestAnimationFrame(() => {
        this.pendingFrame = null;
        this.update(this.pendingCenter, this.pendingHeading);
      });
    },

    update(center, heading) {
      heading = ((Number(heading) % 360) + 360) % 360;
      const screenNorthAngle = -heading;

      if (this.cardinalRose) {
        this.cardinalRose.style.transform = `rotate(${screenNorthAngle}deg)`;
      }

      if (this.headingRose) {
        this.headingRose.style.transform = `rotate(${screenNorthAngle}deg)`;
      }
      if (this.headingBadge) {
        this.headingBadge.textContent = `${Math.round(heading)}°`;
      }

      if (this.qiblaRose) {
        this.qiblaRose.style.transform = `rotate(${screenNorthAngle}deg)`;
      }

      if (center && center.valid) {
        if (this.qiblaWarning) this.qiblaWarning.style.display = 'none';
        if (this.qiblaDial) this.qiblaDial.style.opacity = '1';
        const qiblaAzimuth = this.calculateQibla(center.latitude, center.longitude);
        if (this.qiblaText) {
          this.qiblaText.textContent = `${Math.round(qiblaAzimuth)}°`;
        }
        if (this.qiblaNeedle) {
          // Convert the geographic bearing into screen space. Heading zero
          // places north at the top; rotating the camera clockwise subtracts
          // the same angle from every geographic bearing on screen.
          const screenQiblaAngle = ((qiblaAzimuth - heading) % 360 + 360) % 360;
          this.qiblaNeedle.style.setProperty('--qibla-angle', `${screenQiblaAngle}deg`);
        }
      } else {
        if (this.qiblaWarning) this.qiblaWarning.style.display = 'block';
        if (this.qiblaDial) this.qiblaDial.style.opacity = '0.35';
        if (this.qiblaText) {
          this.qiblaText.textContent = '—';
        }
      }
    }
  };

  const UnitSettings = {
    distanceUnit: 'Auto',
    areaUnit: 'Auto',
    elevationUnit: 'Meters',
    set(cfg) {
      if (!cfg) return;
      this.distanceUnit = cfg.distanceUnit || this.distanceUnit;
      this.areaUnit = cfg.areaUnit || this.areaUnit;
      this.elevationUnit = cfg.elevationUnit || this.elevationUnit;
    }
  };

  const CrsManager = {
    current: { epsg: 4326, name: 'WGS 84', isProjected: false, unit: 'degree' },
    set(config) {
      this.current = { ...this.current, ...(config || {}) };
      sendHostMessage({ event: 'crs.changed', crs: this.current });
      return this.current;
    }
  };

  const SearchPinManager = {
    entity: null,
    set(config) {
      if (this.entity) {
        viewer.entities.remove(this.entity);
        this.entity = null;
      }
      if (!config || config.show === false) {
        viewer.scene.requestRender();
        return false;
      }
      const longitude = Number(config.longitude);
      const latitude = Number(config.latitude);
      if (!Number.isFinite(longitude) || !Number.isFinite(latitude)) return false;
      this.entity = viewer.entities.add({
        id: 'zearth-search-pin',
        position: Cesium.Cartesian3.fromDegrees(longitude, latitude, 0),
        billboard: {
          image: createSearchPinCanvas(),
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
          disableDepthTestDistance: 0,
          scaleByDistance: new Cesium.NearFarScalar(1000, 1.0, 12000000, 0.55)
        },
        label: {
          text: isolatedLabelText(config.title || 'Search result'),
          font: '13px "Segoe UI", Tahoma, "Noto Sans Arabic", sans-serif',
          fillColor: Cesium.Color.WHITE,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 3,
          style: Cesium.LabelStyle.FILL_AND_OUTLINE,
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          pixelOffset: new Cesium.Cartesian2(0, -44),
          heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
          disableDepthTestDistance: 0,
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 10000000)
        }
      });
      viewer.scene.requestRender();
      return true;
    }
  };

  function createSearchPinCanvas() {
    const canvas = document.createElement('canvas');
    canvas.width = 40;
    canvas.height = 52;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ef4444';
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(20, 50);
    ctx.bezierCurveTo(16, 40, 4, 31, 4, 20);
    ctx.arc(20, 20, 16, Math.PI, 0, false);
    ctx.bezierCurveTo(36, 31, 24, 40, 20, 50);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(20, 20, 6, 0, Math.PI * 2);
    ctx.fill();
    return canvas;
  }

  const RasterOverlayManager = {
    add(config) {
      if (!config || !config.id || !config.imageUrl) return false;
      this.remove(config.id);
      const entity = viewer.entities.add({
        id: `raster-overlay-${config.id}`,
        name: config.name || config.id,
        rectangle: {
          coordinates: Cesium.Rectangle.fromDegrees(
            Number(config.west), Number(config.south), Number(config.east), Number(config.north)),
          material: new Cesium.ImageMaterialProperty({
            image: config.imageUrl,
            transparent: true,
            color: Cesium.Color.WHITE.withAlpha(Number.isFinite(Number(config.opacity)) ? Number(config.opacity) : 1)
          })
        }
      });
      ImageryManager.overlayLayers.set(config.id, { id: config.id, entity, attribution: '' });
      viewer.scene.requestRender();
      return true;
    },
    remove(id) {
      const item = ImageryManager.overlayLayers.get(id);
      if (!item || !item.entity) return false;
      viewer.entities.remove(item.entity);
      ImageryManager.overlayLayers.delete(id);
      viewer.scene.requestRender();
      return true;
    }
  };

  const EnvironmentManager = {
    sunlightEnabled: false,
    atmosphereEnabled: true,
    animationTimer: null,
    dateUtc: new Date().toISOString().slice(0, 10),
    setTimeOfDay(timeHours, dateUtc) {
      const hours = Math.max(0, Math.min(24, Number(timeHours) || 0));
      // Date/time is UTC; geographic sun angle automatically follows the
      // globe's actual Earth location in Cesium's lighting calculation.
      if (typeof dateUtc === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(dateUtc)) {
        const candidate = new Date(dateUtc + 'T00:00:00Z');
        if (Number.isFinite(candidate.getTime()) && candidate.toISOString().slice(0, 10) === dateUtc) {
          this.dateUtc = dateUtc;
        }
      }
      const now = new Date(this.dateUtc + 'T00:00:00Z');
      const whole = Math.floor(hours === 24 ? 0 : hours);
      const minutes = Math.round((hours - Math.floor(hours)) * 60);
      now.setUTCHours(whole, minutes, 0, 0);
      viewer.clock.currentTime = Cesium.JulianDate.fromDate(now);
      viewer.clock.shouldAnimate = false; // Hold manual solar time between changes.
      const slider = document.getElementById('sunlight-slider');
      const label = document.getElementById('sunlight-time-label');
      if (slider) slider.value = String(hours);
      if (label) label.textContent = `${String(whole).padStart(2, '0')}:${String(minutes).padStart(2, '0')} UTC`;
      sendHostMessage({ event: 'environment.timeChanged', timeHours: hours });
      viewer.scene.requestRender();
      return hours;
    },
    setSunlight(config) {
      const enabled = config?.enabled !== false;
      this.sunlightEnabled = enabled;
      viewer.scene.globe.enableLighting = enabled;
      viewer.shadows = enabled;
      const hud = document.getElementById('sunlight-hud');
      if (hud) hud.style.display = enabled ? 'block' : 'none';
      if (typeof config?.timeHours === 'number') this.setTimeOfDay(config.timeHours, config.dateUtc);
      else if (enabled) {
        const now = new Date();
        this.setTimeOfDay(now.getUTCHours() + now.getUTCMinutes() / 60,
          now.toISOString().slice(0, 10));
      }
      viewer.scene.requestRender();
      sendHostMessage({ event: 'environment.sunlightChanged', enabled });
      return enabled;
    },
    setAtmosphere(config) {
      const enabled = config?.enabled !== false;
      this.atmosphereEnabled = enabled;
      if (viewer.scene.skyAtmosphere) viewer.scene.skyAtmosphere.show = enabled;
      if (viewer.scene.globe) viewer.scene.globe.showGroundAtmosphere = enabled;
      if (viewer.scene.fog) viewer.scene.fog.enabled = enabled;
      viewer.scene.requestRender();
      sendHostMessage({ event: 'environment.atmosphereChanged', enabled });
      return enabled;
    },
    toggleAnimation() {
      const button = document.getElementById('btn-sunlight-play');
      if (this.animationTimer) {
        clearInterval(this.animationTimer);
        this.animationTimer = null;
        if (button) button.textContent = '▶';
        return false;
      }
      if (button) button.textContent = '⏸';
      this.animationTimer = setInterval(() => {
        const slider = document.getElementById('sunlight-slider');
        const current = Number(slider?.value || 0);
        this.setTimeOfDay((current + 0.1) % 24);
      }, 120);
      return true;
    }
  };

  const SwipeManager = {
    active: false,
    leftId: null,
    rightId: null,
    previousBasemap: null,
    start(leftId, rightId) {
      this.stop(false);
      const left = ImageryManager.baseLayers[leftId];
      const right = ImageryManager.baseLayers[rightId];
      if (!left?.layer || !right?.layer || left.layer === right.layer) return false;
      this.previousBasemap = ImageryManager.activeBasemap;
      this.leftId = leftId;
      this.rightId = rightId;
      left.layer.show = true;
      right.layer.show = true;
      left.layer.splitDirection = Cesium.SplitDirection.LEFT;
      right.layer.splitDirection = Cesium.SplitDirection.RIGHT;
      viewer.scene.splitPosition = 0.5;
      const container = document.getElementById('swipe-container');
      if (container) container.style.display = 'block';
      const leftBadge = document.getElementById('swipe-left-badge');
      const rightBadge = document.getElementById('swipe-right-badge');
      if (leftBadge) leftBadge.textContent = left.name || leftId;
      if (rightBadge) rightBadge.textContent = right.name || rightId;
      this.active = true;
      viewer.scene.requestRender();
      return true;
    },
    stop(restore = true) {
      if (this.leftId && ImageryManager.baseLayers[this.leftId]?.layer) {
        ImageryManager.baseLayers[this.leftId].layer.splitDirection = Cesium.SplitDirection.NONE;
      }
      if (this.rightId && ImageryManager.baseLayers[this.rightId]?.layer) {
        ImageryManager.baseLayers[this.rightId].layer.splitDirection = Cesium.SplitDirection.NONE;
      }
      const container = document.getElementById('swipe-container');
      if (container) container.style.display = 'none';
      const wasActive = this.active;
      this.active = false;
      this.leftId = null;
      this.rightId = null;
      if (restore && wasActive) ImageryManager.setBasemap(this.previousBasemap || 'esri-hybrid');
      viewer.scene.requestRender();
      return wasActive;
    }
  };

  // Draggable sunlight HUD, independent from globe camera gestures.
  const sunlightHud = document.getElementById('sunlight-hud');
  const sunlightHeader = sunlightHud?.querySelector('.sunlight-header');
  if (sunlightHud && sunlightHeader) {
    let origin = null;
    sunlightHeader.addEventListener('pointerdown', event => {
      if (event.button !== 0 || event.target.closest('button,input')) return;
      const bounds = sunlightHud.getBoundingClientRect();
      origin = { dx: event.clientX - bounds.left, dy: event.clientY - bounds.top };
      sunlightHeader.setPointerCapture(event.pointerId);
    });
    sunlightHeader.addEventListener('pointermove', event => {
      if (!origin) return;
      const bounds = (sunlightHud.offsetParent || document.body).getBoundingClientRect();
      sunlightHud.style.left = Math.max(0, Math.min(Math.max(0, bounds.width - sunlightHud.offsetWidth),
        event.clientX - bounds.left - origin.dx)) + 'px';
      sunlightHud.style.top = Math.max(0, Math.min(Math.max(0, bounds.height - sunlightHud.offsetHeight),
        event.clientY - bounds.top - origin.dy)) + 'px';
    });
    sunlightHeader.addEventListener('pointerup', () => { origin = null; });
    sunlightHeader.addEventListener('pointercancel', () => { origin = null; });
  }
  document.getElementById('sunlight-slider')?.addEventListener('input', e => EnvironmentManager.setTimeOfDay(Number(e.target.value)));
  document.querySelectorAll('.sunlight-preset-btn').forEach(button =>
    button.addEventListener('click', () => EnvironmentManager.setTimeOfDay(Number(button.dataset.time))));
  document.getElementById('btn-sunlight-now')?.addEventListener('click', () => {
    const now = new Date();
    EnvironmentManager.setTimeOfDay(now.getUTCHours() + now.getUTCMinutes() / 60,
      now.toISOString().slice(0, 10));
  });
  document.getElementById('btn-sunlight-play')?.addEventListener('click', () => EnvironmentManager.toggleAnimation());
  document.getElementById('btn-sunlight-close')?.addEventListener('click', () => EnvironmentManager.setSunlight({ enabled: false }));
  document.getElementById('btn-exit-swipe')?.addEventListener('click', () => SwipeManager.stop());

  const swipeDivider = document.getElementById('swipe-divider');
  let draggingSwipe = false;
  swipeDivider?.addEventListener('pointerdown', e => { draggingSwipe = true; swipeDivider.setPointerCapture(e.pointerId); });
  swipeDivider?.addEventListener('pointermove', e => {
    if (!draggingSwipe) return;
    const ratio = Math.max(0.02, Math.min(0.98, e.clientX / Math.max(1, viewer.canvas.clientWidth)));
    viewer.scene.splitPosition = ratio;
    swipeDivider.style.left = `${ratio * 100}%`;
    viewer.scene.requestRender();
  });
  swipeDivider?.addEventListener('pointerup', () => { draggingSwipe = false; });


  // Keyboard navigation shortcuts for drawing and measurement
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (DrawingManager.activeMode) DrawingManager.cancel();
      if (MeasurementManager.activeMode) MeasurementManager.clear();
    } else if (e.key === 'Enter') {
      if (DrawingManager.activeMode) DrawingManager.finish();
      if (MeasurementManager.activeMode) MeasurementManager.finish();
    }
  });

  // Bind HUD measurement action buttons
  // Keep measurement results visible without covering the area the user is tracing.
  // Header-only pointer dragging; ignore controls and clamp inside the WebView viewport.
  (function enableMovableMeasurementHud() {
    const hud = document.getElementById('measurement-hud');
    const handle = document.getElementById('measure-drag-handle');
    if (!hud || !handle) return;
    let drag = null;
    handle.addEventListener('pointerdown', event => {
      if (event.button !== 0 || event.target.closest('select, button, input')) return;
      const rect = hud.getBoundingClientRect();
      hud.style.position = 'fixed';
      hud.style.left = rect.left + 'px';
      hud.style.top = rect.top + 'px';
      hud.style.transform = 'none';
      drag = { id: event.pointerId, x: event.clientX, y: event.clientY, left: rect.left, top: rect.top };
      handle.setPointerCapture(event.pointerId);
      handle.style.cursor = 'grabbing';
      event.preventDefault();
    });
    handle.addEventListener('pointermove', event => {
      if (!drag || event.pointerId !== drag.id) return;
      const newLeft = drag.left + event.clientX - drag.x;
      const newTop = drag.top + event.clientY - drag.y;
      hud.style.left = Math.max(0, Math.min(window.innerWidth - hud.offsetWidth, newLeft)) + 'px';
      hud.style.top = Math.max(0, Math.min(window.innerHeight - hud.offsetHeight, newTop)) + 'px';
    });
    const release = event => {
      if (!drag || event.pointerId !== drag.id) return;
      drag = null;
      handle.style.cursor = 'grab';
      if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
    };
    handle.addEventListener('pointerup', release);
    handle.addEventListener('pointercancel', release);
  })();

  document.getElementById('btn-measure-finish')?.addEventListener('click', () => MeasurementManager.finish());
  document.getElementById('btn-measure-clear')?.addEventListener('click', () => MeasurementManager.clear());

  // 5. Host Communication Helper
  function sendHostMessage(msg) {
    const json = JSON.stringify(msg);
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(json);
    }
  }

  // Error listeners for host reporting
  window.addEventListener('error', (event) => {
    sendHostMessage({
      event: 'js.error',
      message: event.message,
      filename: event.filename,
      lineno: event.lineno,
      colno: event.colno
    });
  });

  window.addEventListener('unhandledrejection', (event) => {
    sendHostMessage({
      event: 'js.unhandledrejection',
      reason: String(event.reason)
    });
  });

  function updateCoordUI(lat, lon, alt, heading) {
    if (!statusEl) return;
    const latStr = `${Math.abs(lat).toFixed(4)}° ${lat >= 0 ? 'N' : 'S'}`;
    const lonStr = `${Math.abs(lon).toFixed(4)}° ${lon >= 0 ? 'E' : 'W'}`;
    const altStr = alt >= 1000 ? `${(alt / 1000).toLocaleString(undefined, { maximumFractionDigits: 0 })} km` : `${alt.toFixed(0)} m`;
    const modeStr = viewer.scene.mode === Cesium.SceneMode.SCENE3D ? 'Scene3D' : 'Scene2D';
    statusEl.textContent = `${latStr} | ${lonStr} | Eye: ${altStr} | Head: ${heading.toFixed(1)}° | ${modeStr}`;
  }

  // 6. Real-Time Camera Telemetry & Compass (CRS-01, CRS-02, CRS-03)
  let lastPostTime = 0;
  let lastCenterPostTime = 0;
  viewer.camera.changed.addEventListener(() => {
    let carto = viewer.camera.positionCartographic;
    if (viewer.scene.mode === Cesium.SceneMode.SCENE2D && viewer.scene.mapProjection) {
      carto = viewer.scene.mapProjection.unproject(viewer.camera.position);
    }
    if (!carto) return;

    const lat = Cesium.Math.toDegrees(carto.latitude);
    const lon = Cesium.Math.toDegrees(carto.longitude);
    const alt = carto.height;
    let heading = ((Cesium.Math.toDegrees(viewer.camera.heading) % 360) + 360) % 360;
    if (Math.abs(heading - 360) < 0.001) heading = 0.0;
    const pitch = Cesium.Math.toDegrees(viewer.camera.pitch);
    const roll = Cesium.Math.toDegrees(viewer.camera.roll);

    updateCoordUI(lat, lon, alt, heading);

    if (compassRing) {
      compassRing.style.transform = `rotate(${-heading}deg)`;
    }

    const center = getScreenCenterGround();
    CompassManager.requestUpdate(center, heading);

    const now = Date.now();
    if (now - lastPostTime > 200) { // 5Hz throttle; the host coalesces further on the UI thread
      lastPostTime = now;
      sendHostMessage({
        event: 'camera.changed',
        camera: {
          latitude: lat,
          longitude: lon,
          altitude: alt,
          heading: heading,
          pitch: pitch,
          roll: roll
        }
      });
    }

    if (now - lastCenterPostTime > 250) {
      lastCenterPostTime = now;
      sendHostMessage({
        event: 'map.centerChanged',
        center: center
      });
    }
  });

  // Discrete movement completion notification for extent history stack (ZE-018)
  viewer.camera.moveEnd.addEventListener(() => {
    let carto = viewer.camera.positionCartographic;
    if (viewer.scene.mode === Cesium.SceneMode.SCENE2D && viewer.scene.mapProjection) {
      carto = viewer.scene.mapProjection.unproject(viewer.camera.position);
    }
    if (!carto) return;

    let heading = ((Cesium.Math.toDegrees(viewer.camera.heading) % 360) + 360) % 360;
    if (Math.abs(heading - 360) < 0.001) heading = 0.0;

    sendHostMessage({
      event: 'camera.moveEnd',
      camera: {
        latitude: Cesium.Math.toDegrees(carto.latitude),
        longitude: Cesium.Math.toDegrees(carto.longitude),
        altitude: carto.height,
        heading: heading,
        pitch: Cesium.Math.toDegrees(viewer.camera.pitch),
        roll: Cesium.Math.toDegrees(viewer.camera.roll)
      }
    });

    const centerEnd = getScreenCenterGround();
    CompassManager.update(centerEnd, heading);
    GraticuleManager.refreshForCamera();
    sendHostMessage({
      event: 'map.centerChanged',
      center: centerEnd
    });
  });

  viewer.scene.morphComplete.addEventListener(() => {
    const is3D = viewer.scene.mode === Cesium.SceneMode.SCENE3D;
    if (btn3D) btn3D.classList.toggle('active', is3D);
    if (btn2D) btn2D.classList.toggle('active', !is3D);
    sendHostMessage({
      event: 'scene.modeChanged',
      mode: is3D ? 'Scene3D' : 'Scene2D'
    });

    const center = getScreenCenterGround();
    CompassManager.update(center, Cesium.Math.toDegrees(viewer.camera.heading));
    sendHostMessage({
      event: 'map.centerChanged',
      center: center
    });

    if (pendingSceneMode) {
      const next = pendingSceneMode;
      pendingSceneMode = null;
      setTimeout(() => setSceneMode(next), 60);
    }
  });

  // Navigation ScreenSpace Handlers (NAV-05, NAV-06, NAV-07, CRS-02)
  const navHandler = new Cesium.ScreenSpaceEventHandler(viewer.scene.canvas);
  let lastMapPointerPosition = new Cesium.Cartesian2(
    viewer.scene.canvas.clientWidth / 2,
    viewer.scene.canvas.clientHeight / 2);

  // Smooth Double-Click Zoom toward clicked ground target
  navHandler.setInputAction((movement) => {
    if (DrawingManager.activeMode || MeasurementManager.activeMode) {
      return; // Handled by active drawing/measuring sketch
    }

    const position = movement.position;
    if (!position) return;

    lastMapPointerPosition = Cesium.Cartesian2.clone(position, lastMapPointerPosition);
    zoomAtScreenPosition(true, position, 0.48);
  }, Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);

  // Live Throttled Cursor Position Tracking (CRS-02)
  let lastMouseMoveTime = 0;
  navHandler.setInputAction((movement) => {
    const now = Date.now();
    if (now - lastMouseMoveTime < 100) return;
    lastMouseMoveTime = now;

    const position = movement.endPosition;
    if (!position) return;
    lastMapPointerPosition = Cesium.Cartesian2.clone(position, lastMapPointerPosition);

    const pickCartesian = pickGroundAtScreenPosition(position);

    if (pickCartesian) {
      const carto = Cesium.Cartographic.fromCartesian(pickCartesian);
      if (!isNaN(carto.latitude) && !isNaN(carto.longitude)) {
        sendHostMessage({
          event: 'map.cursorMoved',
          position: {
            valid: true,
            latitude: Cesium.Math.toDegrees(carto.latitude),
            longitude: Cesium.Math.toDegrees(carto.longitude),
            altitude: isNaN(carto.height) ? 0 : carto.height
          }
        });
        return;
      }
    }

    sendHostMessage({
      event: 'map.cursorMoved',
      position: { valid: false, latitude: 0, longitude: 0, altitude: 0 }
    });
  }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

  viewer.canvas.addEventListener('mouseleave', () => {
    sendHostMessage({
      event: 'map.cursorMoved',
      position: { valid: false, latitude: 0, longitude: 0, altitude: 0 }
    });
  });

  // 7. Navigation Actions
  function flyToHome() {
    viewer.camera.flyTo({
      destination: Cesium.Cartesian3.fromDegrees(HOME_LON, HOME_LAT, HOME_ALT),
      orientation: {
        heading: 0.0,
        pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
        roll: 0.0
      },
      duration: 1.2
    });
  }

  function flyToFullExtent() {
    viewer.camera.flyTo({
      destination: Cesium.Cartesian3.fromDegrees(20.0, 15.0, 22000000.0),
      orientation: {
        heading: 0.0,
        pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
        roll: 0.0
      },
      duration: 1.2
    });
  }

  function resetNorth() {
    viewer.camera.flyTo({
      destination: viewer.camera.position,
      orientation: {
        heading: 0.0,
        pitch: viewer.camera.pitch,
        roll: 0.0
      },
      duration: 0.5
    });
  }

  function resetPitch() {
    viewer.camera.flyTo({
      destination: viewer.camera.position,
      orientation: {
        heading: viewer.camera.heading,
        pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
        roll: 0.0
      },
      duration: 0.5
    });
  }

  // Configurable zoom step factor (ZE-016). A smaller default prevents the
  // abrupt jump that made the globe leave the viewport at continental scales.
  let zoomFactor = 0.28;

  function setZoomFactor(factor) {
    if (typeof factor === 'number' && factor > 0.05 && factor < 0.65) {
      zoomFactor = factor;
    }
    return zoomFactor;
  }

  function zoomAtScreenPosition(zoomingIn, requestedPosition, requestedFactor) {
    const camera = viewer.camera;
    const canvas = viewer.canvas;
    if (!camera || !canvas) return false;

    const position = requestedPosition || lastMapPointerPosition || new Cesium.Cartesian2(
      canvas.clientWidth / 2,
      canvas.clientHeight / 2);
    const factor = Cesium.Math.clamp(
      Number.isFinite(requestedFactor) ? requestedFactor : zoomFactor,
      0.05,
      0.65);
    const anchor = pickGroundAtScreenPosition(position);
    const magnitude = Math.max(20.0, camera.getMagnitude());

    if (viewer.scene.mode === Cesium.SceneMode.SCENE2D) {
      const beforeCartographic = anchor
        ? viewer.scene.globe.ellipsoid.cartesianToCartographic(anchor)
        : null;
      const amount = zoomingIn
        ? magnitude * factor
        : magnitude * (factor / (1.0 - factor));

      if (zoomingIn) camera.zoomIn(amount);
      else camera.zoomOut(amount);

      // Orthographic 2D zoom changes the frustum. Translate by the projected
      // before/after difference so the coordinate under the cursor stays put.
      if (beforeCartographic) {
        const afterAnchor = pickGroundAtScreenPosition(position);
        if (afterAnchor) {
          const afterCartographic = viewer.scene.globe.ellipsoid.cartesianToCartographic(afterAnchor);
          const beforeProjected = viewer.scene.mapProjection.project(beforeCartographic);
          const afterProjected = viewer.scene.mapProjection.project(afterCartographic);
          if (beforeProjected && afterProjected) {
            camera.position.x += beforeProjected.x - afterProjected.x;
            camera.position.y += beforeProjected.y - afterProjected.y;
          }
        }
      }
      viewer.scene.requestRender();
      return true;
    }

    if (anchor) {
      const direction = Cesium.Cartesian3.subtract(anchor, camera.positionWC, new Cesium.Cartesian3());
      const distance = Cesium.Cartesian3.magnitude(direction);
      if (distance > 1.0) {
        Cesium.Cartesian3.normalize(direction, direction);
        const height = Math.max(0.0, camera.positionCartographic?.height || magnitude);
        let amount = zoomingIn
          ? distance * factor
          : distance * (factor / (1.0 - factor));
        amount = zoomingIn
          ? Math.min(amount, Math.max(0, height - controller.minimumZoomDistance), distance * 0.85)
          : Math.min(amount, Math.max(0, controller.maximumZoomDistance - height));
        if (!zoomingIn) Cesium.Cartesian3.negate(direction, direction);
        if (amount > 0) camera.move(direction, amount);
        viewer.scene.requestRender();
        return true;
      }
    }

    // Sky fallback: keep the globe centered and respect the same speed limits.
    const amount = zoomingIn
      ? magnitude * factor
      : magnitude * (factor / (1.0 - factor));
    if (zoomingIn) camera.zoomIn(amount);
    else camera.zoomOut(amount);
    viewer.scene.requestRender();
    return true;
  }

  function zoomIn() {
    zoomAtScreenPosition(true, lastMapPointerPosition);
  }

  function zoomOut() {
    zoomAtScreenPosition(false, lastMapPointerPosition);
  }

  // Fit selection & bounding box with antimeridian/dateline crossing support (ZE-017, AC-024)
  function fitSelection(payload) {
    if (!payload) return;
    let positions = [];
    if (Array.isArray(payload.coordinates)) {
      positions = payload.coordinates.map(c => Cesium.Cartesian3.fromDegrees(c.longitude, c.latitude, c.altitude || 0));
    } else if (payload.entityId) {
      const ent = viewer.entities.getById(payload.entityId);
      if (ent && ent.position) {
        const pos = ent.position.getValue(viewer.clock.currentTime);
        if (pos) positions.push(pos);
      }
    } else if (payload.west !== undefined && payload.south !== undefined && payload.east !== undefined && payload.north !== undefined) {
      if (payload.west > payload.east) {
        // Crossing the ±180° antimeridian/dateline: create bounding points to avoid global zoom-out
        positions = [
          Cesium.Cartesian3.fromDegrees(payload.west, payload.south),
          Cesium.Cartesian3.fromDegrees(payload.west, payload.north),
          Cesium.Cartesian3.fromDegrees(payload.east, payload.south),
          Cesium.Cartesian3.fromDegrees(payload.east, payload.north)
        ];
      } else {
        const rect = Cesium.Rectangle.fromDegrees(payload.west, payload.south, payload.east, payload.north);
        viewer.camera.flyTo({
          destination: rect,
          duration: payload.durationSeconds || 1.2
        });
        return;
      }
    }

    if (positions.length > 0) {
      const sphere = Cesium.BoundingSphere.fromPoints(positions);
      sphere.radius = Math.max(sphere.radius * 1.5, 1000.0);
      viewer.camera.flyToBoundingSphere(sphere, {
        duration: payload.durationSeconds || 1.2
      });
    }
  }

  // 50-drag automated stress testing helper (ZE-014, AC-020)
  async function runDragStressTest(dragCount = 50) {
    const canvas = viewer.canvas;
    const rect = canvas.getBoundingClientRect();
    const results = { count: dragCount, successfulDrags: 0, recovered: false, finalAltitude: 0 };

    for (let i = 0; i < dragCount; i++) {
      const startX = rect.left + Math.random() * rect.width;
      const startY = rect.top + Math.random() * rect.height;
      const endX = rect.left + Math.random() * rect.width;
      const endY = rect.top + Math.random() * rect.height;

      canvas.dispatchEvent(new PointerEvent('pointerdown', { clientX: startX, clientY: startY, pointerId: 1, buttons: 1 }));
      canvas.dispatchEvent(new PointerEvent('pointermove', { clientX: endX, clientY: endY, pointerId: 1, buttons: 1 }));
      canvas.dispatchEvent(new PointerEvent('pointerup', { clientX: endX, clientY: endY, pointerId: 1, buttons: 0 }));

      results.successfulDrags++;
      if (i % 10 === 0) await new Promise(r => setTimeout(r, 16));
    }

    const carto = viewer.camera.positionCartographic;
    if (carto && !isNaN(carto.height) && carto.height >= 50.0 && carto.height <= 60000000.0) {
      results.recovered = true;
      results.finalAltitude = carto.height;
    }
    return results;
  }

  let pendingSceneMode = null;

  function setSceneMode(mode) {
    if (!mode) return;
    const m = String(mode).trim().toLowerCase();
    const isTarget2D = (m === 'scene2d' || m === '2d');
    const isTarget3D = (m === 'scene3d' || m === '3d' || m === 'globe');
    const isTargetColumbus = (m === 'columbusview' || m === 'columbus' || m === '2.5d');

    const currentMode = viewer.scene.mode;
    if (currentMode === Cesium.SceneMode.MORPHING) {
      console.warn('[Cesium Engine] Scene is actively morphing. Queuing mode switch to:', mode);
      pendingSceneMode = isTarget2D ? 'Scene2D' : (isTargetColumbus ? 'ColumbusView' : 'Scene3D');
      return;
    }

    if (isTarget2D) {
      if (currentMode !== Cesium.SceneMode.SCENE2D) {
        viewer.scene.morphTo2D(0.8);
      }
    } else if (isTargetColumbus) {
      if (currentMode !== Cesium.SceneMode.COLUMBUS_VIEW) {
        viewer.scene.morphToColumbusView(0.8);
      }
    } else if (isTarget3D) {
      if (currentMode !== Cesium.SceneMode.SCENE3D) {
        viewer.scene.morphTo3D(0.8);
      }
    }
  }

  // 8. Theme Management (ZE-011)
  function setTheme(theme) {
    const isLight = theme === 'light';
    if (isLight) {
      document.body.classList.remove('dark-theme');
      document.body.classList.add('light-theme');
      if (viewer && viewer.scene) {
        viewer.scene.backgroundColor = Cesium.Color.fromCssColorString('#F3F3F5');
      }
    } else {
      document.body.classList.remove('light-theme');
      document.body.classList.add('dark-theme');
      if (viewer && viewer.scene) {
        viewer.scene.backgroundColor = Cesium.Color.BLACK;
      }
    }
  }

  // 9. Bind HTML HUD Controls
  document.getElementById('btn-zoom-in')?.addEventListener('click', zoomIn);
  document.getElementById('btn-zoom-out')?.addEventListener('click', zoomOut);
  document.getElementById('btn-home')?.addEventListener('click', flyToHome);
  document.getElementById('btn-full-extent')?.addEventListener('click', flyToFullExtent);

  btn3D?.addEventListener('click', () => setSceneMode('Scene3D'));
  btn2D?.addEventListener('click', () => setSceneMode('Scene2D'));

  // In-globe history UI shares the existing ImageryManager instead of using
  // a blocking WPF window. A timeline release is a map publishing date, not capture time.
  let historicalPriorBasemap = null;
  const HistoricalTimeline = window.ZEarthHistoricalTimeline?.create({
    element: document.getElementById('history-timeline'),
    onSelect: release => {
      const layer = ImageryManager.registerDynamicBasemap({
        id: 'wayback-live',
        name: 'Esri Wayback release ' + release.date,
        url: release.tileUrl,
        attribution: '© Esri World Imagery Wayback • Release ' + release.date,
        setActive: true
      });
      return layer?.success !== false;
    },
    onClose: () => {
      ImageryManager.setBasemap(historicalPriorBasemap || 'offline-vector');
      historicalPriorBasemap = null;
    }
  });

  // 10. Host Message Dispatcher
  let flightToken = 0;
  function handleHostMessage(raw) {
    let data;
    try {
      data = typeof raw === 'string' ? JSON.parse(raw) : raw;
    } catch (e) {
      console.error('Invalid host JSON:', e);
      return;
    }

    const reqId = data.requestId;
    const reqAction = data.action;

    // Correlated response protocol (RC-3): requests may be acknowledged with 'accepted' and are
    // always resolved by a terminal status (completed / cancelled / failed).
    function respond(status, payload, error) {
      if (!reqId) return;
      const msg = {
        requestId: reqId,
        action: reqAction,
        status: status,
        success: status === 'completed',
        payload: payload
      };
      if (error) msg.error = error;
      sendHostMessage(msg);
    }

    function respondSuccess(payload) {
      respond('completed', payload);
    }

    function respondAccepted(payload) {
      respond('accepted', payload);
    }

    try {
      switch (data.action) {
        case 'app.setTheme': {
          const theme = data.payload?.theme || 'dark';
          setTheme(theme);
          respondSuccess({ theme });
          break;
        }
        case 'camera.get': {
          const carto = viewer.camera.positionCartographic;
          respondSuccess({
            latitude: Cesium.Math.toDegrees(carto.latitude),
            longitude: Cesium.Math.toDegrees(carto.longitude),
            altitude: carto.height,
            heading: Cesium.Math.toDegrees(viewer.camera.heading),
            pitch: Cesium.Math.toDegrees(viewer.camera.pitch),
            roll: Cesium.Math.toDegrees(viewer.camera.roll)
          });
          break;
        }
        case 'camera.flyTo': {
          const p = data.payload || {};
          const duration = p.durationSeconds || 1.5;
          // Acknowledge immediately so the host keeps the request alive, then resolve it on
          // complete/cancel. A newer flight supersedes this token (RC-3).
          const token = ++flightToken;
          respondAccepted({ started: true });
          viewer.camera.flyTo({
            destination: Cesium.Cartesian3.fromDegrees(
              p.longitude ?? HOME_LON,
              p.latitude ?? HOME_LAT,
              p.altitude ?? HOME_ALT
            ),
            orientation: {
              heading: Cesium.Math.toRadians(p.heading || 0),
              pitch: Cesium.Math.toRadians(p.pitch || -89.9),
              roll: 0.0
            },
            duration,
            complete: () => {
              if (token !== flightToken) {
                respond('cancelled', { arrived: false, reason: 'superseded' });
                return;
              }
              respondSuccess({ arrived: true });
            },
            cancel: () => {
              respond('cancelled', { arrived: false, reason: token !== flightToken ? 'superseded' : 'cancelled' });
            }
          });
          break;
        }
        case 'camera.home': {
          flyToHome();
          respondSuccess({ home: true });
          break;
        }
        case 'camera.fullExtent': {
          flyToFullExtent();
          respondSuccess({ fullExtent: true });
          break;
        }
        case 'camera.resetNorth': {
          resetNorth();
          respondSuccess({ heading: 0 });
          break;
        }
        case 'camera.resetPitch': {
          resetPitch();
          respondSuccess({ pitch: -90 });
          break;
        }
        case 'camera.zoomIn': {
          zoomIn();
          respondSuccess({ altitude: viewer.camera.positionCartographic.height });
          break;
        }
        case 'camera.zoomOut': {
          zoomOut();
          respondSuccess({ altitude: viewer.camera.positionCartographic.height });
          break;
        }
        case 'camera.setZoomFactor': {
          const factor = setZoomFactor(data.payload?.factor);
          respondSuccess({ factor });
          break;
        }
        case 'camera.fitSelection': {
          fitSelection(data.payload);
          respondSuccess({ fit: true });
          break;
        }
        case 'scene.setMode': {
          const mode = data.payload?.mode || data.payload?.sceneMode || 'Scene3D';
          setSceneMode(mode);
          respondSuccess({ mode });
          break;
        }
        case 'layers.setBasemap': {
          const mode = data.payload?.mode || data.payload?.id || 'esri-hybrid';
          ImageryManager.setBasemap(mode);
          respondSuccess({ basemap: mode });
          break;
        }
        case 'historical.loading': {
          if (!HistoricalTimeline) {respond('failed', null, 'Timeline UI unavailable'); break;}
          if (!HistoricalTimeline.isVisible()) historicalPriorBasemap = ImageryManager.activeBasemap;
          HistoricalTimeline.load();
          respondSuccess({ visible: true, loading: true });
          break;
        }
        case 'historical.show': {
          if (!HistoricalTimeline) {respond('failed', null, 'Timeline UI unavailable'); break;}
          HistoricalTimeline.setReleases(data.payload?.releases);
          respondSuccess({ visible: HistoricalTimeline.isVisible() });
          break;
        }
        case 'historical.error': {
          HistoricalTimeline?.error(String(data.payload?.message || 'Wayback catalog unavailable.'));
          respondSuccess({ errorShown: true });
          break;
        }
        case 'layers.registerBasemap': {
          const registered = ImageryManager.registerDynamicBasemap(data.payload || {});
          respondSuccess(registered);
          break;
        }
        case 'layers.toggleHybridLabels': {
          const show = data.payload?.show !== false;
          ImageryManager.toggleHybridLabels(show);
          respondSuccess({ show });
          break;
        }
        case 'layers.add': {
          const res = ImageryManager.addCustomLayer(data.payload?.layer || data.payload);
          if (res?.success === false) respond('failed', res, res.error || 'Could not add imagery layer');
          else respondSuccess(res);
          break;
        }
        case 'layers.addRasterOverlay': {
          const added = RasterOverlayManager.add(data.payload);
          if (!added) respond('failed', { added: false }, 'Raster overlay could not be added');
          else respondSuccess({ added: true, id: data.payload?.id });
          break;
        }
        case 'layers.removeRasterOverlay': {
          const removed = RasterOverlayManager.remove(data.payload?.id);
          if (!removed) respond('failed', { removed: false }, 'Raster overlay was not found');
          else respondSuccess({ removed: true, id: data.payload?.id });
          break;
        }
        case 'layers.remove': {
          const ok = ImageryManager.removeLayer(data.payload?.id);
          if (!ok) respond('failed', { removed: false }, 'Layer was not found');
          else respondSuccess({ removed: true, id: data.payload?.id });
          break;
        }
        case 'layers.setVisibility': {
          const ok = ImageryManager.setLayerVisibility(data.payload?.id, data.payload?.visible);
          if (!ok) respond('failed', { updated: false }, 'Cannot change visibility: layer was not found');
          else respondSuccess({ updated: true, id: data.payload?.id, visible: data.payload?.visible });
          break;
        }
        case 'layers.setOpacity': {
          const ok = ImageryManager.setLayerOpacity(data.payload?.id, data.payload?.opacity);
          if (!ok) respond('failed', { updated: false }, 'Cannot change opacity: layer was not found');
          else respondSuccess({ updated: true, id: data.payload?.id, opacity: data.payload?.opacity });
          break;
        }
        case 'layers.reorder': {
          const ok = ImageryManager.reorderLayer(data.payload?.id, data.payload?.direction);
          if (!ok) respond('failed', { reordered: false }, 'Cannot reorder: layer was not found');
          else respondSuccess({ reordered: true, id: data.payload?.id });
          break;
        }
        case 'layers.setBorders': {
          // Independent opt-in Esri reference overlay, not the hybrid satellite
          // labels: hiding boundaries must never hide the selected base map.
          const id = 'zearth-country-borders';
          const show = data.payload?.visible === true;
          let result = { success: true };
          if (show) {
            if (ImageryManager.overlayLayers.has(id)) {
              result.success = ImageryManager.setLayerVisibility(id, true);
            } else {
              result = ImageryManager.addCustomLayer({
                id,
                name: 'Esri World Boundaries & Places',
                type: 'RasterTile',
                url: 'https://services.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}',
                attribution: 'Esri World Boundaries and Places — © Esri and data contributors',
                maximumLevel: 19
              });
            }
          } else {
            ImageryManager.removeLayer(id);
          }
          if (result.success === false) respond('failed', null, result.error || 'Boundary overlay unavailable');
          else respondSuccess({ bordersVisible: show, source: 'Esri World Boundaries and Places' });
          break;
        }
        case 'layers.setGraticules': {
          const vis = data.payload?.visible !== false;
          GraticuleManager.setConfig({ visible: vis });
          respondSuccess({ graticules: vis });
          break;
        }
        case 'layers.setGraticulesConfig': {
          GraticuleManager.setConfig(data.payload);
          respondSuccess({ graticules: GraticuleManager.config.visible, configurationApplied: true });
          break;
        }
        case 'credits.setConfig': {
          CreditsManager.setConfig(data.payload);
          respondSuccess({ credits: true });
          break;
        }
        case 'camera.setZoomSensitivity': {
          const factor = setZoomFactor(data.payload?.factor);
          respondSuccess({ zoomFactor: factor });
          break;
        }
        case 'compass.setMode': {
          const mode = data.payload?.mode || 'cardinal';
          CompassManager.setMode(mode);
          respondSuccess({ mode });
          break;
        }
        case 'units.set': {
          UnitSettings.set(data.payload);
          respondSuccess({
            distanceUnit: UnitSettings.distanceUnit,
            areaUnit: UnitSettings.areaUnit,
            elevationUnit: UnitSettings.elevationUnit
          });
          break;
        }
        case 'crs.setMapCrs': {
          respondSuccess(CrsManager.set(data.payload));
          break;
        }
        case 'search.setPin': {
          const shown = SearchPinManager.set(data.payload);
          respondSuccess({ shown });
          break;
        }
        case 'reference.setLayer': {
          respondAccepted({ loading: true, id: data.payload?.id });
          ImageryManager.addReferenceLayer(data.payload).then(result => {
            if (result.success) respondSuccess(result);
            else respond('failed', result, result.error || 'Reference layer failed');
          }).catch(error => respond('failed', null, String(error?.message || error)));
          break;
        }
        case 'reference.setOpacity': {
          const updated = ImageryManager.setLayerOpacity(data.payload?.id, data.payload?.opacity);
          respondSuccess({ updated, id: data.payload?.id, opacity: data.payload?.opacity });
          break;
        }
        case 'swipe.start': {
          const started = SwipeManager.start(data.payload?.left, data.payload?.right);
          if (started) respondSuccess({ started: true });
          else respond('failed', { started: false }, 'The selected basemaps cannot be compared.');
          break;
        }
        case 'swipe.stop': {
          respondSuccess({ stopped: SwipeManager.stop() });
          break;
        }
        case 'device.requestLocation': {
          if (!navigator.geolocation) {
            respond('failed', null, 'This Windows/WebView2 runtime does not expose geolocation.');
            break;
          }
          respondAccepted({ waitingForUserPermission: true });
          navigator.geolocation.getCurrentPosition(
            pos => {
              const lat = pos.coords.latitude;
              const lon = pos.coords.longitude;
              const accuracy = pos.coords.accuracy;
              if (!Number.isFinite(lat) || !Number.isFinite(lon) ||
                  lat < -90 || lat > 90 || lon < -180 || lon > 180) {
                respond('failed', null, 'Device returned invalid latitude/longitude.');
                return;
              }
              // Only on a user-requested fix; never persist, broadcast to providers
              // or equate the device fix with camera/scene center.
              sendHostMessage({
                event: 'device.locationFix', latitude: lat, longitude: lon,
                accuracyM: Number.isFinite(accuracy) ? accuracy : null,
                timestampUtc: new Date(pos.timestamp).toISOString(),
                source: 'device-permission'
              });
              respondSuccess({ acquired: true });
            },
            err => respond('failed', null, err?.message || 'Location denied or unavailable.'),
            { enableHighAccuracy: true, maximumAge: 0, timeout: 12000 }
          );
          break;
        }
        case 'environment.setSunlight': {
          const enabled = EnvironmentManager.setSunlight(data.payload);
          respondSuccess({ enabled });
          break;
        }
        case 'environment.setAtmosphere': {
          const enabled = EnvironmentManager.setAtmosphere(data.payload);
          respondSuccess({ enabled });
          break;
        }
        case 'environment.setTimeOfDay': {
          const timeHours = EnvironmentManager.setTimeOfDay(data.payload?.timeHours, data.payload?.dateUtc);
          respondSuccess({ timeHours });
          break;
        }
        case 'placemarks.set': {
          if (Array.isArray(data.payload)) {
            loadPlacemarks(data.payload);
            respondSuccess({ count: data.payload.length });
          } else {
            respond('failed', null, 'placemarks.set requires an array payload');
          }
          break;
        }
        case 'draw.start': {
          const type = data.payload?.type || 'point';
          DrawingManager.start(type, data.payload?.style);
          respondSuccess({ drawing: true, type });
          break;
        }
        case 'draw.finish': {
          DrawingManager.finish();
          respondSuccess({ finished: true });
          break;
        }
        case 'draw.cancel': {
          DrawingManager.cancel();
          respondSuccess({ cancelled: true });
          break;
        }
        case 'snap.setConfig': {
          SnapManager.setConfig(data.payload);
          respondSuccess({
            snap: true,
            enabled: SnapManager.enabled,
            tolerancePixels: SnapManager.tolerancePixels,
            groundToleranceMeters: SnapManager.groundToleranceMeters,
            mode: SnapManager.mode,
            snapVertices: SnapManager.snapVertices,
            snapMidpoints: SnapManager.snapMidpoints,
            snapEdges: SnapManager.snapEdges
          });
          break;
        }
        case 'measure.start': {
          const type = data.payload?.type || 'distance';
          MeasurementManager.start(type, data.payload?.options);
          respondSuccess({ measuring: true, type });
          break;
        }
        case 'measure.finish': {
          MeasurementManager.finish();
          respondSuccess({ finished: true });
          break;
        }
        case 'measure.clear': {
          MeasurementManager.clear();
          respondSuccess({ cleared: true });
          break;
        }
        case 'terrain.setProvider': {
          const type = data.payload?.type || 'ellipsoid';
          const url = data.payload?.url;
          const ionToken = data.payload?.ionToken;
          TerrainManager.setProvider(type, url, ionToken).then(res => {
            if (res && res.success === false) {
              respond('failed', res, res.error || 'Terrain provider unavailable');
            } else {
              respondSuccess(res);
            }
          }).catch(err => respond('failed', null, String(err?.message || err)));
          break;
        }
        case 'terrain.setExaggeration': {
          const factor = typeof data.payload?.factor === 'number' ? data.payload.factor : 1.0;
          const applied = TerrainManager.setExaggeration(factor);
          respondSuccess({ exaggeration: applied });
          break;
        }
        case 'terrain.resetExaggeration': {
          const applied = TerrainManager.resetExaggeration();
          respondSuccess({ exaggeration: applied });
          break;
        }
        case 'request.cancel': {
          // The host abandoned a request (typically a camera flight that exceeded its timeout).
          // Abort the orphaned work so the globe does not keep animating/streaming for a command the
          // host is no longer tracking (RC-3).
          const target = (data.payload && data.payload.action) || '';
          if (target.indexOf('camera.') === 0 && viewer && viewer.camera &&
              typeof viewer.camera.cancelFlight === 'function') {
            flightToken++; // invalidate any pending complete/cancel callbacks of the old flight
            viewer.camera.cancelFlight();
          }
          respondSuccess({ cancelled: true, action: target });
          break;
        }
        default: {
          // Unknown actions are resolved immediately with a terminal failed status so the host
          // never blocks until its request timeout (RC-3).
          respond('failed', null, `Unsupported action: ${data.action}`);
          break;
        }
      }
    } catch (err) {
      console.error('Error handling host message:', err);
      respond('failed', null, String(err && err.message ? err.message : err));
    }
  }

  // 100-cycle 2D/3D mode transition stress test (ZE-038, AC-013)
  async function runModeToggleStressTest(cycles = 100) {
    const results = { totalCycles: cycles, completedCycles: 0, passed: false, errors: [] };
    const originalMode = viewer.scene.mode;

    try {
      for (let i = 0; i < cycles; i++) {
        const targetMode = (i % 2 === 0) ? 'Scene2D' : 'Scene3D';
        setSceneMode(targetMode);
        results.completedCycles++;

        if (i % 10 === 0) {
          await new Promise(r => setTimeout(r, 16));
        }
      }

      // Restore original mode cleanly
      setSceneMode(originalMode === Cesium.SceneMode.SCENE3D ? 'Scene3D' : 'Scene2D');
      await new Promise(r => setTimeout(r, 40));

      const carto = viewer.camera.positionCartographic;
      if (carto && !isNaN(carto.height) && results.completedCycles === cycles) {
        results.passed = true;
      }
    } catch (e) {
      results.errors.push(e.toString());
    }

    return results;
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => handleHostMessage(event.data));
  }
  window.addEventListener('message', (event) => handleHostMessage(event.data));

  // Expose global for diagnostics & automated verification
  window.ZEarthGlobe = {
    viewer,
    Cesium,
    ImageryManager,
    TerrainManager,
    DrawingManager,
    SnapManager,
    MeasurementManager,
    runModeToggleStressTest,
    flyToHome,
    flyToFullExtent,
    resetNorth,
    resetPitch,
    zoomIn,
    zoomOut,
    setZoomFactor,
    fitSelection,
    runDragStressTest,
    setSceneMode,
    setTheme,
    setTerrainProvider: (type, url, ionToken) => TerrainManager.setProvider(type, url, ionToken),
    setTerrainExaggeration: (factor) => TerrainManager.setExaggeration(factor),
    resetTerrainExaggeration: () => TerrainManager.resetExaggeration(),
    sendCommand: (cmd) => sendHostMessage({ command: cmd })
  };

  // Initialize managers (all `const` managers are now defined, so cross-references are safe)
  GraticuleManager.init();
  CreditsManager.init();
  CompassManager.init();
  ImageryManager.init();

  // Expose on window.ZEarthGlobe
  window.ZEarthGlobe.GraticuleManager = GraticuleManager;
  window.ZEarthGlobe.CreditsManager = CreditsManager;
  window.ZEarthGlobe.CompassManager = CompassManager;
  window.ZEarthGlobe.getScreenCenterGround = getScreenCenterGround;

  // Notify host that the engine is ready
  sendHostMessage({
    event: 'engine.ready',
    version: Cesium.VERSION,
    webgl: !!viewer.scene.context._gl,
    webgl2: !!viewer.scene.context._webgl2,
    initialCamera: {
      latitude: HOME_LAT,
      longitude: HOME_LON,
      altitude: HOME_ALT
    }
  });

  console.log(`Z EARTH CesiumJS Geographic Engine v${Cesium.VERSION} initialized successfully.`);
})();
