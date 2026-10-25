using System;
using System.Collections.Generic;
using System.Linq;

namespace ZEarth.Domain.Imagery
{
    public class ImageryRegistry
    {
        public const string ProviderNaturalEarth = "offline-vector";
        public const string ProviderOpenStreetMap = "osm-standard";
        public const string ProviderOsmHumanitarian = "osm-humanitarian";
        public const string ProviderCyclOsm = "osm-cycling";
        public const string ProviderOpenTopoMap = "opentopo";
        public const string ProviderCartoDark = "carto-dark";
        public const string ProviderCartoLight = "carto-light";
        public const string ProviderCartoVoyager = "carto-voyager";
        public const string ProviderEsriImagery = "esri-imagery";
        public const string ProviderEsriHybrid = "esri-hybrid";
        public const string ProviderEsriStreets = "esri-streets";
        public const string ProviderEsriTopo = "esri-topo";
        public const string ProviderEsriGray = "esri-gray";
        public const string ProviderEsriPhysical = "esri-physical";
        public const string ProviderEsriShadedRelief = "esri-shaded-relief";
        public const string ProviderEsriOcean = "esri-ocean";

        private readonly List<ImageryLayer> _registeredLayers = new();

        public static IReadOnlyList<ImageryLayer> Providers => new ImageryRegistry().Basemaps.ToList();

        public IReadOnlyList<ImageryLayer> Layers => _registeredLayers.AsReadOnly();
        public IEnumerable<ImageryLayer> Basemaps => _registeredLayers.Where(l => l.IsBaseLayer);
        public IEnumerable<ImageryLayer> CustomOverlays => _registeredLayers.Where(l => !l.IsBaseLayer);

        public ImageryRegistry()
        {
            RegisterBuiltInProviders();
        }

        private void RegisterBuiltInProviders()
        {
            // 1. Natural Earth Vector (Offline Bundled, zero network)
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderNaturalEarth,
                name: "Natural Earth Vector (Offline)",
                providerType: ImageryProviderType.NaturalEarthOffline,
                attribution: "Natural Earth 1:50m / Public Domain (Offline)",
                isBaseLayer: true,
                isOffline: true
            )
            {
                Category = BasemapCategory.Offline,
                ProviderGroup = "Natural Earth",
                Order = 0,
                IsVisible = true,
                Opacity = 1.0,
                ExtentDescription = "Global (Offline)",
                StatusMessage = "Bundled Offline"
            });

            // 2. OpenStreetMap Standard
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderOpenStreetMap,
                name: "OpenStreetMap Standard",
                providerType: ImageryProviderType.OpenStreetMap,
                attribution: "© OpenStreetMap contributors (ODbL)",
                isBaseLayer: true,
                isOffline: false
            )
            {
                Category = BasemapCategory.Streets,
                ProviderGroup = "OpenStreetMap",
                UrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                Order = 1,
                IsVisible = true,
                Opacity = 1.0,
                ExtentDescription = "Global (Levels 0-19)",
                StatusMessage = "Ready (Online)"
            });

            // 3. OpenStreetMap Humanitarian style
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderOsmHumanitarian,
                name: "OpenStreetMap Humanitarian",
                providerType: ImageryProviderType.XyzTileServer,
                attribution: "© OpenStreetMap contributors • Tiles © HOT/OSM France",
                isBaseLayer: true)
            {
                Category = BasemapCategory.Streets,
                ProviderGroup = "OpenStreetMap",
                UrlTemplate = "https://a.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png",
                Order = 2,
                IsVisible = false,
                MaxZoom = 19,
                ExtentDescription = "Global humanitarian cartography",
                StatusMessage = "Ready (Public tile service; fair-use policy)"
            });

            // 4. CyclOSM cycle-focused cartography
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderCyclOsm,
                name: "CyclOSM Cycling Map",
                providerType: ImageryProviderType.XyzTileServer,
                attribution: "© OpenStreetMap contributors • CyclOSM",
                isBaseLayer: true)
            {
                Category = BasemapCategory.Streets,
                ProviderGroup = "OpenStreetMap",
                UrlTemplate = "https://a.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png",
                Order = 3,
                IsVisible = false,
                MaxZoom = 20,
                ExtentDescription = "Global cycling routes and infrastructure",
                StatusMessage = "Ready (Public tile service; fair-use policy)"
            });

            // 3. OpenTopoMap (Topographic Relief)
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderOpenTopoMap,
                name: "OpenTopoMap (Topographic Relief)",
                providerType: ImageryProviderType.OpenTopoMap,
                attribution: "Map data: © OpenStreetMap contributors, SRTM | Map style: © OpenTopoMap (CC-BY-SA)",
                isBaseLayer: true,
                isOffline: false
            )
            {
                Category = BasemapCategory.Topographic,
                ProviderGroup = "OpenStreetMap",
                UrlTemplate = "https://tile.opentopomap.org/{z}/{x}/{y}.png",
                Order = 2,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global (Levels 0-17)",
                StatusMessage = "Ready (Online)"
            });

            // 4. CartoDB Dark Matter
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderCartoDark,
                name: "CartoDB Dark Matter",
                providerType: ImageryProviderType.CartoDbDark,
                attribution: "© OpenStreetMap contributors, © CARTO",
                isBaseLayer: true,
                isOffline: false
            )
            {
                Category = BasemapCategory.Cartographic,
                ProviderGroup = "CARTO",
                UrlTemplate = "https://basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png",
                Order = 3,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global (Levels 0-19)",
                StatusMessage = "Ready (Online)"
            });

            // 5. CartoDB Positron (Light)
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderCartoLight,
                name: "CartoDB Positron (Light)",
                providerType: ImageryProviderType.CartoDbLight,
                attribution: "© OpenStreetMap contributors, © CARTO",
                isBaseLayer: true,
                isOffline: false
            )
            {
                Category = BasemapCategory.Cartographic,
                ProviderGroup = "CARTO",
                UrlTemplate = "https://basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png",
                Order = 4,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global (Levels 0-19)",
                StatusMessage = "Ready (Online)"
            });

            _registeredLayers.Add(new ImageryLayer(
                id: ProviderCartoVoyager,
                name: "CARTO Voyager",
                providerType: ImageryProviderType.XyzTileServer,
                attribution: "© OpenStreetMap contributors • © CARTO",
                isBaseLayer: true)
            {
                Category = BasemapCategory.Cartographic,
                ProviderGroup = "CARTO",
                UrlTemplate = "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                Order = 6,
                IsVisible = false,
                ExtentDescription = "Global modern cartography",
                StatusMessage = "Ready (Online)"
            });

            // 6. Esri World Imagery (Satellite, no labels)
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriImagery,
                name: "Esri World Imagery (No Labels)",
                providerType: ImageryProviderType.EsriWorldImagery,
                attribution: "Source: Esri, Maxar, Earthstar Geographics, and GIS User Community",
                isBaseLayer: true,
                isOffline: false,
                authRequired: false
            )
            {
                Category = BasemapCategory.Satellite,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
                Order = 5,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global High-Res Satellite",
                StatusMessage = "Ready (ArcGIS Services)"
            });

            // 7. Esri World Hybrid (Satellite + Boundaries/Places Overlay)
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriHybrid,
                name: "Esri World Imagery (Hybrid Labels)",
                providerType: ImageryProviderType.EsriWorldHybrid,
                attribution: "Source: Esri, Maxar, Earthstar Geographics, CNES/Airbus DS, USDA, USGS, AeroGRID, IGN, and GIS User Community",
                isBaseLayer: true,
                isOffline: false,
                authRequired: false
            )
            {
                Category = BasemapCategory.Hybrid,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
                Order = 6,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global Satellite with Reference Overlay",
                StatusMessage = "Ready (Imagery + Overlay)"
            });

            // 8. Esri World Street Map
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriStreets,
                name: "Esri World Street Map",
                providerType: ImageryProviderType.EsriWorldStreets,
                attribution: "Source: Esri, DeLorme, NAVTEQ, USGS, Intermap, iPC, NRCAN, Esri Japan, METI, Esri China (Hong Kong), Esri (Thailand), TomTom",
                isBaseLayer: true,
                isOffline: false,
                authRequired: false
            )
            {
                Category = BasemapCategory.Streets,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer",
                Order = 7,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global Street Network",
                StatusMessage = "Ready (ArcGIS Streets)"
            });

            // 9. Esri World Topographic Map
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriTopo,
                name: "Esri World Topographic Map",
                providerType: ImageryProviderType.EsriWorldTopo,
                attribution: "Source: Esri, DeLorme, HERE, TomTom, Intermap, increment P Corp., GEBCO, USGS, FAO, NPS, NRCAN, GeoBase, IGN, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), swisstopo, MapmyIndia",
                isBaseLayer: true,
                isOffline: false,
                authRequired: false
            )
            {
                Category = BasemapCategory.Topographic,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer",
                Order = 8,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global Topography & Contours",
                StatusMessage = "Ready (ArcGIS Topo)"
            });

            // 10. Esri Light Gray Canvas
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriGray,
                name: "Esri Light Gray Canvas",
                providerType: ImageryProviderType.EsriLightGray,
                attribution: "Source: Esri, HERE, DeLorme, MapmyIndia, © OpenStreetMap contributors, and GIS user community",
                isBaseLayer: true,
                isOffline: false,
                authRequired: false
            )
            {
                Category = BasemapCategory.Cartographic,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer",
                Order = 9,
                IsVisible = false,
                Opacity = 1.0,
                ExtentDescription = "Global Minimalist Gray",
                StatusMessage = "Ready (Light Gray)"
            });

            // Physical/geology, shaded relief, and ocean products belong in
            // the Basemap gallery rather than the Reference Layers tree.
            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriPhysical,
                name: "Esri World Physical Map",
                providerType: ImageryProviderType.ArcGisRest,
                attribution: "Source: Esri, US National Park Service",
                isBaseLayer: true
            )
            {
                Category = BasemapCategory.Geology,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Physical_Map/MapServer",
                Order = 14,
                IsVisible = false,
                ExtentDescription = "Global physical geography and landforms",
                StatusMessage = "Ready (ArcGIS Services)"
            });

            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriShadedRelief,
                name: "Esri World Shaded Relief",
                providerType: ImageryProviderType.ArcGisRest,
                attribution: "Source: Esri, USGS",
                isBaseLayer: true)
            {
                Category = BasemapCategory.Geology,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Shaded_Relief/MapServer",
                Order = 15,
                IsVisible = false,
                ExtentDescription = "Global shaded terrain relief",
                StatusMessage = "Ready (ArcGIS Services)"
            });

            _registeredLayers.Add(new ImageryLayer(
                id: ProviderEsriOcean,
                name: "Esri Ocean & Bathymetry",
                providerType: ImageryProviderType.ArcGisRest,
                attribution: "Source: Esri, GEBCO, NOAA, National Geographic",
                isBaseLayer: true)
            {
                Category = BasemapCategory.Ocean,
                ProviderGroup = "Esri",
                UrlTemplate = "https://services.arcgisonline.com/ArcGIS/rest/services/Ocean/World_Ocean_Base/MapServer",
                Order = 16,
                IsVisible = false,
                ExtentDescription = "Global ocean basemap and bathymetry",
                StatusMessage = "Ready (ArcGIS Services)"
            });
        }

        public ImageryLayer? GetLayer(string id)
        {
            return _registeredLayers.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public void SetBasemap(string id)
        {
            foreach (var l in _registeredLayers.Where(x => x.IsBaseLayer))
            {
                l.IsVisible = string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool AddCustomLayer(ImageryLayer layer)
        {
            if (string.IsNullOrWhiteSpace(layer.Id) || string.IsNullOrWhiteSpace(layer.Name))
                return false;

            if (_registeredLayers.Any(l => string.Equals(l.Id, layer.Id, StringComparison.OrdinalIgnoreCase)))
                return false;

            layer.Order = _registeredLayers.Count;
            _registeredLayers.Add(layer);
            return true;
        }

        public bool RemoveLayer(string id)
        {
            var layer = GetLayer(id);
            if (layer == null || layer.IsBaseLayer) // Cannot remove default base layers
                return false;

            _registeredLayers.Remove(layer);
            ReindexOrders();
            return true;
        }

        public bool MoveUp(string id)
        {
            int index = _registeredLayers.FindIndex(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index <= 0) return false;

            var item = _registeredLayers[index];
            _registeredLayers.RemoveAt(index);
            _registeredLayers.Insert(index - 1, item);
            ReindexOrders();
            return true;
        }

        public bool MoveDown(string id)
        {
            int index = _registeredLayers.FindIndex(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= _registeredLayers.Count - 1) return false;

            var item = _registeredLayers[index];
            _registeredLayers.RemoveAt(index);
            _registeredLayers.Insert(index + 1, item);
            ReindexOrders();
            return true;
        }

        private void ReindexOrders()
        {
            for (int i = 0; i < _registeredLayers.Count; i++)
            {
                _registeredLayers[i].Order = i;
            }
        }

        public string GetCombinedAttribution()
        {
            var visibleAttributions = _registeredLayers
                .Where(l => l.IsVisible && !string.IsNullOrWhiteSpace(l.Attribution))
                .Select(l => l.Attribution)
                .Distinct()
                .ToList();

            if (visibleAttributions.Count == 0)
                return "Z EARTH Desktop GIS | Ahmad Ibrahim";

            return string.Join(" • ", visibleAttributions);
        }

        public static bool ValidateUrlTemplate(string url, ImageryProviderType providerType)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            if (providerType == ImageryProviderType.XyzTileServer ||
                providerType == ImageryProviderType.OpenStreetMap ||
                providerType == ImageryProviderType.OpenTopoMap ||
                providerType == ImageryProviderType.CartoDbDark ||
                providerType == ImageryProviderType.CartoDbLight)
            {
                // Must contain {z}, {x}, and {y} tokens
                return url.Contains("{z}", StringComparison.OrdinalIgnoreCase) &&
                       url.Contains("{x}", StringComparison.OrdinalIgnoreCase) &&
                       url.Contains("{y}", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
    }
}
