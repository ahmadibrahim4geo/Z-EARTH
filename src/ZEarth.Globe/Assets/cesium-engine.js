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

  // Viewport engine locale state (pure English vs pure Arabic UI)
  const EngineLocale = {
    isArabic: false
  };

  function updateEngineLocale(isArabic) {
    EngineLocale.isArabic = Boolean(isArabic);
    const qiblaTitle = document.getElementById('qibla-title');
    if (qiblaTitle) qiblaTitle.textContent = isArabic ? 'القبلة' : 'Qibla';
    const qiblaWarning = document.getElementById('qibla-warning');
    if (qiblaWarning) qiblaWarning.textContent = isArabic ? 'وجه نحو الأرض' : 'Point at Earth';
    const btnNorthReset = document.getElementById('btn-north-reset');
    if (btnNorthReset) {
      btnNorthReset.textContent = isArabic ? 'الشمال لأعلى' : 'North Up';
      btnNorthReset.title = isArabic ? 'إعادة ضبط الشمال لأعلى' : 'Reset North Up (N)';
    }
    if (typeof WorldGazetteerManager !== 'undefined' && WorldGazetteerManager && WorldGazetteerManager.setLocale) {
      WorldGazetteerManager.setLocale(EngineLocale.isArabic);
    }
  }

  // Camera state memory for recovery and stress protection
  let lastSafeCamera = {
    position: Cesium.Cartesian3.fromDegrees(HOME_LON, HOME_LAT, HOME_ALT),
    heading: 0.0,
    pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
    roll: 0.0
  };

  let WeatherStationsManager = null;
  let WorldGazetteerManager = null;

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

      // D. CartoDB Dark Matter / Dark Canvas (Free Esri Dark Canvas to avoid API Key watermarks)
      registerTileLayer(
        'carto-dark',
        'CartoDB Dark Matter',
        'https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}',
        '© Esri, HERE, Garmin, © OpenStreetMap contributors',
        16
      );

      // E. CartoDB Positron / Light Canvas (Free Esri Light Canvas to avoid API Key watermarks)
      registerTileLayer(
        'carto-light',
        'CartoDB Positron (Light)',
        'https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}',
        '© Esri, HERE, Garmin, © OpenStreetMap contributors',
        16
      );

      registerTileLayer(
        'carto-voyager',
        'CARTO Voyager',
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',
        '© Esri, HERE, Garmin, USGS, © OpenStreetMap contributors',
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

      // The local provisional layer is not a user selection. Do not publish it as
      // a basemap change before engine.ready: WPF still needs its persisted choice.
      this.setBasemap('offline-vector', false);
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

    setBasemap(id, notifyHost = true) {
      if (!id) id = 'offline-vector';
      id = String(id).trim().toLowerCase();
      if (id === 'none' || id === 'off') {
        this.activeBasemap = 'off';
        for (const [key, base] of Object.entries(this.baseLayers)) {
          if (base && base.layer) base.layer.show = false;
        }
        if (this.labelsLayer) this.labelsLayer.show = false;
        viewer.scene.globe.baseColor = Cesium.Color.fromCssColorString('#0b192c');
        viewer.scene.requestRender();
        if (notifyHost) sendHostMessage({ event: 'basemap.changed', basemap: 'off' });
        return true;
      }
      if (id === 'offline' || id === 'naturalearth') id = 'offline-vector';
      if (id === 'osm' || id === 'openstreetmap') id = 'osm-standard';
      if (id === 'esri' || id === 'satellite' || id === 'hybrid') id = 'esri-hybrid';

      if (!this.baseLayers[id] && id !== 'offline-vector') {
        console.warn('[ImageryManager] Unknown basemap requested:', id);
        return false;
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
      if (notifyHost) sendHostMessage({ event: 'imagery.basemapChanged', basemap: id });
      return true;
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

    activeOverpassLayers: new Map(),

    async queryOverpassFeatures(config, west, east, south, north) {
      const bbox = `${south},${west},${north},${east}`;
      const query = String(config.query || '').replaceAll('{bbox}', bbox);
      if (!query) return null;
      const response = await fetch(`${config.url}?data=${encodeURIComponent(query)}`);
      if (!response.ok) return null;
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
      return features;
    },

    async updateOverpassLayer(config) {
      const rect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      if (!rect) return { success: true, pendingZoom: true };
      const west = Cesium.Math.toDegrees(rect.west);
      const east = Cesium.Math.toDegrees(rect.east);
      const south = Cesium.Math.toDegrees(rect.south);
      const north = Cesium.Math.toDegrees(rect.north);
      const lonSpan = east >= west ? east - west : 360 - west + east;
      const latSpan = north - south;
      if (lonSpan > 12 || latSpan > 12) {
        return { success: true, id: config.id, pendingZoom: true };
      }
      const features = await this.queryOverpassFeatures(config, west, east, south, north);
      if (!features) return { success: true, id: config.id };

      const existing = this.overlayLayers.get(config.id);
      if (existing && existing.dataSource) {
        viewer.dataSources.remove(existing.dataSource, true);
      }

      const geojson = { type: 'FeatureCollection', features };
      const strokeColor = config.id.includes('water') ? '#38bdf8' : (config.id.includes('building') ? '#fbbf24' : '#22c55e');
      const source = await Cesium.GeoJsonDataSource.load(geojson, {
        clampToGround: true,
        stroke: Cesium.Color.fromCssColorString(strokeColor),
        fill: Cesium.Color.fromCssColorString(strokeColor).withAlpha(0.25),
        strokeWidth: 2
      });
      source.name = config.name || config.id;
      await viewer.dataSources.add(source);
      this.overlayLayers.set(config.id, { id: config.id, name: config.name, dataSource: source, attribution: config.attribution || '', opacity: config.opacity ?? 1.0 });
      this.updateAttribution();
      viewer.scene.requestRender();
      return { success: true, id: config.id, featureCount: features.length };
    },

    onCameraMoveEnd() {
      if (this.activeOverpassLayers && this.activeOverpassLayers.size > 0) {
        for (const [id, config] of this.activeOverpassLayers.entries()) {
          this.updateOverpassLayer(config).catch(() => {});
        }
      }
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
            const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
            const date = config.dateOrTimestamp || yesterday;
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
        if (config.id === 'ref_weather_stations' && WeatherStationsManager) {
          WeatherStationsManager.setEnabled(false);
        }
        if (config.id === 'ref_world_gazetteer' && WorldGazetteerManager) {
          WorldGazetteerManager.setEnabled(false);
        }
        return { success: true, removed: this.removeLayer(config.id), id: config.id };
      }

      const normalizedType = String(config.layerType || config.type || '').toLowerCase();

      if (config.id === 'ref_weather_stations' || normalizedType === 'weatherstationsgeojson') {
        if (WeatherStationsManager) {
          WeatherStationsManager.setEnabled(true);
          return { success: true, id: config.id };
        }
      }

      if (config.id === 'ref_world_gazetteer' || normalizedType === 'worldgazetteer') {
        if (WorldGazetteerManager) {
          WorldGazetteerManager.setEnabled(true);
          return { success: true, id: config.id };
        }
      }

      if (config.id === 'ref_world_countries' || normalizedType === 'worldcountries') {
        this.removeLayer(config.id);
        if (window.ZEARTH_WORLD_COUNTRIES) {
          try {
            const source = await Cesium.GeoJsonDataSource.load(window.ZEARTH_WORLD_COUNTRIES, {
              clampToGround: true,
              stroke: Cesium.Color.fromCssColorString('#38bdf8'),
              fill: Cesium.Color.fromCssColorString('#0284c7').withAlpha(0.12),
              strokeWidth: 2
            });
            source.name = 'World Country Boundaries';
            await viewer.dataSources.add(source);
            source.show = true;
            this.overlayLayers.set(config.id, {
              id: config.id,
              name: 'World Country Boundaries',
              dataSource: source,
              attribution: 'Natural Earth 110m (Public Domain)'
            });
            viewer.scene.requestRender();
            return { success: true, id: config.id };
          } catch (e) {
            return { success: false, error: String(e) };
          }
        }
      }

      if (normalizedType === 'userlocalvector') {
        this.removeLayer(config.id);
        try {
          const loadPayload = config.geojson || config.url;
          if (loadPayload) {
            const source = await Cesium.GeoJsonDataSource.load(loadPayload, {
              clampToGround: true,
              stroke: Cesium.Color.fromCssColorString('#38bdf8'),
              fill: Cesium.Color.fromCssColorString('#38bdf8').withAlpha(0.25),
              strokeWidth: 2,
              markerColor: Cesium.Color.fromCssColorString('#38bdf8')
            });
            source.name = config.name || config.id;
            await viewer.dataSources.add(source);
            source.show = true;
            this.overlayLayers.set(config.id, {
              id: config.id,
              name: config.name,
              dataSource: source,
              attribution: config.attribution || 'User Local Layer',
              opacity: typeof config.opacity === 'number' ? config.opacity : 1.0
            });
            this.updateAttribution();
            viewer.scene.requestRender();
            return { success: true, id: config.id };
          }
        } catch (e) {
          sendHostMessage({ event: 'imagery.error', layerId: config.id, message: String(e?.message || e) });
          return { success: false, error: String(e?.message || e) };
        }
      }

      if (normalizedType === 'overpassgeojson') {
        this.removeLayer(config.id);
        if (!this.activeOverpassLayers) this.activeOverpassLayers = new Map();
        this.activeOverpassLayers.set(config.id, config);
        try {
          return await this.updateOverpassLayer(config);
        } catch (e) {
          return { success: true, id: config.id, pendingZoom: true };
        }
      }

      if (normalizedType === 'wikimediageosearch' || normalizedType === 'wikipediageosearch') {
        this.removeLayer(config.id);
        try {
          const center = getScreenCenterGround();
          const cam = viewer.camera.positionCartographic;
          const lat = center.valid ? center.latitude : Cesium.Math.toDegrees(cam.latitude);
          const lon = center.valid ? center.longitude : Cesium.Math.toDegrees(cam.longitude);
          if (!Number.isFinite(lat) || !Number.isFinite(lon)) throw new Error('Center the globe on a ground location first.');
          const coord = `${lat}|${lon}`;
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
            const itemLat = Number(coordinate.lat);
            const itemLon = Number(coordinate.lon);
            if (!Number.isFinite(itemLat) || !Number.isFinite(itemLon)) continue;
            const title = String(page.title || 'Nearby place');
            const thumb = page.imageinfo?.[0]?.thumburl;
            source.entities.add({
              name: title,
              position: Cesium.Cartesian3.fromDegrees(itemLon, itemLat, 40),
              point: thumb ? undefined : { pixelSize: 9, color: Cesium.Color.fromCssColorString('#38bdf8'), outlineColor: Cesium.Color.WHITE, outlineWidth: 2, heightReference: Cesium.HeightReference.CLAMP_TO_GROUND },
              billboard: thumb ? { image: thumb, width: 48, height: 48, verticalOrigin: Cesium.VerticalOrigin.BOTTOM, heightReference: Cesium.HeightReference.CLAMP_TO_GROUND, disableDepthTestDistance: 0 } : undefined,
              label: { text: title, font: '11px Segoe UI, sans-serif', fillColor: Cesium.Color.WHITE, outlineColor: Cesium.Color.BLACK, outlineWidth: 3, style: Cesium.LabelStyle.FILL_AND_OUTLINE, pixelOffset: new Cesium.Cartesian2(0, thumb ? -54 : -14), distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 3000000), disableDepthTestDistance: 0 },
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
          for (const entity of source.entities.values) {
            const props = entity.properties;
            const title = props?.title ? props.title.getValue() : (entity.name || 'Feature');
            entity.name = title;
            if (entity.billboard) {
              entity.billboard.heightReference = Cesium.HeightReference.CLAMP_TO_GROUND;
            }
            entity.label = {
              text: title,
              font: '11px Segoe UI, sans-serif',
              fillColor: Cesium.Color.WHITE,
              outlineColor: Cesium.Color.BLACK,
              outlineWidth: 3,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              pixelOffset: new Cesium.Cartesian2(0, -18),
              distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 15000000)
            };
          }
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
      if (id === 'ref_weather_stations' && WeatherStationsManager) {
        WeatherStationsManager.setEnabled(false);
      }
      if (id === 'ref_world_gazetteer' && WorldGazetteerManager) {
        WorldGazetteerManager.setEnabled(false);
      }
      if (this.activeOverpassLayers) {
        this.activeOverpassLayers.delete(id);
      }
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
    providerRequestGeneration: 0,
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
      promise.catch(() => { if (this.tileCache.get(key) === promise) this.tileCache.delete(key); });
      if (this.tileCache.size > 160) {
        const oldestKey = this.tileCache.keys().next().value;
        this.tileCache.delete(oldestKey);
      }
      return promise;
    },

    async loadLocalRasterTile(x, y, level) {
      const key = `local/${level}/${x}/${y}`;
      if (this.tileCache.has(key)) return this.tileCache.get(key);
      const promise = (async () => {
        const abortController = new AbortController();
        const timeoutId = window.setTimeout(() => abortController.abort(), 25000);
        let response;
        try {
          response = await fetch(`https://terrain.local/local-dem/${level}/${x}/${y}.bin`, {
            mode: 'cors', cache: 'no-store', signal: abortController.signal
          });
        } finally {
          window.clearTimeout(timeoutId);
        }
        if (!response.ok) throw new Error(`Local terrain tile HTTP ${response.status}`);
        const buffer = await response.arrayBuffer();
        const expectedBytes = this.gridSize * this.gridSize * 4;
        if (buffer.byteLength !== expectedBytes) {
          throw new Error(`Local terrain tile has ${buffer.byteLength} bytes; expected ${expectedBytes}`);
        }
        // Preserve NaN as the coverage mask; zero can be a real elevation.
        return new Float32Array(buffer);
      })();
      this.tileCache.set(key, promise);
      promise.catch(() => { if (this.tileCache.get(key) === promise) this.tileCache.delete(key); });
      if (this.tileCache.size > 160) this.tileCache.delete(this.tileCache.keys().next().value);
      return promise;
    },

    // Missing cells are ellipsoid for rendering only, never measurements.
    async loadLocalRasterRenderTile(x, y, level) {
      const raw = await this.loadLocalRasterTile(x, y, level);
      const size = this.gridSize;
      // +180 and -180 are the same meridian. GDAL's half-open eastern
      // boundary can return NaN there; use the real western-edge samples.
      let westEdge = null;
      if (x === 2 ** level - 1 && raw.some((value, i) =>
          i % size === size - 1 && !Number.isFinite(value))) {
        westEdge = x === 0 ? raw : await this.loadLocalRasterTile(0, y, level);
      }
      return Float32Array.from(raw, (elevation, i) => {
        if (Number.isFinite(elevation)) return elevation;
        if (westEdge && i % size === size - 1) {
          const wrapped = westEdge[i - (size - 1)];
          if (Number.isFinite(wrapped)) return wrapped;
        }
        return 0;
      });
    },

    async sampleLocalRasterPoints(points) {
      const level = 18, n = 2 ** level, size = this.gridSize;
      return Promise.all(points.map(async point => {
        if (Math.abs(point.latitude) > 85.05112878) return { ...point, height: NaN };
        const normalized = ((point.longitude + 180) % 360 + 360) % 360;
        const globalX = normalized / 360 * n;
        const lat = point.latitude * Math.PI / 180;
        const globalY = (1 - Math.log(Math.tan(lat) + 1 / Math.cos(lat)) / Math.PI) / 2 * n;
        const x = Math.max(0, Math.min(n - 1, Math.floor(globalX)));
        const y = Math.max(0, Math.min(n - 1, Math.floor(globalY)));
        const column = Math.max(0, Math.min(size - 1, (globalX - x) * (size - 1)));
        const row = Math.max(0, Math.min(size - 1, (globalY - y) * (size - 1)));
        const x0 = Math.floor(column), x1 = Math.min(size - 1, x0 + 1);
        const y0 = Math.floor(row), y1 = Math.min(size - 1, y0 + 1);
        const heights = await this.loadLocalRasterTile(x, y, level);
        const a = heights[y0 * size + x0], b = heights[y0 * size + x1];
        const c = heights[y1 * size + x0], d = heights[y1 * size + x1];
        // Conservatively reject boundaries and holes if an input is missing.
        if (![a, b, c, d].every(Number.isFinite)) return { ...point, height: NaN };
        const u = column - x0, v = row - y0;
        const height = (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v;
        return { ...point, height };
      }));
    },

    async sampleBackground30mDem(points) {
      const level = 12, n = 2 ** level, size = this.gridSize;
      return Promise.all(points.map(async point => {
        if (Math.abs(point.latitude) > 85.05112878) return { ...point, height: 0 };
        const normalized = ((point.longitude + 180) % 360 + 360) % 360;
        const globalX = normalized / 360 * n;
        const lat = point.latitude * Math.PI / 180;
        const globalY = (1 - Math.log(Math.tan(lat) + 1 / Math.cos(lat)) / Math.PI) / 2 * n;
        const x = Math.max(0, Math.min(n - 1, Math.floor(globalX)));
        const y = Math.max(0, Math.min(n - 1, Math.floor(globalY)));
        const column = Math.max(0, Math.min(size - 1, (globalX - x) * (size - 1)));
        const row = Math.max(0, Math.min(size - 1, (globalY - y) * (size - 1)));
        const x0 = Math.floor(column), x1 = Math.min(size - 1, x0 + 1);
        const y0 = Math.floor(row), y1 = Math.min(size - 1, y0 + 1);
        try {
          const heights = await this.loadTerrariumTile(x, y, level);
          const a = heights[y0 * size + x0], b = heights[y0 * size + x1];
          const c = heights[y1 * size + x0], d = heights[y1 * size + x1];
          if (![a, b, c, d].every(Number.isFinite)) return { ...point, height: 0 };
          const u = column - x0, v = row - y0;
          const height = (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v;
          return { ...point, height: Math.round(height * 10) / 10 };
        } catch {
          return { ...point, height: 0 };
        }
      }));
    },

    async loadHostFloatTile(source, x, y, level, providerToken) {
      const key = `${source}/${level}/${x}/${y}`;
      if (this.tileCache.has(key)) return this.tileCache.get(key);
      const promise = (async () => {
        const headers = {};
        if (providerToken) headers['X-ZEarth-Provider-Token'] = providerToken;
        const response = await fetch(`https://terrain.local/${source}/${level}/${x}/${y}.bin`, {
          mode: 'cors', cache: 'no-store', headers
        });
        if (!response.ok) throw new Error(`${source} terrain tile HTTP ${response.status}`);
        const buffer = await response.arrayBuffer();
        const expectedBytes = this.gridSize * this.gridSize * 4;
        if (buffer.byteLength !== expectedBytes) {
          throw new Error(`${source} terrain tile has ${buffer.byteLength} bytes; expected ${expectedBytes}`);
        }
        const heights = new Float32Array(buffer);
        for (const elevation of heights) {
          if (!Number.isFinite(elevation)) throw new Error(`${source} terrain tile contains unresolved NoData`);
        }
        return heights;
      })();
      this.tileCache.set(key, promise);
      promise.catch(() => { if (this.tileCache.get(key) === promise) this.tileCache.delete(key); });
      return promise;
    },

    async getJaxaAw3dGenerator() {
      if (!this.jaxaAw3dGeneratorPromise) {
        this.jaxaAw3dGeneratorPromise = (async () => {
          const je = await import('https://data.earth.jaxa.jp/api/javascript/v2.0.1/jaxa.earth.esm.js');
          const generator = new je.TileGenerator({
            collectionUrl: 'https://s3.ap-northeast-1.wasabisys.com/je-pds/cog/v1/JAXA.EORC_ALOS.PRISM_AW3D30.v3.2_global/collection.json',
            band: 'DSM',
            resampling: je.Resampling.BILINEAR,
            tileSize: 256,
            delay: 100
          });
          await generator.init();
          return generator;
        })();
        this.jaxaAw3dGeneratorPromise.catch(() => { this.jaxaAw3dGeneratorPromise = null; });
      }
      return this.jaxaAw3dGeneratorPromise;
    },

    async loadJaxaAw3dTile(x, y, level) {
      const key = `aw3d30/${level}/${x}/${y}`;
      if (this.tileCache.has(key)) return this.tileCache.get(key);
      const promise = (async () => {
        const generator = await this.getJaxaAw3dGenerator();
        const dataObject = await generator.getDataObject(x, y, level);
        if (!dataObject || dataObject.width !== 256 || dataObject.height !== 256) {
          throw new Error('JAXA Earth API returned an unexpected AW3D30 tile');
        }
        const heights = new Float32Array(this.gridSize * this.gridSize);
        for (let row = 0; row < this.gridSize; row++) {
          const sourceRow = Math.round(row * 255 / (this.gridSize - 1));
          for (let column = 0; column < this.gridSize; column++) {
            const sourceColumn = Math.round(column * 255 / (this.gridSize - 1));
            const elevation = dataObject.data[sourceRow * 256 + sourceColumn];
            if (!Number.isFinite(elevation)) throw new Error('AW3D30 has no numeric data for this tile');
            heights[row * this.gridSize + column] = elevation;
          }
        }
        return heights;
      })();
      this.tileCache.set(key, promise);
      promise.catch(() => { if (this.tileCache.get(key) === promise) this.tileCache.delete(key); });
      return promise;
    },

    async verifyViewportLoader(loader) {
      const ground = getScreenCenterGround();
      const camera = viewer.camera.positionCartographic;
      const lon = ground.valid ? ground.longitude : Cesium.Math.toDegrees(camera.longitude);
      const lat = ground.valid ? ground.latitude : Cesium.Math.toDegrees(camera.latitude);
      const level = 8, n = 2 ** level;
      const x = Math.min(n - 1, Math.floor((((lon + 180) % 360 + 360) % 360) / 360 * n));
      const radians = Cesium.Math.toRadians(Math.max(-85.05112878, Math.min(85.05112878, lat)));
      const y = Math.max(0, Math.min(n - 1,
        Math.floor((1 - Math.log(Math.tan(radians) + 1 / Math.cos(radians)) / Math.PI) / 2 * n)));
      const heights = await loader(x, y, level);
      let minimumElevationM = Infinity, maximumElevationM = -Infinity;
      for (const elevation of heights) {
        minimumElevationM = Math.min(minimumElevationM, elevation);
        maximumElevationM = Math.max(maximumElevationM, elevation);
      }
      return { level, x, y, sampleCount: heights.length, minimumElevationM, maximumElevationM };
    },

    async activateLocalRasterProvider(generation = this.providerRequestGeneration) {
      this.tileCache.clear();
      // A regional file need not overlap the camera or even contain a sample on
      // the coarse world root. Probe an actual in-coverage tile selected by GDAL.
      const response = await fetch('https://terrain.local/local-dem/metadata', {
        mode: 'cors', cache: 'no-store'
      });
      if (!response.ok) throw new Error(`Local terrain metadata HTTP ${response.status}`);
      const metadata = await response.json();
      const level = metadata.probeLevel, x = metadata.probeX, y = metadata.probeY;
      if (!Number.isInteger(level) || level < 0 || level > 18 ||
          !Number.isInteger(x) || !Number.isInteger(y) || x < 0 || y < 0 ||
          x >= 2 ** level || y >= 2 ** level) {
        throw new Error('Local terrain has no valid coverage probe');
      }
      const [root, heights] = await Promise.all([
        this.loadLocalRasterTile(0, 0, 0), this.loadLocalRasterTile(x, y, level)
      ]);
      if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
      if (root.length !== this.gridSize * this.gridSize) throw new Error('Invalid local terrain root');
      const valid = Array.from(heights).filter(Number.isFinite);
      if (!valid.length) throw new Error('No numeric DEM samples found in local raster coverage');
      const probe = { level, x, y, sampleCount: valid.length,
        minimumElevationM: Math.min(...valid), maximumElevationM: Math.max(...valid) };
      const label = 'Local numeric elevation raster (ellipsoid outside raster coverage)';
      viewer.terrainProvider = new Cesium.CustomHeightmapTerrainProvider({
        width: this.gridSize,
        height: this.gridSize,
        tilingScheme: new Cesium.WebMercatorTilingScheme(),
        callback: (tileX, tileY, tileLevel) => tileLevel > 18 ? undefined :
          this.loadLocalRasterRenderTile(tileX, tileY, tileLevel),
        credit: 'Local elevation raster; ellipsoid shown where raster has no elevation data'
      });
      viewer.scene.globe.depthTestAgainstTerrain = true;
      viewer.scene.globe.maximumScreenSpaceError = 1.5;
      if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
      this.currentType = 'localDem';
      this.setExaggeration(this.currentExaggeration);
      sendHostMessage({ event: 'terrain.changed', type: 'localDem', label, probe });
      viewer.scene.requestRender();
      return { success: true, type: 'localDem', label, probe };
    },

    async activateHeightmapProvider(type, label, credit, loader, generation = this.providerRequestGeneration) {
      this.tileCache.clear();
      // A viewport tile alone cannot establish renderability: Cesium first
      // requests the root tile. Reject incomplete roots instead of claiming
      // that a partial/local raster is active while the globe remains flat.
      const [, probe] = await Promise.all([
        loader(0, 0, 0),
        this.verifyViewportLoader(loader)
      ]);
      if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
      viewer.terrainProvider = new Cesium.CustomHeightmapTerrainProvider({
        width: this.gridSize,
        height: this.gridSize,
        tilingScheme: new Cesium.WebMercatorTilingScheme(),
        callback: (x, y, level) => level > 14 ? undefined : loader(x, y, level),
        credit
      });
      viewer.scene.globe.depthTestAgainstTerrain = true;
      viewer.scene.globe.maximumScreenSpaceError = 1.5;
      if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
      this.currentType = type;
      this.setExaggeration(this.currentExaggeration);
      sendHostMessage({ event: 'terrain.changed', type, label, probe });
      viewer.scene.requestRender();
      return { success: true, type, label, probe };
    },

    supersededProviderResult() {
      return { success: false, superseded: true, error: 'Terrain selection was superseded by a newer request.' };
    },

    async setProvider(type, url, ionToken) {
      const generation = ++this.providerRequestGeneration;
      type = type || 'ellipsoid';
      if ((type === 'worldTerrain' || type === 'worldBathymetry') && ionToken && Cesium.Ion) {
        Cesium.Ion.defaultAccessToken = String(ionToken).trim();
      }
      if (type === 'localDem') {
        try {
          return await this.activateLocalRasterProvider(generation);
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          console.warn('[TerrainManager] Local raster terrain failed:', err);
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Local raster unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'aw3d30') {
        try {
          return await this.activateHeightmapProvider(type, 'JAXA AW3D30 (JAXA Earth API)',
            'Elevation data © JAXA; obtained using JAXA Earth API',
            (x, y, level) => this.loadJaxaAw3dTile(x, y, level), generation);
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'JAXA AW3D30 unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'copernicusGlo30') {
        try {
          const token = String(ionToken || '').trim();
          if (!token) throw new Error('Copernicus Data Space access token is required for GLO-30');
          return await this.activateHeightmapProvider(type, 'Copernicus DEM GLO-30 (CDSE Process API)',
            'Copernicus DEM GLO-30 © European Union, accessed through CDSE',
            (x, y, level) => this.loadHostFloatTile('copernicus-glo30', x, y, level, token), generation);
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'Copernicus GLO-30 unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'gebco2026') {
        try {
          return await this.activateHeightmapProvider(type, 'GEBCO 2026 Grid (Australian Antarctic Division / Source Cooperative)',
            'GEBCO Bathymetric Compilation Group 2026; cloud-optimized GeoTIFF by Australian Antarctic Division via Source Cooperative',
            (x, y, level) => this.loadHostFloatTile('gebco2026', x, y, level), generation);
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'GEBCO 2026 unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'srtm30') {
        try {
          const result = await this.activateHeightmapProvider(type, 'SRTM-backed AWS Terrain Tiles',
            'Elevation data © Mapzen/Tilezen and data providers, hosted by AWS Open Data; SRTM is used in covered source regions',
            (x, y, level) => this.loadTerrariumTile(x, y, level), generation);
          return result;
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider();
          this.currentType = 'ellipsoid';
          sendHostMessage({ event: 'terrain.fallback', reason: err?.message || 'SRTM-backed terrain unavailable' });
          return { success: false, fallback: 'ellipsoid', error: String(err?.message || err) };
        }
      } else if (type === 'demTerrain' || type === 'globalDem') {
        try {
          // Do not report Terrain as active until a real elevation tile has passed through
          // the host proxy and decoded successfully.
          // Fetch independently in parallel; both real numeric tiles must pass.
          const [, probe] = await Promise.all([
            this.loadTerrariumTile(0, 0, 0),
            this.verifyViewportTerrainTile()
          ]);
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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
            if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = tp;
          viewer.scene.globe.depthTestAgainstTerrain = true;
          if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
          this.currentType = 'worldBathymetry';
          this.setExaggeration(this.currentExaggeration);
          sendHostMessage({ event: 'terrain.changed', type: 'worldBathymetry', label: 'Cesium World Bathymetry / GEBCO 2023' });
          viewer.scene.requestRender();
          return { success: true, type: 'worldBathymetry', label: 'Cesium World Bathymetry / GEBCO 2023' };
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
          viewer.terrainProvider = tp;
          viewer.scene.globe.depthTestAgainstTerrain = true;
          if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) viewer.scene.morphTo3D(0.6);
          this.currentType = 'custom';
          this.setExaggeration(this.currentExaggeration);
          sendHostMessage({ event: 'terrain.changed', type: 'custom' });
          viewer.scene.requestRender();
          return { success: true, type: 'custom' };
        } catch (err) {
          if (generation !== this.providerRequestGeneration) return this.supersededProviderResult();
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

  function shapeArabicText(text) {
    if (!text || !/[\u0600-\u06ff\u0750-\u077f\u08a0-\u08ff]/.test(text)) return String(text || '');

    const CHAR_MAP = {
      0x0621: [0xfe80, 0xfe80, 0xfe80, 0xfe80],
      0x0622: [0xfe81, 0xfe82, 0xfe81, 0xfe82],
      0x0623: [0xfe83, 0xfe84, 0xfe83, 0xfe84],
      0x0624: [0xfe85, 0xfe86, 0xfe85, 0xfe86],
      0x0625: [0xfe87, 0xfe88, 0xfe87, 0xfe88],
      0x0626: [0xfe89, 0xfe8a, 0xfe8b, 0xfe8c],
      0x0627: [0xfe8d, 0xfe8e, 0xfe8d, 0xfe8e],
      0x0628: [0xfe8f, 0xfe90, 0xfe91, 0xfe92],
      0x0629: [0xfe93, 0xfe94, 0xfe93, 0xfe94],
      0x062a: [0xfe95, 0xfe96, 0xfe97, 0xfe98],
      0x062b: [0xfe99, 0xfe9a, 0xfe9b, 0xfe9c],
      0x062c: [0xfe9d, 0xfe9e, 0xfe9f, 0xfea0],
      0x062d: [0xfea1, 0xfea2, 0xfea3, 0xfea4],
      0x062e: [0xfea5, 0xfea6, 0xfea7, 0xfea8],
      0x062f: [0xfea9, 0xfeaa, 0xfea9, 0xfeaa],
      0x0630: [0xfeab, 0xfeac, 0xfeab, 0xfeac],
      0x0631: [0xfead, 0xfeae, 0xfead, 0xfeae],
      0x0632: [0xfeaf, 0xfeb0, 0xfeaf, 0xfeb0],
      0x0633: [0xfeb1, 0xfeb2, 0xfeb3, 0xfeb4],
      0x0634: [0xfeb5, 0xfeb6, 0xfeb7, 0xfeb8],
      0x0635: [0xfeb9, 0xfeba, 0xfebb, 0xfebc],
      0x0636: [0xfebd, 0xfebe, 0xfebf, 0xfec0],
      0x0637: [0xfec1, 0xfec2, 0xfec3, 0xfec4],
      0x0638: [0xfec5, 0xfec6, 0xfec7, 0xfec8],
      0x0639: [0xfec9, 0xfeca, 0xfecb, 0xfecc],
      0x063a: [0xfecd, 0xfece, 0xfecf, 0xfed0],
      0x0641: [0xfed1, 0xfed2, 0xfed3, 0xfed4],
      0x0642: [0xfed5, 0xfed6, 0xfed7, 0xfed8],
      0x0643: [0xfed9, 0xfeda, 0xfedb, 0xfedc],
      0x0644: [0xfedd, 0xfede, 0xfedf, 0xfee0],
      0x0645: [0xfee1, 0xfee2, 0xfee3, 0xfee4],
      0x0646: [0xfee5, 0xfee6, 0xfee7, 0xfee8],
      0x0647: [0xfee9, 0xfeea, 0xfeeb, 0xfeec],
      0x0648: [0xfeed, 0xfeee, 0xfeed, 0xfeee],
      0x0649: [0xfeef, 0xfef0, 0xfeef, 0xfef0],
      0x064a: [0xfef1, 0xfef2, 0xfef3, 0xfef4],
      0x067e: [0xfb56, 0xfb57, 0xfb58, 0xfb59],
      0x0686: [0xfb7a, 0xfb7b, 0xfb7c, 0xfb7d],
      0x0698: [0xfb8a, 0xfb8b, 0xfb8a, 0xfb8b],
      0x06af: [0xfb92, 0xfb93, 0xfb94, 0xfb95]
    };

    const RIGHT_ONLY = new Set([
      0x0621, 0x0622, 0x0623, 0x0624, 0x0625, 0x0627, 0x062f, 0x0630, 0x0631, 0x0632, 0x0648, 0x0649, 0x0629, 0x0698
    ]);

    const LAM_ALEF = {
      0x0622: [0xfef5, 0xfef6],
      0x0623: [0xfef7, 0xfef8],
      0x0625: [0xfef9, 0xfefa],
      0x0627: [0xfefb, 0xfefc]
    };

    const clean = String(text).replace(/[\u064B-\u065F\u0670]/g, '');
    const codes = [];
    for (let i = 0; i < clean.length; i++) codes.push(clean.charCodeAt(i));

    const reshaped = [];
    for (let i = 0; i < codes.length; i++) {
      const c = codes[i];
      const prev = i > 0 ? codes[i - 1] : 0;
      const next = i < codes.length - 1 ? codes[i + 1] : 0;

      if (c === 0x0644 && LAM_ALEF[next]) {
        const connectsBefore = CHAR_MAP[prev] && !RIGHT_ONLY.has(prev);
        reshaped.push(String.fromCharCode(LAM_ALEF[next][connectsBefore ? 1 : 0]));
        i++;
        continue;
      }

      if (!CHAR_MAP[c]) {
        reshaped.push(String.fromCharCode(c));
        continue;
      }

      const connectsBefore = CHAR_MAP[prev] && !RIGHT_ONLY.has(prev);
      const connectsAfter = CHAR_MAP[next] && (CHAR_MAP[c] && !RIGHT_ONLY.has(c));

      let form = 0;
      if (connectsBefore && connectsAfter) form = 3;
      else if (connectsBefore) form = 1;
      else if (connectsAfter) form = 2;

      reshaped.push(String.fromCharCode(CHAR_MAP[c][form]));
    }

    const reshapedStr = reshaped.join('');
    const tokens = [];
    const tokenRegex = /([\u0600-\u06ff\ufb50-\ufdff\ufe70-\ufeff]+|[0-9]+|[^\u0600-\u06ff\ufb50-\ufdff\ufe70-\ufeff0-9\s]+|\s+)/g;
    let match;
    while ((match = tokenRegex.exec(reshapedStr)) !== null) {
      tokens.push(match[0]);
    }

    return tokens.reverse().map(tok => {
      if (/[\ufb50-\ufdff\ufe70-\ufeff]/.test(tok)) {
        return [...tok].reverse().join('');
      }
      return tok;
    }).join('');
  }

  function isolatedLabelText(value) {
    return shapeArabicText(value);
  }

  function getHeightReference(altitudeMode) {
    const mode = String(altitudeMode || 'ClampToGround').toLowerCase();
    if (mode === 'absolute') return Cesium.HeightReference.NONE;
    if (mode === 'relativetoground') return Cesium.HeightReference.RELATIVE_TO_GROUND;
    return Cesium.HeightReference.CLAMP_TO_GROUND;
  }

  function createFeatureSymbolCanvas(shape, color) {
    const canvas = document.createElement('canvas');
    canvas.width = 44;
    canvas.height = 44;
    const ctx = canvas.getContext('2d');
    let css = color?.toCssColorString ? color.toCssColorString() : '#22a8de';
    let kind = String(shape || 'circle').toLowerCase();

    // Check color override from pin color keys (e.g. pin-red, pin-blue)
    if (kind.startsWith('pin-')) {
      const colorPart = kind.substring(4);
      const colorMap = {
        'red': '#EF4444', 'blue': '#3B82F6', 'green': '#10B981', 'yellow': '#F59E0B',
        'purple': '#8B5CF6', 'orange': '#F97316', 'pink': '#EC4899', 'cyan': '#06B6D4', 'white': '#F9FAFB'
      };
      if (colorMap[colorPart]) css = colorMap[colorPart];
      kind = 'pin';
    }

    ctx.fillStyle = css;
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 2.5;
    ctx.lineJoin = 'round';
    ctx.beginPath();

    if (kind === 'square' || kind === 'shape-square') {
      ctx.rect(7, 7, 30, 30);
      ctx.fill();
      ctx.stroke();
    } else if (kind === 'triangle' || kind === 'shape-triangle') {
      ctx.moveTo(22, 6); ctx.lineTo(38, 38); ctx.lineTo(6, 38); ctx.closePath();
      ctx.fill();
      ctx.stroke();
    } else if (kind === 'diamond' || kind === 'shape-diamond') {
      ctx.moveTo(22, 5); ctx.lineTo(39, 22); ctx.lineTo(22, 39); ctx.lineTo(5, 22); ctx.closePath();
      ctx.fill();
      ctx.stroke();
    } else if (kind === 'star' || kind === 'shape-star') {
      for (let i = 0; i < 10; i++) {
        const radius = i % 2 === 0 ? 18 : 8;
        const angle = -Math.PI / 2 + i * Math.PI / 5;
        const x = 22 + Math.cos(angle) * radius;
        const y = 22 + Math.sin(angle) * radius;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
    } else if (kind === 'target' || kind === 'shape-target') {
      ctx.arc(22, 22, 18, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
      ctx.fillStyle = '#ffffff'; ctx.beginPath(); ctx.arc(22, 22, 11, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = css; ctx.beginPath(); ctx.arc(22, 22, 5, 0, Math.PI * 2); ctx.fill();
    } else if (kind === 'cross' || kind === 'shape-cross') {
      ctx.arc(22, 22, 17, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
      ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(22, 10); ctx.lineTo(22, 34); ctx.moveTo(10, 22); ctx.lineTo(34, 22); ctx.stroke();
    } else if (kind === 'flag' || kind === 'shape-flag') {
      ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(10, 40); ctx.lineTo(10, 8); ctx.stroke();
      ctx.fillStyle = css; ctx.beginPath(); ctx.moveTo(10, 8); ctx.lineTo(36, 16); ctx.lineTo(10, 24); ctx.closePath();
      ctx.fill(); ctx.stroke();
    } else if (kind === 'pin' || kind.startsWith('paddle')) {
      ctx.moveTo(22, 40);
      ctx.bezierCurveTo(18, 32, 8, 26, 8, 16);
      ctx.arc(22, 16, 14, Math.PI, 0, false);
      ctx.bezierCurveTo(36, 26, 26, 32, 22, 40);
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = '#ffffff';
      ctx.beginPath();
      ctx.arc(22, 16, 5, 0, Math.PI * 2);
      ctx.fill();
    } else if (kind.startsWith('num-') || kind.startsWith('letter-')) {
      const char = (kind.startsWith('num-') ? kind.substring(4) : kind.substring(7)).toUpperCase();
      ctx.arc(22, 22, 17, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 16px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(char, 22, 22);
    } else if (kind.startsWith('fac-')) {
      ctx.arc(22, 22, 17, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = '#ffffff';
      ctx.font = '16px "Segoe UI Emoji", sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      const iconChar = kind.includes('airport') ? '✈' :
                       kind.includes('hospital') ? '✚' :
                       kind.includes('school') ? '🏫' :
                       kind.includes('gas') ? '⛽' :
                       kind.includes('restaurant') ? '🍴' :
                       kind.includes('hotel') ? '🏨' :
                       kind.includes('bank') ? '🏦' : '🅿';
      ctx.fillText(iconChar, 22, 23);
    } else {
      ctx.arc(22, 22, 16, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
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

        const fillMode = String(p.fillMode || 'both').toLowerCase();
        const showFill = fillMode !== 'outlineonly' && fillMode !== 'outline';
        const showOutline = fillMode !== 'fillonly' && fillMode !== 'fill';
        const fillAlpha = Number.isFinite(Number(p.fillOpacity)) ? Math.max(0, Math.min(1, Number(p.fillOpacity))) : 0.4;
        let polyMaterial = colorVal.withAlpha(fillAlpha);
        if (p.fillColor && typeof p.fillColor === 'string' && p.fillColor.startsWith('#')) {
          polyMaterial = Cesium.Color.fromCssColorString(p.fillColor).withAlpha(fillAlpha);
        }

        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          polygon: {
            hierarchy: new Cesium.PolygonHierarchy(outerPositions, holes),
            fill: showFill,
            material: polyMaterial,
            outline: showOutline,
            outlineColor: colorVal,
            outlineWidth: Number(p.outlineWidth || p.lineWidth || 2),
            heightReference: getHeightReference(p.altitudeMode)
          }
        });
      } else if (p.type === 'path' && Array.isArray(p.coordinates) && p.coordinates.length >= 2) {
        const positions = p.coordinates.map(c => Cesium.Cartesian3.fromDegrees(c.lon, c.lat, c.alt || 0));
        let lineMaterial = colorVal;
        const linePat = String(p.linePattern || 'solid').toLowerCase();
        if (linePat === 'dashed') {
          lineMaterial = new Cesium.PolylineDashMaterialProperty({
            color: colorVal,
            dashLength: 16.0
          });
        } else if (linePat === 'dotted') {
          lineMaterial = new Cesium.PolylineDashMaterialProperty({
            color: colorVal,
            dashLength: 8.0,
            dashPattern: 255
          });
        } else if (linePat === 'dashdot') {
          lineMaterial = new Cesium.PolylineDashMaterialProperty({
            color: colorVal,
            dashLength: 24.0,
            dashPattern: 3855
          });
        }
        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          polyline: {
            positions: positions,
            width: Number(p.lineWidth || 3),
            material: lineMaterial,
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
      } else if (p.type === 'ellipse' && Number(p.semiMajorAxis) > 0) {
        const lon = Number(p.lon || 0);
        const lat = Number(p.lat || 0);
        const semiMajor = Math.max(0.1, Number(p.semiMajorAxis));
        const semiMinor = Math.max(0.1, Number(p.semiMinorAxis || semiMajor));
        const bearing = Number(p.bearingDegrees || 0);
        ent = viewer.entities.add({
          id: p.id,
          name: p.name,
          position: Cesium.Cartesian3.fromDegrees(lon, lat, Number(p.alt || 0)),
          ellipse: {
            semiMajorAxis: semiMajor,
            semiMinorAxis: semiMinor,
            rotation: Cesium.Math.toRadians(360 - bearing),
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
        ent._zearthFeatureInfo = { name: p.name, type: p.type, attributes: p.attributes || {} };
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
    snapIntersections: true,
    activeSnap: null,

    glyphEl: document.getElementById('snap-glyph'),
    markerEl: document.getElementById('snap-marker'),
    tooltipEl: document.getElementById('snap-tooltip'),
    tetherSvg: document.getElementById('snap-tether-svg'),
    tetherLine: document.getElementById('snap-tether-line'),

    setConfig: function (cfg) {
      if (!cfg) return;
      if (typeof cfg.enabled === 'boolean') this.enabled = cfg.enabled;
      if (typeof cfg.mode === 'string') this.mode = 'GroundDistance';
      if (typeof cfg.tolerancePixels === 'number' && cfg.tolerancePixels > 0) this.tolerancePixels = cfg.tolerancePixels;
      if (typeof cfg.groundToleranceMeters === 'number' && cfg.groundToleranceMeters > 0) this.groundToleranceMeters = cfg.groundToleranceMeters;
      if (typeof cfg.snapVertices === 'boolean') this.snapVertices = cfg.snapVertices;
      if (typeof cfg.snapMidpoints === 'boolean') this.snapMidpoints = cfg.snapMidpoints;
      if (typeof cfg.snapEdges === 'boolean') this.snapEdges = cfg.snapEdges;
      if (typeof cfg.snapIntersections === 'boolean') this.snapIntersections = cfg.snapIntersections;
    },

    showGlyph: function (screenX, screenY, type, label, mousePos) {
      if (!this.glyphEl) {
        this.glyphEl = document.getElementById('snap-glyph');
        this.tooltipEl = document.getElementById('snap-tooltip');
        this.tetherSvg = document.getElementById('snap-tether-svg');
        this.tetherLine = document.getElementById('snap-tether-line');
      }
      if (!this.glyphEl) return;
      this.glyphEl.style.left = `${screenX}px`;
      this.glyphEl.style.top = `${screenY}px`;
      this.glyphEl.className = `snap-glyph snap-${type}`;
      this.glyphEl.style.display = 'block';
      if (this.tooltipEl) {
        this.tooltipEl.textContent = label || `Snap: ${type}`;
      }
      if (this.tetherSvg && this.tetherLine && mousePos && Number.isFinite(mousePos.x) && Number.isFinite(mousePos.y)) {
        this.tetherLine.setAttribute('x1', String(mousePos.x));
        this.tetherLine.setAttribute('y1', String(mousePos.y));
        this.tetherLine.setAttribute('x2', String(screenX));
        this.tetherLine.setAttribute('y2', String(screenY));
        this.tetherSvg.style.display = 'block';
      } else if (this.tetherSvg) {
        this.tetherSvg.style.display = 'none';
      }
    },

    hideGlyph: function () {
      if (!this.glyphEl) {
        this.glyphEl = document.getElementById('snap-glyph');
        this.tetherSvg = document.getElementById('snap-tether-svg');
      }
      if (this.glyphEl) {
        this.glyphEl.style.display = 'none';
      }
      if (this.tetherSvg) {
        this.tetherSvg.style.display = 'none';
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

      const segmentIntersectionNearCursor = (a, b) => {
        // Project both segments into the cursor's local east/north tangent plane.
        // This avoids screen-space/perspective false intersections and is accurate
        // for the local drawing distances governed by groundToleranceMeters.
        const frame = Cesium.Transforms.eastNorthUpToFixedFrame(cursorCartesian);
        const inverse = Cesium.Matrix4.inverseTransformation(frame, new Cesium.Matrix4());
        const a1 = Cesium.Matrix4.multiplyByPoint(inverse, a.p1, new Cesium.Cartesian3());
        const a2 = Cesium.Matrix4.multiplyByPoint(inverse, a.p2, new Cesium.Cartesian3());
        const b1 = Cesium.Matrix4.multiplyByPoint(inverse, b.p1, new Cesium.Cartesian3());
        const b2 = Cesium.Matrix4.multiplyByPoint(inverse, b.p2, new Cesium.Cartesian3());
        const ax = a2.x - a1.x;
        const ay = a2.y - a1.y;
        const bx = b2.x - b1.x;
        const by = b2.y - b1.y;
        const denominator = ax * by - ay * bx;
        if (Math.abs(denominator) < 1e-9) return null;
        const cx = b1.x - a1.x;
        const cy = b1.y - a1.y;
        const ta = (cx * by - cy * bx) / denominator;
        const tb = (cx * ay - cy * ax) / denominator;
        if (ta <= 1e-7 || ta >= 1 - 1e-7 || tb <= 1e-7 || tb >= 1 - 1e-7) return null;
        return Cesium.Cartesian3.lerp(a.p1, a.p2, ta, new Cesium.Cartesian3());
      };

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
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label, screenPos);
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
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label, screenPos);
          return { snapped: true, ...bestTarget };
        }
      }

      // 3. Check true segment intersections in a local tangent plane. Shared
      // endpoints are excluded above because vertex snapping owns that case.
      if (this.snapIntersections && geom.segments.length > 1) {
        for (let i = 0; i < geom.segments.length - 1; i++) {
          for (let j = i + 1; j < geom.segments.length; j++) {
            const intersection = segmentIntersectionNearCursor(geom.segments[i], geom.segments[j]);
            if (!intersection) continue;
            const dist = groundDistance(cursorCartesian, intersection);
            if (dist <= tol && dist < minDistance) {
              const winPos = Cesium.SceneTransforms.worldToWindowCoordinates(viewer.scene, intersection);
              if (!winPos) continue;
              minDistance = dist;
              bestTarget = {
                type: 'intersection',
                label: `Intersection • ${dist.toFixed(2)} m`,
                cartesian: intersection,
                screenX: winPos.x,
                screenY: winPos.y,
                distance: dist
              };
            }
          }
        }

        if (bestTarget) {
          this.activeSnap = bestTarget;
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label, screenPos);
          return { snapped: true, ...bestTarget };
        }
      }

      // 4. Check Edge Nearest Projection (Priority 4)
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
          this.showGlyph(bestTarget.screenX, bestTarget.screenY, bestTarget.type, bestTarget.label, screenPos);
          return { snapped: true, ...bestTarget };
        }
      }

      this.hideGlyph();
      return { snapped: false, cartesian: null };
    }
  };

  // 4.6. Interactive Drawing Engine with Snapping Integration (ZE-040, ZE-041, ZE-045)
  

  // 4.6. Interactive Drawing Engine with Snapping Integration (ZE-040, ZE-041, ZE-045)
  const DrawingManager = {
    activeMode: null, // 'point' | 'path' | 'polygon' | 'rectangle' | 'circle' | 'ellipse'
    vertices: [],
    tempPoints: [],
    previewEntity: null,
    guideEntities: [],
    handler: null,
    style: { color: '#22A8DE', width: 3, opacity: 0.4 },

    pickGroundPosition: function (screenPos) {
      return pickGroundAtScreenPosition(screenPos);
    },

    getRectanglePositions: function (edge) {
      if (!edge || !this.vertices.length) return [];
      const start = Cesium.Cartographic.fromCartesian(this.vertices[0]);
      if (!start) return [];
      const lon1 = Cesium.Math.toDegrees(start.longitude);
      const lat1 = Cesium.Math.toDegrees(start.latitude);

      if (this.vertices.length >= 2) {
        const p2 = Cesium.Cartographic.fromCartesian(this.vertices[1]);
        const p3 = Cesium.Cartographic.fromCartesian(edge);
        if (p2 && p3) {
          const lon2 = Cesium.Math.toDegrees(p2.longitude);
          const lat2 = Cesium.Math.toDegrees(p2.latitude);
          const lon3 = Cesium.Math.toDegrees(p3.longitude);
          const lat3 = Cesium.Math.toDegrees(p3.latitude);
          const oriented = globalThis.ZEarthGeometry?.computeOrientedRectangle(lon1, lat1, lon2, lat2, lon3, lat3);
          if (oriented && oriented.corners) {
            return Cesium.Cartesian3.fromDegreesArray(oriented.corners);
          }
        }
      }

      const finish = Cesium.Cartographic.fromCartesian(edge);
      if (!finish) return [];
      const lon2 = Cesium.Math.toDegrees(finish.longitude);
      const lat2 = Cesium.Math.toDegrees(finish.latitude);
      const degrees = globalThis.ZEarthGeometry?.rectangleCorners(lon1, lat1, lon2, lat2);
      return degrees ? Cesium.Cartesian3.fromDegreesArray(degrees) : [];
    },

    getCircleRadius: function (edge) {
      if (!edge || this.vertices.length === 0) return 0.1;
      const center = Cesium.Cartographic.fromCartesian(this.vertices[0]);
      const target = Cesium.Cartographic.fromCartesian(edge);
      const geodesic = new Cesium.EllipsoidGeodesic(center, target, Cesium.Ellipsoid.WGS84);
      return Math.max(0.1, geodesic.surfaceDistance || 0.1);
    },

    getEllipseParameters: function (edgeOrMinor) {
      if (this.vertices.length < 2) return null;
      const cCenter = Cesium.Cartographic.fromCartesian(this.vertices[0]);
      const cMajor = Cesium.Cartographic.fromCartesian(this.vertices[1]);
      const geodesicMajor = new Cesium.EllipsoidGeodesic(cCenter, cMajor, Cesium.Ellipsoid.WGS84);
      const semiMajor = Math.max(0.1, geodesicMajor.surfaceDistance || 0.1);
      const bearing = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.calculateBearing(
        Cesium.Math.toDegrees(cCenter.longitude), Cesium.Math.toDegrees(cCenter.latitude),
        Cesium.Math.toDegrees(cMajor.longitude), Cesium.Math.toDegrees(cMajor.latitude)
      ) : 0.0;

      let semiMinor = 0.1;
      if (edgeOrMinor) {
        const cMinor = Cesium.Cartographic.fromCartesian(edgeOrMinor);
        const geodesicMinor = new Cesium.EllipsoidGeodesic(cCenter, cMinor, Cesium.Ellipsoid.WGS84);
        semiMinor = Math.max(0.1, Math.min(semiMajor, geodesicMinor.surfaceDistance || 0.1));
      }
      return { semiMajor, semiMinor, bearing };
    },

    start: function (type, styleOptions) {
      this.cancel();
      this.activeMode = type || 'point';
      this.vertices = [];
      this.tempPoints = [];
      this.guideEntities = [];
      this.style = Object.assign({ color: '#22A8DE', width: 3, opacity: 0.4 }, styleOptions);

      document.body.style.cursor = 'crosshair';

      this.handler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);

      let currentMousePos = null;

      // Left Click: Place vertex with snap support
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

        // Oriented 3-Point Rectangle finishes on 3 vertices (P1, P2 baseline, P3 width)
        if (this.activeMode === 'rectangle' && this.vertices.length === 3) {
          this.finish();
          return;
        }

        // Circle finishes on 2 vertices
        if (this.activeMode === 'circle' && this.vertices.length === 2) {
          this.finish();
          return;
        }

        // Ellipse finishes on 3 vertices
        if (this.activeMode === 'ellipse' && this.vertices.length === 3) {
          this.finish();
          return;
        }

        // Setup dynamic preview entities on first vertex
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
            // Live radius guide line
            const radiusGuide = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE')
                })
              }
            });
            this.guideEntities.push(radiusGuide);

            // Live circle polygon preview
            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return new Cesium.PolygonHierarchy([]);
                  const radius = this.getCircleRadius(currentMousePos);
                  const centerCarto = Cesium.Cartographic.fromCartesian(this.vertices[0]);
                  const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleCircle(
                    Cesium.Math.toDegrees(centerCarto.longitude),
                    Cesium.Math.toDegrees(centerCarto.latitude),
                    radius, 64
                  ) : [];
                  return new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(sampled));
                }, false),
                material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE').withAlpha(this.style.opacity || 0.4),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
                outlineWidth: 2,
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
          } else if (this.activeMode === 'ellipse') {
            // Major axis guide line
            const majorGuide = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE')
                })
              }
            });
            this.guideEntities.push(majorGuide);
          }
        } else if (this.vertices.length === 2 && this.activeMode === 'ellipse') {
          // Vertex 2 (major axis endpoint) placed. Remove major axis guide, add minor axis preview and ellipse polygon
          for (const ent of this.guideEntities) {
            viewer.entities.remove(ent);
          }
          this.guideEntities = [];

          const c0 = Cesium.Cartographic.fromCartesian(this.vertices[0]);
          const c1 = Cesium.Cartographic.fromCartesian(this.vertices[1]);
          const semiMajor = this.getCircleRadius(this.vertices[1]);
          const bearing = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.calculateBearing(
            Cesium.Math.toDegrees(c0.longitude), Cesium.Math.toDegrees(c0.latitude),
            Cesium.Math.toDegrees(c1.longitude), Cesium.Math.toDegrees(c1.latitude)
          ) : 0.0;

          // Minor axis guide line
          const minorGuide = viewer.entities.add({
            polyline: {
              positions: new Cesium.CallbackProperty(() => {
                if (!currentMousePos || this.vertices.length < 2) return [];
                return [this.vertices[0], currentMousePos];
              }, false),
              width: 2,
              material: new Cesium.PolylineDashMaterialProperty({
                color: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE')
              })
            }
          });
          this.guideEntities.push(minorGuide);

          this.previewEntity = viewer.entities.add({
            polygon: {
              hierarchy: new Cesium.CallbackProperty(() => {
                if (!currentMousePos || this.vertices.length < 2) return new Cesium.PolygonHierarchy([]);
                const semiMinor = Math.min(semiMajor, this.getCircleRadius(currentMousePos));
                const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleEllipse(
                  Cesium.Math.toDegrees(c0.longitude), Cesium.Math.toDegrees(c0.latitude),
                  semiMajor, semiMinor, bearing, 64
                ) : [];
                return new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(sampled));
              }, false),
              material: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE').withAlpha(this.style.opacity || 0.4),
              outline: true,
              outlineColor: Cesium.Color.fromCssColorString(this.style.color || '#22A8DE'),
              outlineWidth: 2,
              heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
            }
          });
        }
      }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

      // Mouse Move: Dynamic Rubber-band preview with live snap query
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

      if (this.activeMode === 'circle') {
        if (this.vertices.length < 2) {
          this.cancel();
          return;
        }
        const radiusMeters = this.getCircleRadius(this.vertices[1]);
        const centerCarto = Cesium.Cartographic.fromCartesian(this.vertices[0]);
        const centerLon = Cesium.Math.toDegrees(centerCarto.longitude);
        const centerLat = Cesium.Math.toDegrees(centerCarto.latitude);
        const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleCircle(
          centerLon, centerLat, radiusMeters, 64
        ) : [];
        const coords = [];
        for (let i = 0; i < sampled.length; i += 2) {
          coords.push({ longitude: sampled[i], latitude: sampled[i + 1], altitude: centerCarto.height });
        }
        sendHostMessage({
          event: 'draw.completed',
          geometryType: 'circle',
          shapeKind: 'circle',
          center: { longitude: centerLon, latitude: centerLat, altitude: centerCarto.height },
          radiusMeters: radiusMeters,
          coordinates: coords,
          color: this.style.color
        });
        this.cancel();
        return;
      }

      if (this.activeMode === 'ellipse') {
        if (this.vertices.length < 3) {
          this.cancel();
          return;
        }
        const params = this.getEllipseParameters(this.vertices[2]);
        const centerCarto = Cesium.Cartographic.fromCartesian(this.vertices[0]);
        const centerLon = Cesium.Math.toDegrees(centerCarto.longitude);
        const centerLat = Cesium.Math.toDegrees(centerCarto.latitude);
        const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleEllipse(
          centerLon, centerLat, params.semiMajor, params.semiMinor, params.bearing, 64
        ) : [];
        const coords = [];
        for (let i = 0; i < sampled.length; i += 2) {
          coords.push({ longitude: sampled[i], latitude: sampled[i + 1], altitude: centerCarto.height });
        }
        sendHostMessage({
          event: 'draw.completed',
          geometryType: 'ellipse',
          shapeKind: 'ellipse',
          center: { longitude: centerLon, latitude: centerLat, altitude: centerCarto.height },
          semiMajorAxis: params.semiMajor,
          semiMinorAxis: params.semiMinor,
          bearingDegrees: params.bearing,
          coordinates: coords,
          color: this.style.color
        });
        this.cancel();
        return;
      }

      const minVertices = this.activeMode === 'polygon' ? 3 : 2;
      if (this.vertices.length < minVertices) {
        this.cancel();
        return;
      }

      const finalVertices = this.activeMode === 'rectangle'
        ? (this.vertices.length >= 3 ? this.getRectanglePositions(this.vertices[2]) : (this.vertices.length === 2 ? this.getRectanglePositions(this.vertices[1]) : this.vertices))
        : this.vertices;
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

      sendHostMessage({
        event: 'draw.completed',
        geometryType: this.activeMode === 'rectangle' ? 'polygon' : this.activeMode,
        shapeKind: this.activeMode === 'rectangle' ? 'rectangle' : undefined,
        coordinates: coords,
        color: this.style.color
      });

      this.cancel();
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

      for (const ent of this.guideEntities) {
        viewer.entities.remove(ent);
      }
      this.guideEntities = [];

      for (const ent of this.tempPoints) {
        viewer.entities.remove(ent);
      }
      this.tempPoints = [];
    }
  };

  // 4.7. Geodesic Measurement Engine (ZE-046, ZE-047, AC-018, AC-019, AC-035)
  const MeasurementManager = {
    activeMode: null, // 'distance' | 'area' | 'circle' | 'ellipse' | 'rectangle' | 'coordinate'
    vertices: [],
    tempEntities: [],
    previewEntity: null,
    guideEntities: [],
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
        const dLat = carto2.latitude - carto1.latitude;
        const dLon = carto2.longitude - carto1.longitude;
        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
          Math.cos(carto1.latitude) * Math.cos(carto2.latitude) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
        return 6371007.2 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
      }
    },

    // Forward geodesic azimuth / bearing in degrees [0, 360)
    calculateBearing: function (c1, c2) {
      const carto1 = Cesium.Cartographic.fromCartesian(c1);
      const carto2 = Cesium.Cartographic.fromCartesian(c2);
      return globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.calculateBearing(
        Cesium.Math.toDegrees(carto1.longitude), Cesium.Math.toDegrees(carto1.latitude),
        Cesium.Math.toDegrees(carto2.longitude), Cesium.Math.toDegrees(carto2.latitude)
      ) : 0.0;
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
      if (!Number.isFinite(value)) return '0°00\'00.00"N';
      const absolute = Math.abs(value);
      let degrees = Math.floor(absolute);
      let minuteValue = (absolute - degrees) * 60;
      let minutes = Math.floor(minuteValue);
      let seconds = (minuteValue - minutes) * 60;
      if (seconds >= 59.995) {
        seconds = 0.0;
        minutes += 1;
        if (minutes >= 60) {
          minutes = 0;
          degrees += 1;
        }
      }
      const direction = isLatitude ? (value >= 0 ? 'N' : 'S') : (value >= 0 ? 'E' : 'W');
      return `${degrees}°${String(minutes).padStart(2, '0')}'${seconds.toFixed(2).padStart(5, '0')}"${direction}`;
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
        const alt = Number.isFinite(carto.height) ? `${carto.height.toFixed(1)} m` : '0.0 m';
        let segment = '—';
        if (previous) {
          const dist = this.formatDistance(this.calculateGeodesicDistance(previous, position));
          const bearing = this.calculateBearing(previous, position);
          segment = `${dist} (${bearing.toFixed(1)}°)`;
        }
        previous = position;
        return `<tr><td>${index + 1}</td><td>${lon.toFixed(6)}, ${lat.toFixed(6)}</td>` +
          `<td>${this.toDms(lon, false)} ${this.toDms(lat, true)}</td>` +
          `<td>${this.toUtm(lat, lon)}</td><td>${alt}</td><td>${segment}</td></tr>`;
      }).join('');
      return `<div style="max-height:160px;overflow:auto;margin-top:7px"><table class="measure-coord-table">` +
        `<thead><tr><th>#</th><th>DD (Lon, Lat)</th><th>DMS</th><th>UTM</th><th>Elev (Z)</th><th>Segment</th></tr></thead>` +
        `<tbody>${rows}</tbody></table></div>` +
        `<div class="measure-copy-row">` +
        `<button id="btn-copy-measure-coordinates" class="measure-copy-btn" title="Copy table as Tab-Separated Values for Excel/Sheets">Copy Table (TSV)</button>` +
        `<button id="btn-copy-measure-csv" class="measure-copy-btn" title="Copy coordinates as CSV">Copy CSV</button>` +
        `<button id="btn-copy-measure-summary" class="measure-copy-btn" title="Copy measurement summary text">Copy Summary</button>` +
        `</div>`;
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
      } else if (this.activeMode === 'circle') {
        this.titleEl.textContent = '⭕ Geodesic Circle Measurement';
        if (activeList.length < 2) {
          this.bodyEl.textContent = 'Click to place Center point, then move mouse to expand radius.';
        } else {
          const radius = this.calculateGeodesicDistance(activeList[0], activeList[1]);
          const diameter = 2 * radius;
          const circum = 2 * Math.PI * radius;
          const area = Math.PI * radius * radius;
          this.bodyEl.innerHTML = `
            <strong>Radius:</strong> ${this.formatDistance(radius)}<br/>
            <strong>Diameter:</strong> ${this.formatDistance(diameter)}<br/>
            <strong>Circumference:</strong> ${this.formatDistance(circum)}<br/>
            <strong>Geodesic Area:</strong> ${this.formatArea(area)}
          `;
          this.lastResult = {
            mode: 'circle',
            radiusMeters: radius,
            diameterMeters: diameter,
            circumferenceMeters: circum,
            areaSquareMeters: area,
            pointCount: activeList.length
          };
        }
        this.algEl.textContent = 'Algorithm: WGS84 Geodesic Radius & Authalic Area';
      } else if (this.activeMode === 'ellipse') {
        this.titleEl.textContent = '⬭ Geodesic Ellipse Measurement';
        if (activeList.length < 2) {
          this.bodyEl.textContent = 'Click 1: Center point. Click 2: Major axis endpoint. Click 3: Minor axis endpoint.';
        } else if (activeList.length === 2) {
          const a = this.calculateGeodesicDistance(activeList[0], activeList[1]);
          const bearing = this.calculateBearing(activeList[0], activeList[1]);
          this.bodyEl.innerHTML = `
            <strong>Semi-Major Axis (a):</strong> ${this.formatDistance(a)}<br/>
            <strong>Major Diameter (2a):</strong> ${this.formatDistance(2 * a)}<br/>
            <strong>Orientation / Bearing:</strong> ${bearing.toFixed(1)}°<br/>
            <small>Move mouse to expand minor axis, click to set.</small>
          `;
        } else {
          const a = this.calculateGeodesicDistance(activeList[0], activeList[1]);
          const b = Math.min(a, this.calculateGeodesicDistance(activeList[0], activeList[2]));
          const bearing = this.calculateBearing(activeList[0], activeList[1]);
          const perim = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.calculateEllipsePerimeter(a, b) : Math.PI * (a + b);
          const area = Math.PI * a * b;
          this.bodyEl.innerHTML = `
            <strong>Semi-Major (a):</strong> ${this.formatDistance(a)} | <strong>Major (2a):</strong> ${this.formatDistance(2 * a)}<br/>
            <strong>Semi-Minor (b):</strong> ${this.formatDistance(b)} | <strong>Minor (2b):</strong> ${this.formatDistance(2 * b)}<br/>
            <strong>Orientation:</strong> ${bearing.toFixed(1)}°<br/>
            <strong>Perimeter (Ramanujan):</strong> ${this.formatDistance(perim)}<br/>
            <strong>Geodesic Area:</strong> ${this.formatArea(area)}
          `;
          this.lastResult = {
            mode: 'ellipse',
            semiMajorAxis: a,
            semiMinorAxis: b,
            bearingDegrees: bearing,
            perimeterMeters: perim,
            areaSquareMeters: area,
            pointCount: activeList.length
          };
        }
        this.algEl.textContent = 'Algorithm: WGS84 Ramanujan Ellipse Perimeter & Geodesic Axes';
      } else if (this.activeMode === 'rectangle') {
        this.titleEl.textContent = '▭ 3-Point Oriented Rectangle Measurement';
        if (activeList.length === 1) {
          this.bodyEl.textContent = 'Click 1 placed (Baseline Start). Move cursor and click 2 to set Baseline Length & Azimuth.';
        } else if (activeList.length === 2) {
          const c1 = Cesium.Cartographic.fromCartesian(activeList[0]);
          const c2 = Cesium.Cartographic.fromCartesian(activeList[1]);
          const baseLen = this.calculateGeodesicDistance(activeList[0], activeList[1]);
          const azimuth = this.calculateBearing(activeList[0], activeList[1]);
          this.bodyEl.innerHTML = `
            <strong>Baseline Length:</strong> ${this.formatDistance(baseLen)}<br/>
            <strong>Azimuth:</strong> ${azimuth.toFixed(2)}°<br/>
            <small>Move cursor and click 3 to set perpendicular width.</small>
          `;
        } else if (activeList.length >= 3) {
          const c1 = Cesium.Cartographic.fromCartesian(activeList[0]);
          const c2 = Cesium.Cartographic.fromCartesian(activeList[1]);
          const c3 = Cesium.Cartographic.fromCartesian(activeList[2]);
          const oriented = globalThis.ZEarthGeometry?.computeOrientedRectangle(
            Cesium.Math.toDegrees(c1.longitude), Cesium.Math.toDegrees(c1.latitude),
            Cesium.Math.toDegrees(c2.longitude), Cesium.Math.toDegrees(c2.latitude),
            Cesium.Math.toDegrees(c3.longitude), Cesium.Math.toDegrees(c3.latitude)
          );
          if (oriented) {
            this.bodyEl.innerHTML = `
              <strong>Length (Baseline):</strong> ${this.formatDistance(oriented.length)}<br/>
              <strong>Width (Perpendicular):</strong> ${this.formatDistance(oriented.width)}<br/>
              <strong>Azimuth:</strong> ${oriented.bearing.toFixed(2)}°<br/>
              <strong>Perimeter:</strong> ${this.formatDistance(oriented.perimeter)}<br/>
              <strong>Area:</strong> ${this.formatArea(oriented.area)}
            `;
            this.lastResult = {
              mode: 'rectangle',
              lengthMeters: oriented.length,
              widthMeters: oriented.width,
              bearingDegrees: oriented.bearing,
              perimeterMeters: oriented.perimeter,
              areaSquareMeters: oriented.area,
              corners: oriented.corners,
              pointCount: activeList.length
            };
          }
        }
        this.algEl.textContent = 'Algorithm: 3-Point Oriented Geodesic Rectangle (Baseline + Perpendicular Offset)';
      } else if (this.activeMode === 'coordinate') {
        this.titleEl.textContent = '📍 Continuous Coordinate Inspection';
        this.bodyEl.innerHTML = `
          <strong>Pinned Points:</strong> ${activeList.length}<br/>
          <small>Click to drop numbered inspection pins. Each point displays DD, DMS, UTM, Elevation, and Segment Azimuth.</small>
        `;
        this.lastResult = {
          mode: 'coordinate',
          pointCount: activeList.length
        };
        this.algEl.textContent = 'Algorithm: WGS84 High-Precision Geodetic & UTM Projections';
      }

      if (activeList.length > 0 && this.bodyEl) {
        this.bodyEl.insertAdjacentHTML('beforeend', this.buildVertexTable(activeList));

        const safeCopyText = (textToCopy) => {
          if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(textToCopy).catch(() => {
              try {
                const ta = document.createElement('textarea');
                ta.value = textToCopy;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
              } catch (_) {}
            });
          } else {
            try {
              const ta = document.createElement('textarea');
              ta.value = textToCopy;
              ta.style.position = 'fixed';
              ta.style.opacity = '0';
              document.body.appendChild(ta);
              ta.select();
              document.execCommand('copy');
              document.body.removeChild(ta);
            } catch (_) {}
          }
        };

        // Copy Table TSV handler
        this.bodyEl.querySelector('#btn-copy-measure-coordinates')?.addEventListener('click', (e) => {
          let prev = null;
          const lines = activeList.map((position, index) => {
            const c = Cesium.Cartographic.fromCartesian(position);
            const lat = Cesium.Math.toDegrees(c.latitude);
            const lon = Cesium.Math.toDegrees(c.longitude);
            const alt = Number.isFinite(c.height) ? c.height.toFixed(2) : '0.00';
            const dms = `${this.toDms(lon, false)} ${this.toDms(lat, true)}`;
            const utm = this.toUtm(lat, lon);
            let seg = '—';
            if (prev) {
              const d = this.calculateGeodesicDistance(prev, position).toFixed(2);
              const b = this.calculateBearing(prev, position).toFixed(1);
              seg = `${d} m (${b}°)`;
            }
            prev = position;
            return `${index + 1}	${lon.toFixed(8)}	${lat.toFixed(8)}	${dms}	${utm}	${alt}	${seg}`;
          });
          const header = 'Point	Longitude	Latitude	DMS	UTM	Altitude(m)	Segment';
          safeCopyText([header, ...lines].join(String.fromCharCode(10)));
          const btn = e.target;
          btn.textContent = 'Copied TSV!';
          btn.classList.add('copied');
          setTimeout(() => { btn.textContent = 'Copy Table (TSV)'; btn.classList.remove('copied'); }, 1800);
        }, { once: true });

        // Copy CSV handler
        this.bodyEl.querySelector('#btn-copy-measure-csv')?.addEventListener('click', (e) => {
          const lines = activeList.map((position, index) => {
            const c = Cesium.Cartographic.fromCartesian(position);
            const lat = Cesium.Math.toDegrees(c.latitude);
            const lon = Cesium.Math.toDegrees(c.longitude);
            const alt = Number.isFinite(c.height) ? c.height.toFixed(2) : '0.00';
            return `${index + 1},${lon.toFixed(8)},${lat.toFixed(8)},${alt}`;
          });
          safeCopyText(['Point,Longitude,Latitude,Altitude', ...lines].join(String.fromCharCode(10)));
          const btn = e.target;
          btn.textContent = 'Copied CSV!';
          btn.classList.add('copied');
          setTimeout(() => { btn.textContent = 'Copy CSV'; btn.classList.remove('copied'); }, 1800);
        }, { once: true });

        // Copy Summary handler
        this.bodyEl.querySelector('#btn-copy-measure-summary')?.addEventListener('click', (e) => {
          const textSummary = this.bodyEl.innerText || '';
          safeCopyText(textSummary.trim());
          const btn = e.target;
          btn.textContent = 'Copied Summary!';
          btn.classList.add('copied');
          setTimeout(() => { btn.textContent = 'Copy Summary'; btn.classList.remove('copied'); }, 1800);
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
      this.guideEntities = [];
      document.body.style.cursor = 'crosshair';

      this.updateHUD();

      this.handler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);

      let currentMousePos = null;

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

        // Dynamic rubber-band previews
        if (this.activeMode === 'circle') {
          if (this.vertices.length === 1) {
            // Live radius guide line
            const radiusLine = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString('#38bdf8')
                })
              }
            });
            this.guideEntities.push(radiusLine);

            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return new Cesium.PolygonHierarchy([]);
                  const r = this.calculateGeodesicDistance(this.vertices[0], currentMousePos);
                  const c = Cesium.Cartographic.fromCartesian(this.vertices[0]);
                  const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleCircle(
                    Cesium.Math.toDegrees(c.longitude), Cesium.Math.toDegrees(c.latitude), r, 64
                  ) : [];
                  return new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(sampled));
                }, false),
                material: Cesium.Color.fromCssColorString('#38bdf8').withAlpha(0.35),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString('#38bdf8'),
                outlineWidth: 2,
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
            this.tempEntities.push(this.previewEntity);
          } else if (this.vertices.length === 2) {
            this.finish();
            return;
          }
        } else if (this.activeMode === 'ellipse') {
          if (this.vertices.length === 1) {
            const majorLine = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString('#f59e0b')
                })
              }
            });
            this.guideEntities.push(majorLine);
          } else if (this.vertices.length === 2) {
            for (const ent of this.guideEntities) viewer.entities.remove(ent);
            this.guideEntities = [];

            const c0 = Cesium.Cartographic.fromCartesian(this.vertices[0]);
            const c1 = Cesium.Cartographic.fromCartesian(this.vertices[1]);
            const semiMajor = this.calculateGeodesicDistance(this.vertices[0], this.vertices[1]);
            const bearing = this.calculateBearing(this.vertices[0], this.vertices[1]);

            const minorLine = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length < 2) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString('#f59e0b')
                })
              }
            });
            this.guideEntities.push(minorLine);

            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length < 2) return new Cesium.PolygonHierarchy([]);
                  const semiMinor = Math.min(semiMajor, this.calculateGeodesicDistance(this.vertices[0], currentMousePos));
                  const sampled = globalThis.ZEarthGeometry ? globalThis.ZEarthGeometry.sampleEllipse(
                    Cesium.Math.toDegrees(c0.longitude), Cesium.Math.toDegrees(c0.latitude),
                    semiMajor, semiMinor, bearing, 64
                  ) : [];
                  return new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(sampled));
                }, false),
                material: Cesium.Color.fromCssColorString('#f59e0b').withAlpha(0.35),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString('#f59e0b'),
                outlineWidth: 2,
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
            this.tempEntities.push(this.previewEntity);
          } else if (this.vertices.length === 3) {
            this.finish();
            return;
          }
        } else if (this.activeMode === 'rectangle') {
          if (this.vertices.length === 1) {
            const baselineGuide = viewer.entities.add({
              polyline: {
                positions: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length === 0) return [];
                  return [this.vertices[0], currentMousePos];
                }, false),
                width: 2.5,
                material: new Cesium.PolylineDashMaterialProperty({
                  color: Cesium.Color.fromCssColorString('#FACC15')
                })
              }
            });
            this.guideEntities.push(baselineGuide);
          } else if (this.vertices.length === 2) {
            for (const ent of this.guideEntities) viewer.entities.remove(ent);
            this.guideEntities = [];

            this.previewEntity = viewer.entities.add({
              polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                  if (!currentMousePos || this.vertices.length < 2) return new Cesium.PolygonHierarchy([]);
                  const corners = DrawingManager.getRectanglePositions.call(this, currentMousePos);
                  return new Cesium.PolygonHierarchy(corners);
                }, false),
                material: Cesium.Color.fromCssColorString('#22D3EE').withAlpha(0.28),
                outline: true,
                outlineColor: Cesium.Color.fromCssColorString('#FACC15'),
                outlineWidth: 2.5,
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
              }
            });
            this.tempEntities.push(this.previewEntity);
          } else if (this.vertices.length === 3) {
            this.finish();
            return;
          }
        } else if (this.activeMode === 'distance') {
          if (this.vertices.length === 1) {
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
          }
        } else if (this.activeMode === 'area') {
          if (this.vertices.length === 1) {
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

      for (const ent of this.guideEntities) {
        viewer.entities.remove(ent);
      }
      this.guideEntities = [];

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

      for (const ent of this.guideEntities) {
        viewer.entities.remove(ent);
      }
      this.guideEntities = [];

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

// Context Menu on 3D Globe entities
  const featureContextMenu = document.createElement('div');
  featureContextMenu.className = 'feature-context-menu';
  featureContextMenu.hidden = true;
  featureContextMenu.style.position = 'absolute';
  featureContextMenu.style.zIndex = '9999';
  featureContextMenu.style.background = '#22252a';
  featureContextMenu.style.border = '1px solid #3c4048';
  featureContextMenu.style.borderRadius = '6px';
  featureContextMenu.style.padding = '4px 0';
  featureContextMenu.style.boxShadow = '0 8px 24px rgba(0,0,0,0.5)';
  featureContextMenu.style.fontFamily = 'Segoe UI, sans-serif';
  featureContextMenu.style.fontSize = '12px';
  featureContextMenu.style.minWidth = '180px';
  document.getElementById('viewport-container').appendChild(featureContextMenu);

  document.addEventListener('pointerdown', (e) => {
    if (!featureContextMenu.contains(e.target)) {
      featureContextMenu.hidden = true;
    }
  });

  const ProfileHoverCursor = {
    entity: null,
    show(lon, lat) {
      if (!Number.isFinite(lon) || !Number.isFinite(lat)) return;
      const position = Cesium.Cartesian3.fromDegrees(lon, lat);
      if (!this.entity) {
        this.entity = viewer.entities.add({
          id: '__profile_hover_cursor__',
          position: position,
          point: {
            pixelSize: 12,
            color: Cesium.Color.fromCssColorString('#00C8FF'),
            outlineColor: Cesium.Color.WHITE,
            outlineWidth: 2,
            disableDepthTestDistance: Number.POSITIVE_INFINITY
          }
        });
      } else {
        this.entity.position = position;
        this.entity.show = true;
      }
      viewer.scene.requestRender();
    },
    clear() {
      if (this.entity) {
        this.entity.show = false;
        viewer.scene.requestRender();
      }
    }
  };

  const featureRightClickHandler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);
  featureRightClickHandler.setInputAction(movement => {
    if (DrawingManager.activeMode || MeasurementManager.activeMode) return;
    const picked = viewer.scene.pick(movement.position);
    const pickedEnt = picked?.id;
    let info = pickedEnt?._zearthFeatureInfo;
    if (!info && pickedEnt) {
      if (typeof pickedEnt.id === 'string' && pickedEnt.id.length > 0 && !pickedEnt.id.startsWith('__')) {
        info = { id: pickedEnt.id, name: pickedEnt.name || 'Feature', type: pickedEnt.polyline ? 'path' : (pickedEnt.polygon ? 'polygon' : 'point') };
      }
    }
    if (!info) {
      let clickLon = null, clickLat = null;
      try {
        const groundPos = DrawingManager.pickGroundPosition(movement.position) || getScreenCenterGround();
        if (groundPos) {
          const carto = Cesium.Cartographic.fromCartesian(groundPos);
          clickLon = Cesium.Math.toDegrees(carto.longitude);
          clickLat = Cesium.Math.toDegrees(carto.latitude);
        }
      } catch {}

      featureContextMenu.replaceChildren();
      const isAr = EngineLocale.isArabic;
      const emptyItems = isAr ? [
        { label: '★ حفظ المشهد الحالي', cmd: 'places.addCurrentScene' },
        { label: '📍 إضافة علامة موضع هنا', cmd: `places.addPlacemarkAt:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` },
        { type: 'separator' },
        { label: '📍 الاتجاهات من هنا', cmd: `places.directionsFromCoords:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` },
        { label: '🏁 الاتجاهات إلى هنا', cmd: `places.directionsToCoords:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` }
      ] : [
        { label: '★ Add View to Saved Scenes', cmd: 'places.addCurrentScene' },
        { label: '📍 Add Placemark Here', cmd: `places.addPlacemarkAt:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` },
        { type: 'separator' },
        { label: '📍 Directions from here', cmd: `places.directionsFromCoords:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` },
        { label: '🏁 Directions to here', cmd: `places.directionsToCoords:${clickLon != null ? clickLon.toFixed(6) : '0'}:${clickLat != null ? clickLat.toFixed(6) : '0'}` }
      ];

      for (const item of emptyItems) {
        if (item.type === 'separator') {
          const sep = document.createElement('div');
          sep.style.height = '1px';
          sep.style.background = '#3c4048';
          sep.style.margin = '4px 0';
          featureContextMenu.appendChild(sep);
          continue;
        }
        const btn = document.createElement('div');
        btn.textContent = item.label;
        btn.style.padding = '6px 14px';
        btn.style.color = '#e2e8f0';
        btn.style.cursor = 'pointer';
        btn.style.fontSize = '12px';
        btn.style.transition = 'background 0.15s ease';
        btn.addEventListener('mouseenter', () => { btn.style.background = '#333842'; });
        btn.addEventListener('mouseleave', () => { btn.style.background = 'transparent'; });
        btn.addEventListener('click', () => {
          featureContextMenu.hidden = true;
          sendHostMessage({ command: item.cmd });
        });
        featureContextMenu.appendChild(btn);
      }

      featureContextMenu.hidden = false;
      const width = 230, height = 180;
      featureContextMenu.style.left = `${Math.max(8, Math.min(viewer.canvas.clientWidth - width - 8, movement.position.x + 4))}px`;
      featureContextMenu.style.top = `${Math.max(8, Math.min(viewer.canvas.clientHeight - height - 8, movement.position.y + 4))}px`;
      return;
    }

    featureContextMenu.replaceChildren();

    // Select this item in the host
    sendHostMessage({ command: 'places.select:' + (info.id || '') });

    const isAr = EngineLocale.isArabic;
    const menuItems = isAr ? [
      { label: '⚙ الخصائص...', cmd: 'places.openProperties:' + info.id },
      { label: '📈 الملف التضاريسي', cmd: 'places.elevationProfile:' + info.id },
      { type: 'separator' },
      { label: '✂ قص', cmd: 'places.cut:' + info.id },
      { label: '📋 نسخ', cmd: 'places.copy:' + info.id },
      { label: '🗑 حذف', cmd: 'places.delete:' + info.id },
      { type: 'separator' },
      { label: '📍 الاتجاهات من هنا', cmd: 'places.directionsFrom:' + info.id },
      { label: '🏁 الاتجاهات إلى هنا', cmd: 'places.directionsTo:' + info.id },
      { label: '📸 لقطة منظر', cmd: 'places.snapshotView:' + info.id }
    ] : [
      { label: '⚙ Properties...', cmd: 'places.openProperties:' + info.id },
      { label: '📈 Show Elevation Profile', cmd: 'places.elevationProfile:' + info.id },
      { type: 'separator' },
      { label: '✂ Cut', cmd: 'places.cut:' + info.id },
      { label: '📋 Copy', cmd: 'places.copy:' + info.id },
      { label: '🗑 Delete', cmd: 'places.delete:' + info.id },
      { type: 'separator' },
      { label: '📍 Directions from here', cmd: 'places.directionsFrom:' + info.id },
      { label: '🏁 Directions to here', cmd: 'places.directionsTo:' + info.id },
      { label: '📸 Snapshot View', cmd: 'places.snapshotView:' + info.id }
    ];

    for (const item of menuItems) {
      if (item.type === 'separator') {
        const sep = document.createElement('div');
        sep.style.height = '1px';
        sep.style.background = '#3c4048';
        sep.style.margin = '4px 0';
        featureContextMenu.appendChild(sep);
        continue;
      }
      const btn = document.createElement('div');
      btn.textContent = item.label;
      btn.style.padding = '6px 14px';
      btn.style.color = '#e2e8f0';
      btn.style.cursor = 'pointer';
      btn.style.transition = 'background 0.15s ease';
      btn.addEventListener('mouseenter', () => { btn.style.background = '#333842'; });
      btn.addEventListener('mouseleave', () => { btn.style.background = 'transparent'; });
      btn.addEventListener('click', () => {
        featureContextMenu.hidden = true;
        sendHostMessage({ command: item.cmd });
      });
      featureContextMenu.appendChild(btn);
    }

    featureContextMenu.hidden = false;
    const width = 190, height = 210;
    featureContextMenu.style.left = `${Math.max(8, Math.min(viewer.canvas.clientWidth - width - 8, movement.position.x + 4))}px`;
    featureContextMenu.style.top = `${Math.max(8, Math.min(viewer.canvas.clientHeight - height - 8, movement.position.y + 4))}px`;
  }, Cesium.ScreenSpaceEventType.RIGHT_CLICK);

const featureInfoPanel = document.createElement('section');
  featureInfoPanel.className = 'feature-info-panel';
  featureInfoPanel.hidden = true;
  featureInfoPanel.setAttribute('role', 'region');
  featureInfoPanel.setAttribute('aria-label', 'Feature attributes');
  document.getElementById('viewport-container').appendChild(featureInfoPanel);
  const featureClickHandler = new Cesium.ScreenSpaceEventHandler(viewer.canvas);
  featureClickHandler.setInputAction(movement => {
    if (DrawingManager.activeMode || MeasurementManager.activeMode) return;
    const picked = viewer.scene.pick(movement.position);
    const info = picked?.id?._zearthFeatureInfo;
    if (!info) { featureInfoPanel.hidden = true; return; }
    featureInfoPanel.replaceChildren();
    const heading = document.createElement('div');
    heading.className = 'feature-info-heading';
    const title = document.createElement('strong');
    title.textContent = String(info.name || 'Feature');
    title.dir = 'auto';
    const close = document.createElement('button');
    close.type = 'button'; close.textContent = '×'; close.setAttribute('aria-label', 'Close feature attributes');
    close.addEventListener('click', () => { featureInfoPanel.hidden = true; });
    heading.append(title, close);
    featureInfoPanel.appendChild(heading);
    const table = document.createElement('table');
    const body = document.createElement('tbody');
    const rows = [['Type', info.type || 'Feature'], ...Object.entries(info.attributes || {})];
    for (const [key, value] of rows) {
      const tr = document.createElement('tr');
      const th = document.createElement('th'); th.scope = 'row'; th.textContent = String(key); th.dir = 'auto';
      const td = document.createElement('td'); td.textContent = String(value ?? ''); td.dir = 'auto';
      tr.append(th, td); body.appendChild(tr);
    }
    table.appendChild(body); featureInfoPanel.appendChild(table);
    featureInfoPanel.hidden = false;
    const width = featureInfoPanel.offsetWidth, height = featureInfoPanel.offsetHeight;
    featureInfoPanel.style.left = `${Math.max(8, Math.min(viewer.canvas.clientWidth - width - 8, movement.position.x + 14))}px`;
    featureInfoPanel.style.top = `${Math.max(8, Math.min(viewer.canvas.clientHeight - height - 8, movement.position.y + 14))}px`;
  }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

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
  const GRID_DEFAULT_COLORS = {
    gridMajorColor: '#FF5252',
    gridLongitudeColor: '#3BA9FF',
    gridLatitudeColor: '#3AD17B',
    gridLabelColor: '#E8E8E8',
    gridUtmColor: '#FFC107'
  };

  const GraticuleManager = {
    dataSource: null,
    lastViewKey: '',
    config: {
      visible: false,
      // Legacy field kept for bridge compatibility. Labels are now generated
      // automatically for every enabled component.
      showLabels: true,
      showKeyParallels: true,
      showUtmZones: false,
      showLongitudeLines: true,
      showLatitudeLines: true,
      longitudeIntervalDegrees: 0,
      latitudeIntervalDegrees: 0,
      gridLabelFormat: "DD",
      utmLabelMode: "GridZone",
      gridLabelSize: "Large",
      utmLabelPlacement: "Center",
      ...GRID_DEFAULT_COLORS
    },

    init() {
      this.dataSource = new Cesium.CustomDataSource('geographic-graticules');
      this.dataSource.show = false;
      viewer.dataSources.add(this.dataSource);
      this.buildGraticules();
      // Calculate a single geographic view centre per rendered frame. Only label
      // positions change during motion; line geometry is rebuilt at moveEnd.
      viewer.scene.preRender.addEventListener(() => {
        if (this.config.visible) {
          this.updateLabelViewCenter();
          this.refreshWhileMoving();
        }
      });
      // A WebView resize changes label collision and edge clipping even when the
      // camera itself does not move. Rebuild once after layout settles so labels
      // do not remain partially hidden until the next pan/zoom.
      if (typeof ResizeObserver !== 'undefined') {
        this.resizeObserver = new ResizeObserver(() => {
          if (!this.config.visible || this.resizeRefreshPending) return;
          this.resizeRefreshPending = true;
          window.requestAnimationFrame(() => {
            this.resizeRefreshPending = false;
            this.lastViewKey = '';
            this.refreshForCamera();
          });
        });
        this.resizeObserver.observe(viewer.canvas);
      }
    },

    labelSizePixels() {
      switch (this.config.gridLabelSize) {
        case 'Small': return 11;
        case 'Medium': return 13;
        case 'ExtraLarge': return 19;
        default: return 15;
      }
    },

    cssColor(value, fallback) {
      const fallbackColor = Cesium.Color.fromCssColorString(fallback);
      if (typeof value !== 'string' || !value.trim()) return fallbackColor;
      try {
        return Cesium.Color.fromCssColorString(value.trim()) || fallbackColor;
      } catch (error) {
        return fallbackColor;
      }
    },

    lineColor(value, fallback, alpha) {
      return this.cssColor(value, fallback).withAlpha(alpha);
    },

    addLabel(text, longitude, latitude, options) {
      if (!text) return;
      const settings = options || {};
      const anchor = Cesium.Cartesian3.fromDegrees(longitude, latitude);
      const originalCenter = this.labelViewCenter ? { ...this.labelViewCenter } : null;
      const moving = settings.axis && originalCenter;
      const positionForView = () => {
        if (!moving || !this.labelViewCenter) return anchor;
        const coords = window.ZEarthGraticuleGeometry.movingLabelCoordinates(
          settings.axis,
          settings.axis === 'parallel' ? latitude : longitude,
          settings.axis === 'parallel' ? longitude : latitude,
          originalCenter, this.labelViewCenter);
        return Cesium.Cartesian3.fromDegrees(coords.longitude, coords.latitude);
      };
      const visibleFromCamera = new Cesium.CallbackProperty(() => {
        const current = positionForView();
        const normal = Cesium.Ellipsoid.WGS84.geodeticSurfaceNormal(current, new Cesium.Cartesian3());
        return this.surfaceVisible(current, normal);
      }, false);
      const pixels = settings.pixels || this.labelSizePixels();

      // A label is text around a point. Cesium keeps drawing a label whose
      // anchor sits just outside the canvas, which produced half-words such as
      // "ZD 28Q" clipped by the viewport edge. Anchors outside the canvas are
      // skipped, and anchors near an edge grow away from it so the full text
      // stays readable and attached to the same geographic point.
      const screen = this.projectToScreen(anchor);
      const canvas = viewer.canvas;
      const canvasWidth = canvas ? canvas.clientWidth : 0;
      const canvasHeight = canvas ? canvas.clientHeight : 0;
      if (screen && canvasWidth && canvasHeight &&
          (screen.x < 0 || screen.x > canvasWidth || screen.y < 0 || screen.y > canvasHeight)) {
        return;
      }
      let horizontalOrigin = settings.horizontalOrigin;
      if (horizontalOrigin === undefined || horizontalOrigin === null) {
        if (screen && canvasWidth && screen.x < canvasWidth * 0.45) {
          horizontalOrigin = Cesium.HorizontalOrigin.LEFT;
        } else if (screen && canvasWidth && screen.x > canvasWidth * 0.88) {
          horizontalOrigin = Cesium.HorizontalOrigin.RIGHT;
        } else {
          horizontalOrigin = Cesium.HorizontalOrigin.CENTER;
        }
      }
      const offsetX = Number.isFinite(settings.offsetX)
        ? settings.offsetX
        : (horizontalOrigin === Cesium.HorizontalOrigin.LEFT ? 8
          : horizontalOrigin === Cesium.HorizontalOrigin.RIGHT ? -8 : 0);
      const offsetY = Number.isFinite(settings.offsetY) ? settings.offsetY : -14;
      // In 2D the map is a flat plane: the grid lines and the label anchors are
      // coplanar at height 0, so the line primitives won the depth test and the
      // text rendered dim grey on top of its own line. Depth testing is still
      // required in 3D so a label can never be drawn through the globe.
      const depthTestDisabled = viewer.scene.mode === Cesium.SceneMode.SCENE3D
        ? 0
        : Number.POSITIVE_INFINITY;
      // Cesium draws every label independently, so two labels can land on the
      // same pixels (a Tropic of Cancer label on top of a UTM zone label, for
      // example). Labels are laid out in priority order — major references
      // first, regular grid labels next, UTM labels last — and any label whose
      // box overlaps an accepted one is dropped for this frame.
      if (!this.reserveLabelSpace(text, screen, pixels, horizontalOrigin, offsetX, offsetY)) {
        return;
      }
      this.dataSource.entities.add({
        position: moving ? new Cesium.CallbackPositionProperty(positionForView, false) : anchor,
        label: {
          show: visibleFromCamera,
          text: text,
          font: (settings.bold === false ? '' : 'bold ') + pixels + "px 'Segoe UI', sans-serif",
          fillColor: settings.color || this.cssColor(this.config.gridLabelColor, '#FFFFFF'),
          outlineColor: Cesium.Color.fromCssColorString('#000000'),
          outlineWidth: Math.max(3, Math.round(pixels / 4)),
          style: Cesium.LabelStyle.FILL_AND_OUTLINE,
          showBackground: false,
          heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
          // Depth testing stays enabled in 3D so a label cannot be drawn
          // through Earth; in 2D it is disabled because everything is coplanar.
          disableDepthTestDistance: depthTestDisabled,
          pixelOffset: new Cesium.Cartesian2(offsetX, offsetY),
          horizontalOrigin: horizontalOrigin
        }
      });
    },

    buildGraticules() {
      this.dataSource.entities.removeAll();
      this.resetLabelLayout();
      // An explicit data-source visibility gate also hides pending primitives.
      this.dataSource.show = this.config.visible;
      if (!this.config.visible) {
        this.lastViewKey = '';
        viewer.scene.requestRender();
        return;
      }

      const viewRect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      const full = { west: -180, east: 180, south: -90, north: 90 };
      let bounds = full;
      if (viewRect) {
        bounds = {
          west: Cesium.Math.toDegrees(viewRect.west),
          east: Cesium.Math.toDegrees(viewRect.east),
          south: Math.max(-90, Cesium.Math.toDegrees(viewRect.south)),
          north: Math.min(90, Cesium.Math.toDegrees(viewRect.north))
        };
      }

      const longitudeWindows = window.ZEarthGraticuleGeometry.longitudeWindows(bounds);
      const lineBounds = window.ZEarthGraticuleGeometry.expandViewBounds(bounds);
      const lineLongitudeWindows = window.ZEarthGraticuleGeometry.longitudeWindows(lineBounds);
      this.lineCoverage = lineBounds;
      const lonSpan = Math.max(0.000001, longitudeWindows.reduce((sum, w) => sum + (w.east - w.west), 0));
      const latSpan = Math.max(0.000001, bounds.north - bounds.south);
      // Previous descending candidates chose 60° almost everywhere.
      const candidates = [0.000001, 0.000002, 0.000005, 0.00001, 0.00002, 0.00005,
        0.0001, 0.0002, 0.0005, 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60];
      const baseLon = this.config.longitudeIntervalDegrees > 0
        ? Math.max(0.05, this.config.longitudeIntervalDegrees)
        : (candidates.find(v => lonSpan / v <= 10) || 60);
      const baseLat = this.config.latitudeIntervalDegrees > 0
        ? Math.max(0.05, this.config.latitudeIntervalDegrees)
        : (candidates.find(v => latSpan / v <= 10) || 60);
      // Thin — never rescale — the user's interval when the viewport cannot
      // resolve it. Only integer multiples of the requested interval are used,
      // so a 5° setting still produces 5°/10°/20° lines and never 7.3°.
      const canvasWidth = Math.max(320, viewer.canvas?.clientWidth || 1200);
      const canvasHeight = Math.max(240, viewer.canvas?.clientHeight || 800);
      const thin = (span, base, pixels, minPixels) => {
        const minDegrees = (span / pixels) * minPixels;
        return base * Math.max(1, Math.ceil(minDegrees / base));
      };
      // A meridian needs more horizontal room than a parallel needs vertically,
      // because longitude labels are wider than latitude labels.
      const lonInterval = Math.min(60, thin(lonSpan, baseLon, canvasWidth, 44));
      const latInterval = Math.min(60, thin(latSpan, baseLat, canvasHeight, 34));
      let centerLon = bounds.west + lonSpan / 2;
      if (centerLon > 180) centerLon -= 360;
      const centerLat = (bounds.south + bounds.north) / 2;
      const groundCenter = getScreenCenterGround();
      // Labels stay on their own geographic line while sliding along it as the
      // camera moves; rebuilds on moveEnd still choose the visible set.
      const labelLon = Math.max(-179.99, Math.min(179.99,
        groundCenter.valid ? groundCenter.longitude : centerLon));
      const labelLat = Math.max(-89.99, Math.min(89.99,
        groundCenter.valid ? groundCenter.latitude : centerLat));
      this.labelViewCenter = { lon: labelLon, lat: labelLat };
      // Parallel labels stay on the centre column while meridian labels use a
      // band slightly above centre, so the two families cannot stack on the
      // same pixel at world scale and neither is pushed against a viewport edge.
      const parallelLabelLon = labelLon;
      const meridianLabelLat = Math.max(-89, Math.min(89, labelLat + latSpan * 0.18));
      const is2D = viewer.scene.mode !== Cesium.SceneMode.SCENE3D;

      const majorColor = this.lineColor(this.config.gridMajorColor, '#FF5252', 0.92);
      const lonColor = this.lineColor(this.config.gridLongitudeColor, '#3BA9FF', 0.78);
      const latColor = this.lineColor(this.config.gridLatitudeColor, '#3AD17B', 0.78);
      const utmColor = this.lineColor(this.config.gridUtmColor, '#FFC107', 0.82);

      // Major references are spread across three longitudes and two latitudes
      // instead of stacking on a single point at the viewport centre.
      const spreadLon = (index) => {
        const step = Math.max(15, Math.min(60, lonSpan * 0.3));
        let lon = labelLon + ((index % 3) - 1) * step;
        if (lon > 179.99) lon -= 360;
        if (lon < -179.99) lon += 360;
        return lon;
      };
      const spreadLat = (index) => {
        const step = Math.max(10, Math.min(30, latSpan * 0.25));
        const lat = labelLat + (index % 2 === 0 ? step : -step);
        return Math.max(-89, Math.min(89, lat));
      };

      // Independent key geographic reference lines and points.
      // Displays the official feature name only (no degree coordinates appended)
      // with bilingual Arabic/English support.
      if (this.config.showKeyParallels) {
        const isAr = EngineLocale.isArabic;
        const nameEquator = isAr ? 'خط الاستواء' : 'Equator';
        const nameCancer = isAr ? 'مدار السرطان' : 'Tropic of Cancer';
        const nameCapricorn = isAr ? 'مدار الجدي' : 'Tropic of Capricorn';
        const nameArctic = isAr ? 'الدائرة القطبية الشمالية' : 'Arctic Circle';
        const nameAntarctic = isAr ? 'الدائرة القطبية الجنوبية' : 'Antarctic Circle';
        const namePrimeMeridian = isAr ? 'خط جرينتش' : 'Prime Meridian';
        const nameDateLine = isAr ? 'خط التاريخ الدولي' : 'International Date Line';
        const nameNorthPole = isAr ? 'القطب الشمالي' : 'North Pole';
        const nameSouthPole = isAr ? 'القطب الجنوبي' : 'South Pole';
        const nameOrigin = isAr ? 'نقطة الأصل (0, 0)' : 'Grid Origin (0, 0)';

        this.addParallel(0, majorColor, 2.6, nameEquator, true, spreadLon(1), -18);
        this.addParallel(23.4365, majorColor, 2, nameCancer, true, spreadLon(0), -16);
        this.addParallel(-23.4365, majorColor, 2, nameCapricorn, true, spreadLon(2), -16);
        this.addParallel(66.5635, majorColor, 1.8, nameArctic, true, spreadLon(1), -16);
        this.addParallel(-66.5635, majorColor, 1.8, nameAntarctic, true, spreadLon(0), -16);
        this.addMeridian(0, majorColor, 2.6, namePrimeMeridian, true, spreadLat(0), -90, 90, -18);
        this.addMeridian(180, majorColor, 2, nameDateLine, true, spreadLat(1), -90, 90, -18);

        this.addKeyPoint(0, 89.999, nameNorthPole, majorColor, { offsetY: is2D ? 16 : -20 });
        this.addKeyPoint(0, -89.999, nameSouthPole, majorColor, { offsetY: is2D ? -16 : -20 });
        this.addKeyPoint(0, 0, nameOrigin, majorColor, { offsetX: 14, offsetY: 12, horizontalOrigin: Cesium.HorizontalOrigin.LEFT });
      }

      // Responsive label spacing: relaxed margins so coordinate labels stay visible and clear
      const edgePadLat = Math.min(latInterval * 0.3, latSpan * 0.04);
      const edgePadLon = Math.min(lonInterval * 0.3, lonSpan * 0.05);
      const latLabelStep = Math.max(1,
        Math.ceil(((latSpan / canvasHeight) * 28) / latInterval));
      const lonLabelStep = Math.max(1,
        Math.ceil(((lonSpan / canvasWidth) * 60) / lonInterval));

      if (this.config.showLatitudeLines) {
        const startLat = Math.ceil(lineBounds.south / latInterval) * latInterval;
        const endLat = Math.floor(lineBounds.north / latInterval) * latInterval;
        let index = 0;
        for (let lat = startLat; lat <= endLat + latInterval * 0.1; lat += latInterval, index++) {
          if ((this.config.showKeyParallels &&
              [0, 23.4365, -23.4365, 66.5635, -66.5635].some(v => Math.abs(lat - v) < 1e-6)) ||
              Math.abs(lat) >= 89.9) continue;
          // Never place a short regular label against a long major-reference
          // label. The threshold is a small fraction of one interval, not a
          // whole interval, or a 20° grid would lose most of its labels.
          const nearMajorLabel = this.config.showKeyParallels &&
            [0, 23.4365, -23.4365, 66.5635, -66.5635]
              .some(v => Math.abs(lat - v) < Math.min(1.2, latInterval * 0.25));
          const nearEdge = lat - bounds.south < edgePadLat || bounds.north - lat < edgePadLat;
          const showLabel = index % latLabelStep === 0 && !nearMajorLabel && !nearEdge;
          this.addParallel(lat, latColor, 1,
            window.ZEarthGraticuleLabels.formatDegrees(lat, 'lat', this.config.gridLabelFormat, latInterval),
            showLabel, parallelLabelLon, -14);
        }
      }

      if (this.config.showLongitudeLines) {
        for (const windowBounds of lineLongitudeWindows) {
          const startLon = Math.ceil(windowBounds.west / lonInterval) * lonInterval;
          const endLon = Math.floor(windowBounds.east / lonInterval) * lonInterval;
          let index = 0;
          for (let lon = startLon; lon <= endLon + lonInterval * 0.1; lon += lonInterval, index++) {
            if (this.config.showKeyParallels &&
                (Math.abs(lon) < 1e-6 || Math.abs(Math.abs(lon) - 180) < 1e-6)) continue;
            const nearMajorLabel = this.config.showKeyParallels &&
              (Math.abs(lon) < Math.min(1.2, lonInterval * 0.25) ||
               Math.abs(Math.abs(lon) - 180) < Math.min(1.2, lonInterval * 0.25));
            const visibleWindow = longitudeWindows.find(w => lon >= w.west && lon <= w.east);
            const nearEdge = !visibleWindow || lon - visibleWindow.west < edgePadLon ||
              visibleWindow.east - lon < edgePadLon;
            const showLabel = index % lonLabelStep === 0 && !nearMajorLabel && !nearEdge;
            this.addMeridian(lon, lonColor, 1,
              window.ZEarthGraticuleLabels.formatDegrees(lon, 'lon', this.config.gridLabelFormat, lonInterval),
              showLabel, meridianLabelLat, -90, 90, -14);
          }
        }
      }

      // UTM bands: latitude -80..84, zone 1..60, and Norway/Svalbard exceptions.
      // Boundaries are drawn at zone edges; labels are placed inside the zone.
      if (this.config.showUtmZones) {
        const segments = window.ZEarthGraticuleGeometry.visibleUtmSegments(lineBounds);
        const drawnBoundaries = new Set();
        // Zone edges sit on the 6° lattice starting at -180, so thinning keeps
        // them structurally correct instead of producing arbitrary meridians.
        const utmStep = Math.max(1, Math.ceil(((lonSpan / canvasWidth) * 52) / 6));
        for (const segment of segments) {
          // Both edges of every segment, deduplicated so a shared meridian is
          // never drawn twice and the last zone is not left open.
          for (const boundary of [segment.west, segment.east]) {
            if (utmStep > 1) {
              const lattice = Math.round((boundary + 180) / 6);
              if (lattice % utmStep !== 0) continue;
            }
            const key = boundary.toFixed(6) + ':' + segment.south.toFixed(3) + ':' + segment.north.toFixed(3);
            if (drawnBoundaries.has(key)) continue;
            drawnBoundaries.add(key);
            // Never draw an extra UTM boundary over a major or ordinary meridian.
            const isMajor = this.config.showKeyParallels &&
              (Math.abs(boundary) < 1e-6 || Math.abs(Math.abs(boundary) - 180) < 1e-6);
            const regularOverlap = this.config.showLongitudeLines &&
              Math.abs(boundary / lonInterval - Math.round(boundary / lonInterval)) < 1e-7;
            if (isMajor || regularOverlap) continue;
            if (!lineLongitudeWindows.some(w => boundary >= w.west && boundary <= w.east)) continue;
            this.addMeridian(boundary, utmColor, 1.4, '', false, labelLat,
              segment.south, segment.north, 0);
          }
        }

        // One label per zone at the center of its zone/band; hide only when the
        // whole globe is visible and 60 labels would overlap.
        if (lonSpan <= 120 && latSpan <= 60) {
          const labels = window.ZEarthGraticuleGeometry.visibleUtmZoneLabels(bounds);
          const labelPixels = Math.max(10, this.labelSizePixels() - 1);
          // Collision handling: convert the approximate rendered text width into
          // degrees and show only every Nth zone when 6° of longitude is narrower
          // than the label. This prevents the overlapping label pile that a fixed
          // "one label per zone" rule produced at regional zoom.
          const sample = window.ZEarthGraticuleGeometry.formatUtmZoneLabel(
            36, 30, this.config.utmLabelMode) || '36R';
          const canvasWidth = Math.max(320, viewer.canvas?.clientWidth || 1200);
          const degreesPerPixel = lonSpan / canvasWidth;
          const labelSpanDegrees = sample.length * labelPixels * 0.62 * degreesPerPixel;
          const zoneStep = Math.max(1, Math.ceil(labelSpanDegrees / 6));
          for (const zoneLabel of labels) {
            if (zoneStep > 1 && zoneLabel.zone % zoneStep !== 0) continue;
            const text = window.ZEarthGraticuleGeometry.formatUtmZoneLabel(
              zoneLabel.zone, zoneLabel.latitude, this.config.utmLabelMode);
            this.addLabel(text, zoneLabel.longitude, zoneLabel.latitude, {
              pixels: labelPixels,
              maxDistance: 14000000,
              offsetY: 0
            });
          }
        }
      }
      viewer.scene.requestRender();
    },

    // Cesium's ground-polyline primitive is a 3D-only feature: in 2D (and
    // Columbus View) a clamped polyline is silently dropped, so the whole grid
    // vanished when the user switched to 2D. Height-0 polylines are the correct
    // representation in 2D because there is no terrain surface to clamp to.
    groundClamp() {
      return viewer.scene.mode === Cesium.SceneMode.SCENE3D;
    },

    // The spherical horizon test only makes sense on a globe. In 2D the whole
    // map plane is visible at once, so applying it there hid every label that
    // was not near the centre of the viewport.
    surfaceVisible(anchor, normal) {
      if (viewer.scene.mode !== Cesium.SceneMode.SCENE3D) return true;
      return window.ZEarthGraticuleGeometry.isSurfaceAnchorVisible(
        anchor, normal, viewer.camera.positionWC);
    },

    // Bounds arithmetic is not enough to place a label: a tilted 3D view reports
    // a view rectangle much wider than the pixels on screen. Cesium 1.146 also
    // renamed wgs84ToWindowCoordinates to worldToWindowCoordinates, so both
    // names are supported rather than silently losing the projection.
    projectToScreen(anchor) {
      try {
        const transforms = Cesium.SceneTransforms;
        const project = transforms.worldToWindowCoordinates || transforms.wgs84ToWindowCoordinates;
        return project ? project.call(transforms, viewer.scene, anchor) : null;
      } catch (e) {
        return null;
      }
    },

    // Reset per rebuild. Labels are added in priority order, so the first label
    // to claim an area keeps it.
    resetLabelLayout() {
      this.labelBoxes = [];
    },

    reserveLabelSpace(text, screen, pixels, horizontalOrigin, offsetX, offsetY) {
      if (!screen) return true;
      const width = Math.max(24, text.length * pixels * 0.58) + 10;
      const height = pixels * 1.65 + 6;
      let left;
      if (horizontalOrigin === Cesium.HorizontalOrigin.LEFT) {
        left = screen.x + offsetX;
      } else if (horizontalOrigin === Cesium.HorizontalOrigin.RIGHT) {
        left = screen.x + offsetX - width;
      } else {
        left = screen.x + offsetX - width / 2;
      }
      const top = screen.y + offsetY - height / 2;
      const box = { left, right: left + width, top, bottom: top + height };
      const boxes = this.labelBoxes || (this.labelBoxes = []);
      for (const other of boxes) {
        if (box.left < other.right && box.right > other.left &&
            box.top < other.bottom && box.bottom > other.top) {
          return false;
        }
      }
      boxes.push(box);
      return true;
    },

    addParallel(lat, color, width, label, showLabel, preferredLon, offsetY) {
      const coords = [];
      for (let lon = -180; lon <= 180; lon += 2) {
        coords.push(Cesium.Cartesian3.fromDegrees(lon, lat));
      }
      this.dataSource.entities.add({
        polyline: {
          positions: coords,
          width: width,
          material: color,
          clampToGround: this.groundClamp()
        }
      });
      if (showLabel && label) {
        const labelLon = Number.isFinite(preferredLon) ? preferredLon : 0;
        this.addLabel(label, labelLon, lat, {
          axis: 'parallel',
          offsetY: offsetY,
          maxDistance: 20000000
        });
      }
    },

    addMeridian(lon, color, width, label, showLabel, preferredLat, south, north, offsetY) {
      const startSouth = Number.isFinite(south) ? south : -90;
      const endNorth = Number.isFinite(north) ? north : 90;
      const coords = [];
      for (let lat = startSouth; lat < endNorth; lat += 2) {
        coords.push(Cesium.Cartesian3.fromDegrees(lon, lat));
      }
      coords.push(Cesium.Cartesian3.fromDegrees(lon, endNorth));
      this.dataSource.entities.add({
        polyline: {
          positions: coords,
          width: width,
          material: color,
          clampToGround: this.groundClamp()
        }
      });
      if (showLabel && label) {
        const labelLat = Number.isFinite(preferredLat)
          ? Math.max(startSouth, Math.min(endNorth, preferredLat))
          : 0;
        this.addLabel(label, lon, labelLat, {
          axis: 'meridian',
          offsetY: offsetY,
          maxDistance: 20000000
        });
      }
    },

    addKeyPoint(lon, lat, label, color, options) {
      const settings = options || {};
      const anchor = Cesium.Cartesian3.fromDegrees(lon, lat);
      const normal = Cesium.Ellipsoid.WGS84.geodeticSurfaceNormal(anchor, new Cesium.Cartesian3());
      const visibleFromCamera = new Cesium.CallbackProperty(() =>
        this.surfaceVisible(anchor, normal), false);
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
        }
      });
      this.addLabel(label, lon, lat, {
        offsetX: Number.isFinite(settings.offsetX) ? settings.offsetX : 0,
        offsetY: Number.isFinite(settings.offsetY) ? settings.offsetY : -20,
        horizontalOrigin: settings.horizontalOrigin,
        maxDistance: 22000000
      });
    },

    setConfig(cfg) {
      if (!cfg) return;
      if (typeof cfg.visible === 'boolean') this.config.visible = cfg.visible;
      if (typeof cfg.showLabels === 'boolean') this.config.showLabels = cfg.showLabels;
      if (['DD', 'DM', 'DMS', 'Degrees'].includes(cfg.gridLabelFormat)) this.config.gridLabelFormat = cfg.gridLabelFormat;
      if (['Both', 'Hemisphere', 'GridZone'].includes(cfg.utmLabelMode)) this.config.utmLabelMode = cfg.utmLabelMode;
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
      if (['Small', 'Medium', 'Large', 'ExtraLarge'].includes(cfg.gridLabelSize)) {
        this.config.gridLabelSize = cfg.gridLabelSize;
      }
      if (cfg.utmLabelPlacement === 'Center') this.config.utmLabelPlacement = 'Center';
      for (const key of Object.keys(GRID_DEFAULT_COLORS)) {
        if (typeof cfg[key] !== 'string' || !cfg[key].trim()) continue;
        try {
          if (Cesium.Color.fromCssColorString(cfg[key].trim())) {
            this.config[key] = cfg[key].trim();
          }
        } catch (error) {
          // Invalid user colour: keep the previous valid value.
        }
      }
      this.buildGraticules();
      viewer.scene.requestRender();
    },

    updateLabelViewCenter() {
      const ground = getScreenCenterGround();
      if (ground.valid) {
        this.labelViewCenter = { lon: ground.longitude,
          lat: Math.max(-89.99, Math.min(89.99, ground.latitude)) };
        return;
      }
      const rectangle = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      if (!rectangle) return;
      const west = Cesium.Math.toDegrees(rectangle.west);
      const east = Cesium.Math.toDegrees(rectangle.east);
      const span = east >= west ? east - west : 360 - west + east;
      let lon = west + span / 2;
      if (lon > 180) lon -= 360;
      const lat = (Cesium.Math.toDegrees(rectangle.south) + Cesium.Math.toDegrees(rectangle.north)) / 2;
      this.labelViewCenter = { lon, lat: Math.max(-89.99, Math.min(89.99, lat)) };
    },

    refreshWhileMoving() {
      const now = Date.now();
      if (this.motionRefreshPending || now - (this.lastMotionCheck || 0) < 250) return;
      this.lastMotionCheck = now;
      const rectangle = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      if (!rectangle) return;
      const current = {
        west: Cesium.Math.toDegrees(rectangle.west), east: Cesium.Math.toDegrees(rectangle.east),
        south: Cesium.Math.toDegrees(rectangle.south), north: Cesium.Math.toDegrees(rectangle.north)
      };
      if (window.ZEarthGraticuleGeometry.boundsContain(this.lineCoverage, current)) return;
      this.motionRefreshPending = true;
      window.requestAnimationFrame(() => {
        this.motionRefreshPending = false;
        if (this.config.visible) this.refreshForCamera();
      });
    },

    refreshForCamera() {
      if (!this.config.visible) return;
      const rectangle = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      const q = value => Cesium.Math.toDegrees(value).toFixed(5);
      const boundsKey = rectangle
        ? `${q(rectangle.west)}/${q(rectangle.south)}/${q(rectangle.east)}/${q(rectangle.north)}`
        : 'space';
      const altitude = Math.round((viewer.camera.positionCartographic?.height || 0) / 10000);
      const heading = Math.round(Cesium.Math.toDegrees(viewer.camera.heading || 0));
      const pitch = Math.round(Cesium.Math.toDegrees(viewer.camera.pitch || 0));
      const key = `${viewer.scene.mode}/${boundsKey}/${altitude}/${heading}/${pitch}/${viewer.canvas.clientWidth}x${viewer.canvas.clientHeight}`;
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
    activeProjection: null,

    resolveProjection(config) {
      const ellipsoid = (viewer && viewer.scene && viewer.scene.globe && viewer.scene.globe.ellipsoid) ||
        (typeof Cesium !== 'undefined' ? Cesium.Ellipsoid.WGS84 : null);
      if (typeof window !== 'undefined' && window.ZEarthMapProjections && typeof window.ZEarthMapProjections.create === 'function') {
        return window.ZEarthMapProjections.create(config, ellipsoid);
      }
      if (config && config.isProjected && typeof Cesium !== 'undefined' && Cesium.WebMercatorProjection) {
        return new Cesium.WebMercatorProjection(ellipsoid);
      }
      if (typeof Cesium !== 'undefined' && Cesium.GeographicProjection) {
        return new Cesium.GeographicProjection(ellipsoid);
      }
      return null;
    },

    set(config) {
      this.current = { ...this.current, ...(config || {}) };
      const newProj = this.resolveProjection(this.current);
      this.activeProjection = newProj;

      if (viewer && viewer.scene) {
        if (newProj) {
          viewer.scene._mapProjection = newProj;
        }

        // If the scene is currently in 2D mode, smoothly re-apply projection to update 2D geometry and view
        if (viewer.scene.mode === Cesium.SceneMode.SCENE2D) {
          try {
            viewer.scene.morphTo3D(0);
            if (typeof viewer.scene.completeMorph === 'function') {
              viewer.scene.completeMorph();
            }
            viewer.scene.morphTo2D(0.4);
          } catch (e) {
            console.warn('[Cesium Engine] 2D projection re-morph warning:', e);
          }
        }
        viewer.scene.requestRender();
      }

      sendHostMessage({ event: 'crs.changed', crs: this.current, projectionName: newProj?.name || this.current.name });
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

  const Model3DManager = {
    primitives: new Map(),
    validate(config) {
      const longitude = Number(config?.longitude);
      const latitude = Number(config?.latitude);
      const dataUri = config?.dataUri;
      if (!config?.id || typeof dataUri !== 'string' ||
          !/^data:model\/gltf-binary;base64,[A-Za-z0-9+/=]+$/.test(dataUri) ||
          dataUri.length > 11200000 || !Number.isFinite(longitude) ||
          !Number.isFinite(latitude) || longitude < -180 || longitude > 180 ||
          latitude < -90 || latitude > 90) {
        throw new Error('Invalid self-contained GLB model payload or anchor coordinates.');
      }
      return { longitude, latitude, dataUri };
    },
    remove(id) {
      const existing = this.primitives.get(String(id));
      if (!existing) return false;
      viewer.scene.primitives.remove(existing);
      this.primitives.delete(String(id));
      viewer.scene.requestRender();
      return true;
    },
    clear() {
      for (const primitive of this.primitives.values()) {
        viewer.scene.primitives.remove(primitive);
      }
      this.primitives.clear();
      viewer.scene.requestRender();
    },
    async add(config) {
      const checked = this.validate(config);
      const id = String(config.id);
      this.remove(id);
      const position = Cesium.Cartesian3.fromDegrees(
        checked.longitude, checked.latitude, Number(config.altitude) || 0);
      let modelMatrix = Cesium.Transforms.eastNorthUpToFixedFrame(position);
      const scale = Math.max(0.001, Math.min(10000, Number(config.scale) || 1));
      modelMatrix = Cesium.Matrix4.multiplyByUniformScale(modelMatrix, scale, new Cesium.Matrix4());
      const primitive = await Cesium.Model.fromGltfAsync({
        id: `zearth-model-${id}`,
        url: checked.dataUri,
        modelMatrix,
        minimumPixelSize: 64,
        maximumScale: 5000
      });
      viewer.scene.primitives.add(primitive);
      this.primitives.set(id, primitive);
      viewer.scene.requestRender();
      return { id, loaded: true, name: String(config.name || id) };
    },
    async set(configs) {
      if (!Array.isArray(configs)) throw new Error('models.set requires an array payload.');
      this.clear();
      try {
        const loaded = [];
        for (const config of configs) loaded.push(await this.add(config));
        return { count: loaded.length, loaded: true };
      } catch (error) {
        this.clear();
        throw error;
      }
    }
  };

  const EnvironmentManager = {
    sunlightEnabled: false,
    atmosphereEnabled: true,
    animationTimer: null,
    animationSpeed: 60,
    dateUtc: new Date().toISOString().slice(0, 10),
    setTimeOfDay(timeHours, dateUtc, isRealTime = false) {
      const hours = Math.max(0, Math.min(24, Number(timeHours) || 0));
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
      viewer.clock.shouldAnimate = false;

      const slider = document.getElementById('sunlight-slider');
      const label = document.getElementById('sunlight-time-label');
      const datePicker = document.getElementById('sunlight-date-picker');
      const phaseIcon = document.getElementById('sunlight-phase-icon');
      const phaseText = document.getElementById('sunlight-phase-text');

      if (slider) slider.value = String(hours);
      if (label) label.textContent = `${String(whole).padStart(2, '0')}:${String(minutes).padStart(2, '0')} UTC`;
      if (datePicker && datePicker.value !== this.dateUtc) datePicker.value = this.dateUtc;

      // Update solar phase indicator
      if (phaseIcon && phaseText) {
        const isAr = EngineLocale.isArabic;
        if (hours >= 5.0 && hours < 7.0) {
          phaseIcon.textContent = '🌅';
          phaseText.textContent = isAr ? 'شروق' : 'Dawn';
        } else if (hours >= 7.0 && hours < 17.5) {
          phaseIcon.textContent = '☀️';
          phaseText.textContent = isAr ? 'نهار' : 'Daylight';
        } else if (hours >= 17.5 && hours < 19.5) {
          phaseIcon.textContent = '🌇';
          phaseText.textContent = isAr ? 'غروب' : 'Dusk';
        } else {
          phaseIcon.textContent = '🌙';
          phaseText.textContent = isAr ? 'ليل' : 'Night';
        }
      }

      sendHostMessage({ event: 'environment.timeChanged', timeHours: hours,
        dateUtc: this.dateUtc, isRealTime });
      viewer.scene.requestRender();
      return hours;
    },
    setSunlight(config) {
      const enabled = config?.enabled !== false;
      this.sunlightEnabled = enabled;
      viewer.scene.globe.enableLighting = enabled;
      viewer.shadows = enabled;
      const hud = document.getElementById('sunlight-hud');
      if (hud) hud.style.display = enabled ? 'flex' : 'none';
      if (typeof config?.timeHours === 'number') this.setTimeOfDay(config.timeHours, config.dateUtc);
      else if (enabled) {
        const now = new Date();
        this.setTimeOfDay(now.getUTCHours() + now.getUTCMinutes() / 60,
          now.toISOString().slice(0, 10), true);
      }
      viewer.scene.requestRender();
      sendHostMessage({ event: 'environment.sunlightChanged', enabled });
      return enabled;
    },
    setAtmosphere(config) {
      const enabled = config?.enabled !== false;
      this.atmosphereEnabled = enabled;
      if (viewer.scene.skyAtmosphere) {
        viewer.scene.skyAtmosphere.show = enabled;
        viewer.scene.skyAtmosphere.hueShift = 0.0;
        viewer.scene.skyAtmosphere.saturationShift = 0.15;
        viewer.scene.skyAtmosphere.brightnessShift = 0.1;
      }
      if (viewer.scene.globe) {
        viewer.scene.globe.showGroundAtmosphere = enabled;
        viewer.scene.globe.atmosphereLightIntensity = enabled ? 3.8 : 0.0;
        viewer.scene.globe.atmosphereHueShift = 0.0;
        viewer.scene.globe.atmosphereSaturationShift = 0.15;
      }
      if (viewer.scene.fog) {
        viewer.scene.fog.enabled = enabled;
        viewer.scene.fog.density = enabled ? 0.0002 : 0.0;
      }
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
      const stepHours = (this.animationSpeed / 3600);
      this.animationTimer = setInterval(() => {
        const slider = document.getElementById('sunlight-slider');
        const current = Number(slider?.value || 0);
        this.setTimeOfDay((current + stepHours * 0.1) % 24);
      }, 100);
      return true;
    }
  };

  // Google Earth Pro Style Save Image Manager
  const SaveImageManager = {
    visible: false,
    show() {
      this.visible = true;
      const hud = document.getElementById('save-image-hud');
      if (hud) hud.style.display = 'block';
      this.syncMetadata();
      viewer.scene.requestRender();
      sendHostMessage({ event: 'saveImage.changed', visible: true });
    },
    hide() {
      this.visible = false;
      const hud = document.getElementById('save-image-hud');
      if (hud) hud.style.display = 'none';
      viewer.scene.requestRender();
      sendHostMessage({ event: 'saveImage.changed', visible: false });
    },
    toggle() {
      if (this.visible) this.hide();
      else this.show();
      return this.visible;
    },
    syncMetadata() {
      const carto = viewer.camera.positionCartographic;
      if (carto) {
        const lat = Cesium.Math.toDegrees(carto.latitude);
        const lon = Cesium.Math.toDegrees(carto.longitude);
        const latStr = Math.abs(lat).toFixed(4) + '° ' + (lat >= 0 ? 'N' : 'S');
        const lonStr = Math.abs(lon).toFixed(4) + '° ' + (lon >= 0 ? 'E' : 'W');
        const coordsEl = document.getElementById('save-meta-coords');
        if (coordsEl) coordsEl.textContent = `${latStr}, ${lonStr}`;

        const altitude = carto.height || 500000;
        const distKm = Math.max(1, Math.round(altitude / 5000));
        const midEl = document.getElementById('save-scale-mid');
        const maxEl = document.getElementById('save-scale-max');
        if (midEl) midEl.textContent = String(distKm);
        if (maxEl) maxEl.textContent = `${distKm * 2} km`;
      }
      const dateEl = document.getElementById('save-meta-date');
      if (dateEl) dateEl.textContent = new Date().toISOString().slice(0, 10);

      const satNameEl = document.getElementById('save-legend-sat-name');
      if (satNameEl) {
        satNameEl.textContent = ImageryManager.activeBasemapId || 'Satellite Basemap';
      }
    },
    exportImage() {
      const toolbar = document.querySelector('.save-image-toolbar');
      if (toolbar) toolbar.style.opacity = '0';
      viewer.scene.requestRender();
      setTimeout(() => {
        sendHostMessage({ event: 'saveImage.triggerExport' });
        if (toolbar) toolbar.style.opacity = '1';
      }, 70);
    }
  };

  const CloudManager = window.ZEarthCloudLayer.create({
    Cesium, viewer,
    onChange: state => sendHostMessage({
      event: 'environment.cloudsChanged',
      enabled: state.enabled,
      rendered: state.rendered,
      count: state.count
    })
  });

  // Independent basic 3D flight navigation. This is a camera-based simulation,
  // not a Google Earth asset, aircraft dynamics model, or certified flight tool.
  const FlightSimulator = window.ZEarthFlightSimulator.create({
    Cesium, viewer,
    onChange: state => sendHostMessage({ event: 'flight.changed', enabled: state.enabled })
  });

  // User-selected NASA daily MODIS overlay; not Google Earth historical imagery.
  // A successful command configures WMS, it does not prove all tiles are available.
  const NasaGibsManager = window.ZEarthNasaGibs.create({
    Cesium, viewer,
    onStatus: details => sendHostMessage(details)
  });
  const GpsPlayback = window.ZEarthGpsPlayback.create({
    Cesium, viewer,
    onChange: details => sendHostMessage({ event: 'gps.playbackChanged', ...details })
  });

  if (window.ZEarthWeatherStations) {
    WeatherStationsManager = window.ZEarthWeatherStations.create({
      Cesium, viewer,
      onStatus: details => sendHostMessage(details)
    });
  }

  if (window.ZEarthWorldGazetteer) {
    WorldGazetteerManager = window.ZEarthWorldGazetteer.create({
      Cesium, viewer,
      isArabic: false,
      onStatus: details => sendHostMessage(details)
    });
  }

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

  const OverviewMapController = {
    container: null,
    canvas: null,
    box: null,
    ctx: null,
    visible: false,
    initialized: false,

    init() {
      if (this.initialized) return;
      this.container = document.getElementById('overview-map-container');
      this.canvas = document.getElementById('overview-map-canvas');
      this.box = document.getElementById('overview-viewport-box');
      const btnClose = document.getElementById('btn-overview-close');
      if (btnClose) {
        btnClose.addEventListener('click', () => this.setVisible(false));
      }
      if (this.canvas) {
        this.ctx = this.canvas.getContext('2d');
        this.drawWorldMap();
        this.canvas.addEventListener('click', (e) => {
          const rect = this.canvas.getBoundingClientRect();
          const x = e.clientX - rect.left;
          const y = e.clientY - rect.top;
          const lon = (x / this.canvas.width) * 360 - 180;
          const lat = 90 - (y / this.canvas.height) * 180;
          viewer.camera.flyTo({
            destination: Cesium.Cartesian3.fromDegrees(lon, lat, Math.max(1000000, viewer.camera.positionCartographic.height)),
            duration: 1.2
          });
        });
      }
      this.initialized = true;
    },

    drawWorldMap() {
      if (!this.ctx || !this.canvas) return;
      const w = this.canvas.width;
      const h = this.canvas.height;
      const ctx = this.ctx;

      // Ocean background
      ctx.fillStyle = '#0c4a6e';
      ctx.fillRect(0, 0, w, h);

      // Equator and central meridian lines
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.lineWidth = 0.5;
      ctx.beginPath();
      ctx.moveTo(0, h / 2);
      ctx.lineTo(w, h / 2);
      ctx.moveTo(w / 2, 0);
      ctx.lineTo(w / 2, h);
      ctx.stroke();

      // Simplified equirectangular continents polygons
      ctx.fillStyle = '#1e3a5f';
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 0.75;

      const toX = lon => ((lon + 180) / 360) * w;
      const toY = lat => ((90 - lat) / 180) * h;

      const continents = [
        // North America
        [[-168,66], [-160,71], [-130,70], [-80,70], [-60,50], [-70,42], [-80,25], [-97,20], [-105,25], [-120,35], [-125,50], [-140,60], [-168,66]],
        // South America
        [[-80,10], [-50,-5], [-35,-5], [-40,-22], [-55,-40], [-70,-55], [-75,-40], [-80,-5], [-80,10]],
        // Europe
        [[-10,36], [0,44], [10,45], [30,40], [35,60], [25,71], [10,60], [-5,55], [-10,36]],
        // Africa
        [[-15,35], [30,32], [50,12], [40,-10], [30,-34], [18,-34], [10,-5], [-15,12], [-15,35]],
        // Asia
        [[35,60], [60,65], [100,75], [170,68], [140,40], [120,25], [105,10], [80,10], [60,25], [45,35], [35,60]],
        // Australia
        [[115,-20], [135,-12], [150,-24], [150,-38], [135,-35], [115,-32], [115,-20]],
        // Antarctica
        [[-180,-75], [180,-75], [180,-90], [-180,-90]]
      ];

      for (const poly of continents) {
        ctx.beginPath();
        for (let i = 0; i < poly.length; i++) {
          const pt = poly[i];
          const px = toX(pt[0]);
          const py = toY(pt[1]);
          if (i === 0) ctx.moveTo(px, py);
          else ctx.lineTo(px, py);
        }
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
      }
    },

    updateViewport() {
      if (!this.visible || !this.box || !this.canvas) return;
      const rect = viewer.camera.computeViewRectangle(viewer.scene.globe.ellipsoid);
      let west = -180, east = 180, south = -90, north = 90;
      if (rect) {
        west = Cesium.Math.toDegrees(rect.west);
        east = Cesium.Math.toDegrees(rect.east);
        south = Cesium.Math.toDegrees(rect.south);
        north = Cesium.Math.toDegrees(rect.north);
      } else {
        const carto = viewer.camera.positionCartographic;
        if (carto) {
          const clat = Cesium.Math.toDegrees(carto.latitude);
          const clon = Cesium.Math.toDegrees(carto.longitude);
          const span = Math.min(60, Math.max(5, (carto.height / 20000000) * 80));
          west = clon - span;
          east = clon + span;
          south = clat - span / 2;
          north = clat + span / 2;
        }
      }

      south = Math.max(-90, Math.min(90, south));
      north = Math.max(-90, Math.min(90, north));
      const w = this.canvas.width;
      const h = this.canvas.height;
      let x1 = ((west + 180) / 360) * w;
      let x2 = ((east + 180) / 360) * w;
      let y1 = ((90 - north) / 180) * h;
      let y2 = ((90 - south) / 180) * h;

      if (x2 < x1) x2 += w;
      const boxW = Math.max(8, Math.min(w, x2 - x1));
      const boxH = Math.max(6, Math.min(h, y2 - y1));
      const boxLeft = Math.max(0, Math.min(w - boxW, x1 % w));
      const boxTop = Math.max(0, Math.min(h - boxH, y1));

      this.box.style.left = `${boxLeft}px`;
      this.box.style.top = `${boxTop}px`;
      this.box.style.width = `${boxW}px`;
      this.box.style.height = `${boxH}px`;
    },

    setVisible(show) {
      this.init();
      this.visible = Boolean(show);
      if (this.container) {
        this.container.style.display = this.visible ? 'block' : 'none';
      }
      if (this.visible) {
        this.updateViewport();
      }
      sendHostMessage({ event: 'overviewMap.changed', visible: this.visible });
    }
  };

  const CelestialBodyManager = {
    currentBody: 'earth',
    priorEarthBasemap: 'esri-hybrid',

    setBody(name) {
      const target = String(name || 'earth').toLowerCase();
      if (target === this.currentBody) return;

      if (target === 'moon') {
        if (this.currentBody === 'earth') {
          this.priorEarthBasemap = ImageryManager.activeBasemap;
        }
        this.currentBody = 'moon';
        if (viewer.scene.globe) {
          viewer.scene.globe._ellipsoid = Cesium.Ellipsoid.MOON;
        }
        ImageryManager.registerDynamicBasemap({
          id: 'celestial-moon',
          name: 'Moon Global (LRO/WAC)',
          url: 'https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02/1.0.0//default/default028mm/{z}/{y}/{x}.jpg',
          attribution: 'NASA / GSFC / ASU (LRO WAC Global)',
          maximumLevel: 8,
          setActive: true
        });
        viewer.camera.flyTo({
          destination: Cesium.Cartesian3.fromDegrees(0, 0, 5000000, Cesium.Ellipsoid.MOON),
          duration: 1.5
        });
      } else if (target === 'mars') {
        if (this.currentBody === 'earth') {
          this.priorEarthBasemap = ImageryManager.activeBasemap;
        }
        this.currentBody = 'mars';
        const marsEllipsoid = new Cesium.Ellipsoid(3396190.0, 3396190.0, 3376200.0);
        if (viewer.scene.globe) {
          viewer.scene.globe._ellipsoid = marsEllipsoid;
        }
        ImageryManager.registerDynamicBasemap({
          id: 'celestial-mars',
          name: 'Mars Global (Viking MDIM2.1)',
          url: 'https://trek.nasa.gov/tiles/Mars/EQ/Mars_Viking_MDIM21_ClrMosaic_global_232m/1.0.0//default/default028mm/{z}/{y}/{x}.jpg',
          attribution: 'NASA / USGS Astrogeology (Mars Viking)',
          maximumLevel: 7,
          setActive: true
        });
        viewer.camera.flyTo({
          destination: Cesium.Cartesian3.fromDegrees(0, 0, 9000000, marsEllipsoid),
          duration: 1.5
        });
      } else {
        this.currentBody = 'earth';
        if (viewer.scene.globe) {
          viewer.scene.globe._ellipsoid = Cesium.Ellipsoid.WGS84;
        }
        ImageryManager.setBasemap(this.priorEarthBasemap || 'esri-hybrid');
        viewer.camera.flyTo({
          destination: Cesium.Cartesian3.fromDegrees(25.0, 25.0, 15000000, Cesium.Ellipsoid.WGS84),
          duration: 1.5
        });
      }
      sendHostMessage({ event: 'celestialBody.changed', body: this.currentBody });
      viewer.scene.requestRender();
    }
  };

  // Draggable movable sunlight HUD (Google Earth Pro style)
  (function enableMovableSunlightHud() {
    const hud = document.getElementById('sunlight-hud');
    const handle = document.getElementById('sunlight-drag-handle');
    if (!hud || !handle) return;
    let drag = null;
    handle.addEventListener('pointerdown', event => {
      if (event.button !== 0 || event.target.closest('button, input, select')) return;
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

  document.getElementById('sunlight-slider')?.addEventListener('input', e => EnvironmentManager.setTimeOfDay(Number(e.target.value)));
  document.querySelectorAll('.sunlight-preset-btn').forEach(button =>
    button.addEventListener('click', () => EnvironmentManager.setTimeOfDay(Number(button.dataset.time))));
  document.getElementById('btn-sunlight-now')?.addEventListener('click', () => {
    const now = new Date();
    EnvironmentManager.setTimeOfDay(now.getUTCHours() + now.getUTCMinutes() / 60,
      now.toISOString().slice(0, 10), true);
  });
  document.getElementById('btn-sunlight-play')?.addEventListener('click', () => EnvironmentManager.toggleAnimation());
  document.getElementById('btn-sunlight-close')?.addEventListener('click', () => EnvironmentManager.setSunlight({ enabled: false }));

  // Sunlight date picker and day steppers
  document.getElementById('sunlight-date-picker')?.addEventListener('change', e => {
    const slider = document.getElementById('sunlight-slider');
    EnvironmentManager.setTimeOfDay(Number(slider?.value || 12), e.target.value);
  });
  document.getElementById('btn-sunlight-prev-day')?.addEventListener('click', () => {
    const cur = new Date((EnvironmentManager.dateUtc || new Date().toISOString().slice(0, 10)) + 'T00:00:00Z');
    cur.setUTCDate(cur.getUTCDate() - 1);
    const prevDate = cur.toISOString().slice(0, 10);
    const slider = document.getElementById('sunlight-slider');
    EnvironmentManager.setTimeOfDay(Number(slider?.value || 12), prevDate);
  });
  document.getElementById('btn-sunlight-next-day')?.addEventListener('click', () => {
    const cur = new Date((EnvironmentManager.dateUtc || new Date().toISOString().slice(0, 10)) + 'T00:00:00Z');
    cur.setUTCDate(cur.getUTCDate() + 1);
    const nextDate = cur.toISOString().slice(0, 10);
    const slider = document.getElementById('sunlight-slider');
    EnvironmentManager.setTimeOfDay(Number(slider?.value || 12), nextDate);
  });
  document.querySelectorAll('.sunlight-speed-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.sunlight-speed-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      EnvironmentManager.animationSpeed = Number(btn.dataset.speed || 60);
    });
  });

  // Wire Save Image Manager Controls (Google Earth Pro parity)
  document.getElementById('chk-save-title')?.addEventListener('change', e => {
    const el = document.getElementById('save-image-title-card');
    if (el) el.style.display = e.target.checked ? 'flex' : 'none';
  });
  document.getElementById('chk-save-legend')?.addEventListener('change', e => {
    const el = document.getElementById('save-image-legend-card');
    if (el) el.style.display = e.target.checked ? 'flex' : 'none';
  });
  document.getElementById('chk-save-compass')?.addEventListener('change', e => {
    const el = document.getElementById('save-image-compass-card');
    if (el) el.style.display = e.target.checked ? 'flex' : 'none';
  });
  document.getElementById('chk-save-scale')?.addEventListener('change', e => {
    const el = document.getElementById('save-image-scale-card');
    if (el) el.style.display = e.target.checked ? 'block' : 'none';
  });
  document.getElementById('btn-save-image-export')?.addEventListener('click', () => SaveImageManager.exportImage());
  document.getElementById('btn-save-image-close')?.addEventListener('click', () => SaveImageManager.hide());

  window.addEventListener('keydown', e => {
    if (e.key === 'Escape' && SaveImageManager.visible) {
      SaveImageManager.hide();
    }
  });

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
  
  // Wire measurement HUD unit dropdown
  const measureUnitSelect = document.getElementById('measure-unit-select');
  if (measureUnitSelect) {
    measureUnitSelect.addEventListener('change', () => {
      const val = measureUnitSelect.value;
      if (['AutoMetric', 'Meters', 'Kilometers', 'AutoImperial', 'Feet', 'Miles', 'NauticalMiles'].includes(val)) {
        UnitSettings.distanceUnit = val;
      }
      if (['AutoMetric', 'AutoImperial', 'Hectares', 'Acres', 'EgyptianFeddan'].includes(val)) {
        UnitSettings.areaUnit = val;
      }
      if (MeasurementManager.activeMode) {
        MeasurementManager.updateHUD();
      }
    });
  }

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

  function getCameraAltitudeAndZoom() {
    const is2D = viewer.scene.mode === Cesium.SceneMode.SCENE2D;
    let alt = 0;
    const canvas = viewer.canvas;
    const canvasWidth = (canvas && canvas.clientWidth > 0) ? canvas.clientWidth : 1024;

    if (is2D && viewer.camera.frustum && typeof viewer.camera.frustum.width === 'number') {
      const frustumWidth = Math.max(1.0, viewer.camera.frustum.width);
      // In 2D orthographic, equivalent eye altitude matching standard 60 deg horizontal FOV:
      alt = frustumWidth / (2.0 * Math.tan(Cesium.Math.toRadians(30.0)));
    } else {
      const carto = viewer.camera.positionCartographic;
      if (carto && !isNaN(carto.height)) {
        alt = carto.height;
      } else if (viewer.camera.position) {
        alt = Math.max(0.1, Cesium.Cartesian3.magnitude(viewer.camera.position) - 6378137.0);
      }
    }

    alt = Math.max(0.1, alt);

    // Visible ground width across the viewport
    const visibleWidth = (is2D && viewer.camera.frustum && typeof viewer.camera.frustum.width === 'number')
      ? viewer.camera.frustum.width
      : alt * 2.0 * Math.tan(Cesium.Math.toRadians(30.0));

    // Nominal Web Mercator zoom level:
    let cartoCenter = viewer.camera.positionCartographic;
    if (is2D && viewer.scene.mapProjection) {
      cartoCenter = viewer.scene.mapProjection.unproject(viewer.camera.position);
    }
    const centerLatRad = cartoCenter ? cartoCenter.latitude : 0.0;
    const cosLat = Math.max(0.01, Math.cos(centerLatRad));
    const groundRes = Math.max(0.0001, visibleWidth / canvasWidth);
    const nominalZoom = Math.log2((40075016.686 * cosLat) / (256.0 * groundRes));
    const zoomLevel = Math.max(0.0, Math.min(24.0, nominalZoom));

    return { altitude: alt, zoomLevel: zoomLevel };
  }

  function updateCoordUI(lat, lon, alt, heading, zoomLevel) {
    if (!statusEl) return;
    const latStr = `${Math.abs(lat).toFixed(4)}° ${lat >= 0 ? 'N' : 'S'}`;
    const lonStr = `${Math.abs(lon).toFixed(4)}° ${lon >= 0 ? 'E' : 'W'}`;
    let altStr = '';
    if (alt < 100) {
      altStr = `${alt.toFixed(1)} m`;
    } else if (alt < 1000) {
      altStr = `${alt.toFixed(0)} m`;
    } else if (alt < 10000) {
      altStr = `${(alt / 1000).toFixed(2)} km (${alt.toFixed(0)} m)`;
    } else if (alt < 100000) {
      altStr = `${(alt / 1000).toFixed(1)} km`;
    } else {
      altStr = `${(alt / 1000).toLocaleString(undefined, { maximumFractionDigits: 0 })} km`;
    }
    const zoomStr = zoomLevel !== undefined ? ` | Z${zoomLevel.toFixed(1)}` : '';
    const modeStr = viewer.scene.mode === Cesium.SceneMode.SCENE3D ? 'Scene3D' : 'Scene2D';
    statusEl.textContent = `${latStr} | ${lonStr} | Eye: ${altStr}${zoomStr} | Head: ${heading.toFixed(1)}° | ${modeStr}`;
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
    const telemetry = getCameraAltitudeAndZoom();
    const alt = telemetry.altitude;
    const zoom = telemetry.zoomLevel;
    let heading = ((Cesium.Math.toDegrees(viewer.camera.heading) % 360) + 360) % 360;
    if (Math.abs(heading - 360) < 0.001) heading = 0.0;
    const pitch = Cesium.Math.toDegrees(viewer.camera.pitch);
    const roll = Cesium.Math.toDegrees(viewer.camera.roll);

    updateCoordUI(lat, lon, alt, heading, zoom);
    OverviewMapController.updateViewport();

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
          zoomLevel: zoom,
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

    const telemetry = getCameraAltitudeAndZoom();
    const alt = telemetry.altitude;
    const zoom = telemetry.zoomLevel;
    let heading = ((Cesium.Math.toDegrees(viewer.camera.heading) % 360) + 360) % 360;
    if (Math.abs(heading - 360) < 0.001) heading = 0.0;

    sendHostMessage({
      event: 'camera.moveEnd',
      camera: {
        latitude: Cesium.Math.toDegrees(carto.latitude),
        longitude: Cesium.Math.toDegrees(carto.longitude),
        altitude: alt,
        zoomLevel: zoom,
        heading: heading,
        pitch: Cesium.Math.toDegrees(viewer.camera.pitch),
        roll: Cesium.Math.toDegrees(viewer.camera.roll)
      }
    });

    const centerEnd = getScreenCenterGround();
    CompassManager.update(centerEnd, heading);
    GraticuleManager.refreshForCamera();
    ImageryManager.onCameraMoveEnd();
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
    // Morphing projects the same entities into a different scene mode; rebuild
    // so no stale 3D primitives survive in 2D and no line is duplicated.
    GraticuleManager.lastViewKey = '';
    GraticuleManager.refreshForCamera();
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
  // Cesium's default double-click handler can start a competing zoom-to-entity
  // flight. This application's handler below owns the gesture instead.
  viewer.cesiumWidget.screenSpaceEventHandler.removeInputAction(Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
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
    startAnimatedZoom(true, position, 0.48);
  }, Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);

  // Report local elevations from raw numeric tiles, never from the ellipsoid
  // used to render gaps. Keep one request in flight and discard stale positions.
  let localCursorSerial = 0;
  let pendingLocalCursor = null;
  let localCursorRunning = false;
  async function sampleLocalCursor(position, serial) {
    pendingLocalCursor = { position, serial, provider: viewer.terrainProvider };
    if (localCursorRunning) return;
    localCursorRunning = true;
    try {
      while (pendingLocalCursor) {
        const request = pendingLocalCursor;
        pendingLocalCursor = null;
        try {
          const samples = await TerrainManager.sampleLocalRasterPoints([request.position]);
          if (TerrainManager.currentType === 'localDem' && request.provider === viewer.terrainProvider &&
              request.serial === localCursorSerial) {
            const height = samples[0]?.height;
            sendHostMessage({ event: 'map.cursorMoved', position: {
              valid: true, ...request.position,
              altitude: Number.isFinite(height) ? height : null
            }});
          }
        } catch (_) {
          // The already-sent unknown elevation stays visible on failure.
        }
      }
    } finally {
      localCursorRunning = false;
    }
  }

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
        const latitude = Cesium.Math.toDegrees(carto.latitude);
        const longitude = Cesium.Math.toDegrees(carto.longitude);
        const local = TerrainManager.currentType === 'localDem';
        const serial = ++localCursorSerial;
        sendHostMessage({
          event: 'map.cursorMoved',
          position: {
            valid: true, latitude, longitude,
            altitude: local ? null : (isNaN(carto.height) ? 0 : carto.height)
          }
        });
        if (local) sampleLocalCursor({ latitude, longitude }, serial);
        return;
      }
    }

    ++localCursorSerial;
    pendingLocalCursor = null;
    sendHostMessage({
      event: 'map.cursorMoved',
      position: { valid: false, latitude: 0, longitude: 0, altitude: 0 }
    });
  }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

  viewer.canvas.addEventListener('mouseleave', () => {
    ++localCursorSerial;
    pendingLocalCursor = null;
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

  function resetNorth(onComplete = null, onCancel = null) {
    if (FlightSimulator.getState().enabled) FlightSimulator.stop(false);
    viewer.camera.flyTo({
      destination: Cesium.Cartesian3.clone(viewer.camera.position),
      orientation: {
        heading: 0.0,
        pitch: viewer.camera.pitch,
        roll: 0.0
      },
      duration: 0.5,
      complete: () => onComplete?.(),
      cancel: () => onCancel?.()
    });
  }

  function resetPitch(onComplete = null, onCancel = null) {
    if (FlightSimulator.getState().enabled) FlightSimulator.stop(false);
    viewer.camera.flyTo({
      destination: Cesium.Cartesian3.clone(viewer.camera.position),
      orientation: {
        heading: viewer.camera.heading,
        pitch: -Cesium.Math.PI_OVER_TWO + 0.0001,
        roll: 0.0
      },
      duration: 0.5,
      complete: () => onComplete?.(),
      cancel: () => onCancel?.()
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
    if (FlightSimulator.getState().enabled) FlightSimulator.stop(false);
    const camera = viewer.camera;
    const canvas = viewer.canvas;
    if (!camera || !canvas) return false;

    const position = requestedPosition || lastMapPointerPosition || new Cesium.Cartesian2(
      canvas.clientWidth / 2,
      canvas.clientHeight / 2);
    const factor = Cesium.Math.clamp(
      Number.isFinite(requestedFactor) ? requestedFactor : zoomFactor,
      0.0000001,
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

  const zoomAnimator = window.ZEarthCameraZoom.createZoomAnimator({
    requestFrame: callback => window.requestAnimationFrame(callback),
    cancelFrame: id => window.cancelAnimationFrame(id),
    step: zoomAtScreenPosition,
    duration: 420
  });
  let zoomRequestId = null;

  function startAnimatedZoom(zoomingIn, position, factor, onComplete, onCancel, onFailure, requestId = null) {
    // Other programmatic flights and repeated zooms must not fight for control.
    if (typeof viewer.camera.cancelFlight === 'function') viewer.camera.cancelFlight();
    zoomRequestId = null;
    zoomAnimator.cancel();
    zoomRequestId = requestId;
    // Freeze the screen target: mouse-move telemetry mutates lastMapPointerPosition.
    const screenAnchor = position ? Cesium.Cartesian2.clone(position) : null;
    return zoomAnimator.start(zoomingIn, screenAnchor, factor,
      () => { zoomRequestId = null; onComplete?.(); },
      () => { zoomRequestId = null; onCancel?.(); },
      error => { zoomRequestId = null; onFailure?.(error); });
  }

  function zoomIn(onComplete, onCancel, onFailure, requestId) {
    return startAnimatedZoom(true, lastMapPointerPosition, zoomFactor, onComplete, onCancel, onFailure, requestId);
  }

  function zoomOut(onComplete, onCancel, onFailure, requestId) {
    return startAnimatedZoom(false, lastMapPointerPosition, zoomFactor, onComplete, onCancel, onFailure, requestId);
  }

  // User drag/wheel interrupts animation immediately instead of fighting it.
  viewer.canvas.addEventListener('pointerdown', () => zoomAnimator.cancel());
  viewer.canvas.addEventListener('wheel', () => zoomAnimator.cancel(), { passive: true });

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
        if (CrsManager.activeProjection) {
          viewer.scene._mapProjection = CrsManager.activeProjection;
        }
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
  document.getElementById('btn-zoom-in')?.addEventListener('click', () => zoomIn());
  document.getElementById('btn-zoom-out')?.addEventListener('click', () => zoomOut());
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
      // Programmatic navigation must supersede optional flight navigation.
      // Otherwise its animation frame immediately overwrites camera.home/north/zoom.
      if (['camera.flyTo', 'camera.home', 'camera.fullExtent',
           'camera.resetNorth', 'camera.resetPitch', 'camera.zoomIn',
           'camera.zoomOut', 'camera.fitSelection', 'scene.setMode']
          .includes(data.action) && FlightSimulator.getState().enabled) {
        FlightSimulator.stop(false);
      }
      if (['camera.flyTo', 'camera.home', 'camera.fullExtent', 'camera.resetNorth',
           'camera.resetPitch', 'camera.fitSelection', 'scene.setMode'].includes(data.action)) {
        zoomAnimator.cancel();
      }
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
          respondAccepted({ started: true });
          resetNorth(
            () => respondSuccess({ heading: 0 }),
            () => respond('cancelled', { headingChanged: false, reason: 'navigation interrupted' }));
          break;
        }
        case 'camera.resetPitch': {
          respondAccepted({ started: true });
          resetPitch(
            () => respondSuccess({ pitch: -90 }),
            () => respond('cancelled', { pitchChanged: false, reason: 'navigation interrupted' }));
          break;
        }
        case 'camera.zoomIn': {
          respondAccepted({ started: true });
          zoomIn(
            () => respondSuccess({ altitude: viewer.camera.positionCartographic.height }),
            () => respond('cancelled', { reason: 'navigation interrupted' }),
            error => respond('failed', null, String(error || 'Zoom unavailable')), reqId);
          break;
        }
        case 'camera.zoomOut': {
          respondAccepted({ started: true });
          zoomOut(
            () => respondSuccess({ altitude: viewer.camera.positionCartographic.height }),
            () => respond('cancelled', { reason: 'navigation interrupted' }),
            error => respond('failed', null, String(error || 'Zoom unavailable')), reqId);
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
        case 'view.setOverviewMap': {
          const visible = Boolean(data.payload?.visible);
          OverviewMapController.setVisible(visible);
          respondSuccess({ visible });
          break;
        }
        case 'scene.setCelestialBody': {
          const body = data.payload?.body || 'earth';
          CelestialBodyManager.setBody(body);
          respondSuccess({ body: CelestialBodyManager.currentBody });
          break;
        }
        case 'layers.setBasemap': {
          const mode = data.payload?.mode || data.payload?.id || 'esri-hybrid';
          if (ImageryManager.setBasemap(mode)) respondSuccess({ basemap: ImageryManager.activeBasemap });
          else respond('failed', null, 'Unknown basemap: ' + mode);
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
        case 'historical.nasaSet': {
          const result = NasaGibsManager.set(data.payload?.layer, data.payload?.date);
          if (result.success) respondSuccess(result);
          else respond('failed', result, result.error || 'NASA overlay was not configured');
          break;
        }
        case 'historical.nasaClear': {
          respondSuccess(NasaGibsManager.clear());
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
        case 'weather.setMetric': {
          if (WeatherStationsManager) {
            WeatherStationsManager.setMetric(data.payload?.metric || 'temperature');
            respondSuccess({ metric: data.payload?.metric });
          } else {
            respond('failed', null, 'WeatherStationsManager not available');
          }
          break;
        }
        case 'locale.set': {
          updateEngineLocale(Boolean(data.payload?.isArabic));
          respondSuccess({ isArabic: EngineLocale.isArabic });
          break;
        }
        case 'gazetteer.setLocale': {
          updateEngineLocale(Boolean(data.payload?.isArabic));
          if (WorldGazetteerManager) {
            WorldGazetteerManager.setLocale(Boolean(data.payload?.isArabic));
            respondSuccess({ isArabic: Boolean(data.payload?.isArabic) });
          } else {
            respondSuccess({ isArabic: Boolean(data.payload?.isArabic) });
          }
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
        case 'saveImage.toggle': {
          const visible = SaveImageManager.toggle();
          respondSuccess({ visible });
          break;
        }
        case 'saveImage.show': {
          SaveImageManager.show();
          respondSuccess({ visible: true });
          break;
        }
        case 'saveImage.hide': {
          SaveImageManager.hide();
          respondSuccess({ visible: false });
          break;
        }
        case 'environment.setClouds': {
          const state = CloudManager.setEnabled(data.payload?.enabled === true);
          respondSuccess(state);
          break;
        }
        case 'environment.setCenterReticle': {
          const enabled = data.payload?.enabled !== false;
          const reticleEl = document.getElementById('center-reticle');
          if (reticleEl) {
            reticleEl.style.display = enabled ? 'block' : 'none';
          }
          respondSuccess({ enabled });
          break;
        }
        case 'flight.setActive': {
          if (data.payload?.enabled === true) GpsPlayback.stop();
          const state = FlightSimulator.setActive(data.payload?.enabled === true, data.payload);
          if (state.error) respond('failed', state, state.error);
          else respondSuccess(state);
          break;
        }
        case 'gps.playTrack': {
          if (FlightSimulator.getState().enabled) FlightSimulator.stop(false);
          const state = GpsPlayback.start(data.payload?.points, data.payload?.durationSeconds ?? 30);
          if (state.error) respond('failed', state, state.error);
          else respondSuccess(state);
          break;
        }
        case 'gps.stopTrack': {
          respondSuccess(GpsPlayback.stop());
          break;
        }
        case 'gps.pauseTrack': {
          respondSuccess(GpsPlayback.setPaused(data.payload?.paused === true));
          break;
        }
        case 'environment.setTimeOfDay': {
          const timeHours = EnvironmentManager.setTimeOfDay(data.payload?.timeHours, data.payload?.dateUtc);
          respondSuccess({ timeHours });
          break;
        }
        case 'models.add': {
          respondAccepted({ loading: true, id: data.payload?.id });
          Model3DManager.add(data.payload).then(result => respondSuccess(result))
            .catch(error => respond('failed', null, String(error?.message || error)));
          break;
        }
        case 'models.set': {
          respondAccepted({ loading: true });
          Model3DManager.set(data.payload).then(result => respondSuccess(result))
            .catch(error => respond('failed', null, String(error?.message || error)));
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
            snapEdges: SnapManager.snapEdges,
            snapIntersections: SnapManager.snapIntersections
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
        case 'profile.showHover': {
          const lon = data.payload?.longitude;
          const lat = data.payload?.latitude;
          ProfileHoverCursor.show(lon, lat);
          respondSuccess({ hovered: true });
          break;
        }
        case 'profile.clearHover': {
          ProfileHoverCursor.clear();
          respondSuccess({ cleared: true });
          break;
        }
        case 'terrain.samplePath': {
          const points = data.payload?.points;
          const provider = viewer.terrainProvider;
          const terrainSource = TerrainManager.currentType;
          if (!Array.isArray(points) || points.length < 2 || points.length > 256 ||
              points.some(p => !Number.isFinite(p?.longitude) || !Number.isFinite(p?.latitude) ||
                p.latitude < -90 || p.latitude > 90 || p.longitude < -180 || p.longitude > 180)) {
            respond('failed', null, 'Terrain profile requires 2–256 valid geographic points.');
            break;
          }
          const isEllipsoid = terrainSource === 'ellipsoid' ||
              provider instanceof Cesium.EllipsoidTerrainProvider;
          const effectiveSource = isEllipsoid ? '30m DEM (AWS Terrarium / SRTM)' : terrainSource;
          const cartographics = points.map(p =>
            Cesium.Cartographic.fromDegrees(p.longitude, p.latitude));
          respondAccepted({ started: true, source: effectiveSource });
          const terrainSampling = async () => {
            if (isEllipsoid) {
              return TerrainManager.sampleBackground30mDem(points);
            }
            if (terrainSource === 'localDem') {
              return TerrainManager.sampleLocalRasterPoints(points);
            }
            // The local Terrarium CustomHeightmap provider has no availability tree,
            // so sampleTerrainMostDetailed cannot always determine a target level.
            if (provider.availability && typeof Cesium.sampleTerrainMostDetailed === 'function') {
              try {
                return await Cesium.sampleTerrainMostDetailed(provider, cartographics);
              } catch (error) {
                console.warn('Falling back to fixed-level terrain sampling:', error);
              }
            }
            return await Cesium.sampleTerrain(provider, 11, cartographics);
          };
          terrainSampling().then(sampled => {
            if (!isEllipsoid && (viewer.terrainProvider !== provider || TerrainManager.currentType !== terrainSource)) {
              respond('failed', null, 'Terrain source changed during the elevation request.');
              return;
            }
            if (!Array.isArray(sampled) || sampled.length !== points.length ||
                sampled.some(p => !Number.isFinite(p.height))) {
              respond('failed', null, 'No complete numeric terrain profile: one or more DEM samples are missing.');
              return;
            }
            respondSuccess({
              source: effectiveSource,
              verticalUnits: 'metres (source terrain elevation datum)',
              samples: sampled.map((p,i) => ({
                longitude: points[i].longitude,
                latitude: points[i].latitude,
                height: p.height
              }))
            });
          }).catch(err => respond('failed', null,
            'Terrain elevation request failed: ' + String(err?.message || err)));
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
          if ((target === 'camera.zoomIn' || target === 'camera.zoomOut') &&
              data.payload?.requestId === zoomRequestId) zoomAnimator.cancel();
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
    CloudManager,
    FlightSimulator,
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
