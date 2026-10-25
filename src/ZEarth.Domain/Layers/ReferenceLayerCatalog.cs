using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ZEarth.Domain.Layers
{
    public enum ReferenceLayerCategory
    {
        BordersAndLabels,
        PlacesAndNames,
        RoadsAndTransportation,
        Weather,
        GalleryAndKnowledge,
        WaterAndHydrology,
        ParksAndRecreation,
        BuildingsAndInfrastructure,
        NaturalHazards
    }

    public enum ReferenceLayerStatus
    {
        Active,
        OfflineBundled,
        NotConfigured,
        ProviderUnavailable
    }

    public class ReferenceLayerItem : INotifyPropertyChanged
    {
        private bool _isVisible;
        private double _opacity = 1.0;
        private string _statusMessage = string.Empty;
        private ReferenceLayerStatus _status = ReferenceLayerStatus.Active;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("categoryId")]
        public ReferenceLayerCategory Category { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("attribution")]
        public string Attribution { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("layerType")]
        public string LayerType { get; set; } = "RasterTile"; // RasterTile, Wmts, Wms, VectorGeoJson

        [JsonPropertyName("wmtsLayer")]
        public string? WmtsLayer { get; set; }

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("isBuiltIn")]
        public bool IsBuiltIn { get; set; } = true;

        [JsonPropertyName("isConfigured")]
        public bool IsConfigured { get; set; } = true;

        [JsonPropertyName("dateOrTimestamp")]
        public string? DateOrTimestamp { get; set; }

        [JsonPropertyName("boundsDescription")]
        public string BoundsDescription { get; set; } = "Global";

        [JsonPropertyName("isVisible")]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("opacity")]
        public double Opacity
        {
            get => _opacity;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_opacity - clamped) > 0.001)
                {
                    _opacity = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpacityPercentDisplay));
                }
            }
        }

        [JsonPropertyName("status")]
        public ReferenceLayerStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(IsStatusActive));
                }
            }
        }

        [JsonPropertyName("statusMessage")]
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OpacityPercentDisplay => $"{Math.Round(_opacity * 100)}%";
        public bool IsStatusActive => Status == ReferenceLayerStatus.Active || Status == ReferenceLayerStatus.OfflineBundled;
        public string StatusBadgeText => Status switch
        {
            ReferenceLayerStatus.Active => "● Active",
            ReferenceLayerStatus.OfflineBundled => "● Offline",
            ReferenceLayerStatus.NotConfigured => "○ Not configured",
            ReferenceLayerStatus.ProviderUnavailable => "▲ Unavailable",
            _ => "○ Inactive"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public ReferenceLayerItem Clone()
        {
            return new ReferenceLayerItem
            {
                Id = Id,
                Category = Category,
                CategoryName = CategoryName,
                Name = Name,
                Description = Description,
                Provider = Provider,
                Attribution = Attribution,
                Url = Url,
                LayerType = LayerType,
                WmtsLayer = WmtsLayer,
                Query = Query,
                IsBuiltIn = IsBuiltIn,
                IsConfigured = IsConfigured,
                DateOrTimestamp = DateOrTimestamp,
                BoundsDescription = BoundsDescription,
                IsVisible = IsVisible,
                Opacity = Opacity,
                Status = Status,
                StatusMessage = StatusMessage
            };
        }
    }

    public class ReferenceLayerCategoryGroup : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public ReferenceLayerCategory Category { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
        public ObservableCollection<ReferenceLayerItem> Layers { get; set; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>
    /// Authoritative catalog of built-in reference layer definitions (SIDEBAR-01 Scope, Section 4).
    /// Always contains the 9 mandatory categories. Built-in entries cannot be deleted.
    /// </summary>
    public class ReferenceLayerCatalog
    {
        private readonly List<ReferenceLayerItem> _layers = new();
        public IReadOnlyList<ReferenceLayerItem> Layers => _layers.AsReadOnly();

        public ReferenceLayerCatalog()
        {
            InitializeBuiltInCatalog();
        }

        private void InitializeBuiltInCatalog()
        {
            // Category 1: Borders & Labels
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_borders_coastlines",
                Category = ReferenceLayerCategory.BordersAndLabels,
                CategoryName = "Borders & Labels",
                Name = "Country Borders & Coastlines",
                Description = "National international boundaries, maritime coastlines and sovereign limits.",
                Provider = "Natural Earth 1:50m / Esri Reference Boundaries",
                Attribution = "Natural Earth (Public Domain) • Esri",
                Url = "https://services.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer",
                LayerType = "ArcGisMapServer",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Configured (availability checked at runtime)"
            });

            // Category 2: Places & Geographic Names
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_places_names",
                Category = ReferenceLayerCategory.PlacesAndNames,
                CategoryName = "Places & Geographic Names",
                Name = "Populated Places & Capitals",
                Description = "World capitals, major cities, settlements, and geographic place names.",
                Provider = "Carto Voyager Places & Geographic Names",
                Attribution = "© OpenStreetMap contributors • © CARTO",
                Url = "https://basemaps.cartocdn.com/rastertiles/voyager_only_labels/{z}/{x}/{y}.png",
                LayerType = "RasterTile",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (Carto Voyager Online)"
            });

            // Category 3: Roads & Transportation
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_roads_transportation",
                Category = ReferenceLayerCategory.RoadsAndTransportation,
                CategoryName = "Roads & Transportation",
                Name = "Major Highways, Roads & Rails",
                Description = "Global highways, arterial roadways, bridges, and passenger rail corridors.",
                Provider = "Esri World Transportation Reference Overlay",
                Attribution = "Source: Esri, DeLorme, NAVTEQ, TomTom, USGS",
                Url = "https://services.arcgisonline.com/ArcGIS/rest/services/Reference/World_Transportation/MapServer/tile/{z}/{y}/{x}",
                LayerType = "RasterTile",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (Esri Transportation Online)"
            });

            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_railways",
                Category = ReferenceLayerCategory.RoadsAndTransportation,
                CategoryName = "Roads & Transportation",
                Name = "Global Railways",
                Description = "Worldwide railway infrastructure overlay including main lines, stations and rail facilities.",
                Provider = "OpenRailwayMap / OpenStreetMap",
                Attribution = "© OpenStreetMap contributors • OpenRailwayMap",
                Url = "https://a.tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png",
                LayerType = "RasterTile",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (global railway overlay)"
            });

            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_hiking_trails",
                Category = ReferenceLayerCategory.RoadsAndTransportation,
                CategoryName = "Roads & Transportation",
                Name = "Hiking & Walking Trails",
                Description = "Global waymarked hiking routes and recreational trails.",
                Provider = "Waymarked Trails / OpenStreetMap",
                Attribution = "© OpenStreetMap contributors • Waymarked Trails",
                Url = "https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png",
                LayerType = "RasterTile",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (global trail overlay)"
            });

            // Category 4: Weather & Climate
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_weather_climate",
                Category = ReferenceLayerCategory.Weather,
                CategoryName = "Weather",
                Name = "NASA Daily Clouds & True Color",
                Description = "Daily global MODIS Terra corrected-reflectance imagery for cloud and weather-system context.",
                Provider = "NASA GIBS (EOSDIS) MODIS Terra",
                Attribution = "NASA Earth Observing System Data and Information System (EOSDIS)",
                Url = "https://gibs.earthdata.nasa.gov/wmts/epsg4326/best/MODIS_Terra_CorrectedReflectance_TrueColor/default/{Time}/250m/{TileMatrix}/{TileRow}/{TileCol}.jpg",
                LayerType = "WmtsGibs",
                WmtsLayer = "MODIS_Terra_CorrectedReflectance_TrueColor",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Configured Online (NASA GIBS global daily imagery)"
            });

            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_weather_physical",
                Category = ReferenceLayerCategory.Weather,
                CategoryName = "Weather",
                Name = "NASA Aerosol Optical Depth",
                Description = "Daily global aerosol conditions from MODIS Terra, served by NASA GIBS.",
                Provider = "NASA GIBS (EOSDIS) MODIS Terra",
                Attribution = "NASA EOSDIS GIBS",
                Url = "https://gibs.earthdata.nasa.gov/wmts/epsg4326/best/MODIS_Terra_Aerosol/default/{Time}/2km/{TileMatrix}/{TileRow}/{TileCol}.png",
                LayerType = "WmtsGibs",
                WmtsLayer = "MODIS_Terra_Aerosol",
                DateOrTimestamp = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (NASA GIBS global daily layer)"
            });

            // Category 5: Gallery & Knowledge
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_geology_relief",
                Category = ReferenceLayerCategory.GalleryAndKnowledge,
                CategoryName = "Gallery & Knowledge",
                Name = "Wikimedia Commons Nearby Photos",
                Description = "Geotagged, openly licensed photographs around the current map center.",
                Provider = "Wikimedia Commons GeoData API",
                Attribution = "Wikimedia Commons contributors; individual file licenses apply",
                Url = "https://commons.wikimedia.org/w/api.php",
                LayerType = "WikimediaGeoSearch",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (loads around current view)"
            });

            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_wikipedia_places",
                Category = ReferenceLayerCategory.GalleryAndKnowledge,
                CategoryName = "Gallery & Knowledge",
                Name = "Wikipedia Nearby Articles",
                Description = "Georeferenced Wikipedia articles around the current map center.",
                Provider = "Wikipedia GeoData API",
                Attribution = "Wikipedia contributors (CC BY-SA)",
                Url = "https://en.wikipedia.org/w/api.php",
                LayerType = "WikipediaGeoSearch",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (loads around current view)"
            });

            // Category 6: Water & Hydrology
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_water_hydrology",
                Category = ReferenceLayerCategory.WaterAndHydrology,
                CategoryName = "Water & Hydrology",
                Name = "Ocean Reference & Marine Bathymetry",
                Description = "Major river systems, freshwater lakes, reservoirs, undersea trenches and bathymetry.",
                Provider = "Esri World Ocean Reference Overlay",
                Attribution = "Source: Esri, GEBCO, NOAA, National Geographic",
                Url = "https://services.arcgisonline.com/ArcGIS/rest/services/Ocean/World_Ocean_Reference/MapServer/tile/{z}/{y}/{x}",
                LayerType = "RasterTile",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (Esri Ocean Reference Online)"
            });

            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_water_basins",
                Category = ReferenceLayerCategory.WaterAndHydrology,
                CategoryName = "Water & Hydrology",
                Name = "Water Bodies & Waterways (Current View)",
                Description = "OpenStreetMap rivers, canals, lakes and reservoirs queried for the current regional view.",
                Provider = "OpenStreetMap Overpass API",
                Attribution = "© OpenStreetMap contributors (ODbL)",
                Url = "https://overpass-api.de/api/interpreter",
                LayerType = "OverpassGeoJson",
                Query = "[out:json][timeout:25];(way[natural=water]({bbox});relation[natural=water]({bbox});way[waterway]({bbox});relation[waterway]({bbox}););out geom 4000;",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (zoom to a regional view before enabling)"
            });

            // Category 7: Parks & Recreation
            _layers.Add(new ReferenceLayerItem
            {
                // Historical ID retained for saved-project compatibility. This record is OSM
                // parks via Overpass, *not* the NASA GIBS observation layer.
                Id = "ref_earth_observation_gibs",
                Category = ReferenceLayerCategory.ParksAndRecreation,
                CategoryName = "Parks & Recreation",
                Name = "Parks & Protected Areas (Current View)",
                Description = "OpenStreetMap parks, nature reserves, protected areas and national parks in the current view.",
                Provider = "OpenStreetMap Overpass API",
                Attribution = "© OpenStreetMap contributors (ODbL)",
                Url = "https://overpass-api.de/api/interpreter",
                LayerType = "OverpassGeoJson",
                Query = "[out:json][timeout:25];(way[leisure=park]({bbox});relation[leisure=park]({bbox});way[boundary=national_park]({bbox});relation[boundary=national_park]({bbox});way[boundary=protected_area]({bbox});relation[boundary=protected_area]({bbox}););out geom 3000;",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (zoom to a regional view before enabling)"
            });

            // Category 8: Buildings & Infrastructure
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_buildings_infrastructure",
                Category = ReferenceLayerCategory.BuildingsAndInfrastructure,
                CategoryName = "Buildings & Infrastructure",
                Name = "Building Footprints (Current View)",
                Description = "OpenStreetMap building footprints queried for the current regional view.",
                Provider = "OpenStreetMap Overpass API",
                Attribution = "© OpenStreetMap contributors (ODbL)",
                Url = "https://overpass-api.de/api/interpreter",
                LayerType = "OverpassGeoJson",
                Query = "[out:json][timeout:25];(way[building]({bbox});relation[building]({bbox}););out geom 5000;",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (zoom to a city before enabling)"
            });

            // Category 9: Natural Hazards
            _layers.Add(new ReferenceLayerItem
            {
                Id = "ref_natural_hazards",
                Category = ReferenceLayerCategory.NaturalHazards,
                CategoryName = "Natural Hazards",
                Name = "Real-Time Earthquakes (USGS Worldwide M2.5+)",
                Description = "Global real-time seismic events, earthquake epicenters, depth and magnitude recorded within the past 24 hours.",
                Provider = "USGS Earthquake Hazards Program",
                Attribution = "USGS Earthquake Hazards Program (Public Domain)",
                Url = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson",
                LayerType = "GeoJson",
                IsBuiltIn = true,
                IsConfigured = true,
                IsVisible = false,
                Status = ReferenceLayerStatus.Active,
                StatusMessage = "Ready (USGS Real-Time Seismic Feed)"
            });

        }

        public List<ReferenceLayerCategoryGroup> GetGroupedCategories()
        {
            var iconMap = new Dictionary<ReferenceLayerCategory, string>
            {
                { ReferenceLayerCategory.BordersAndLabels, "🌐" },
                { ReferenceLayerCategory.PlacesAndNames, "🏙" },
                { ReferenceLayerCategory.RoadsAndTransportation, "🛣" },
                { ReferenceLayerCategory.Weather, "☁" },
                { ReferenceLayerCategory.GalleryAndKnowledge, "📷" },
                { ReferenceLayerCategory.WaterAndHydrology, "💧" },
                { ReferenceLayerCategory.ParksAndRecreation, "🌳" },
                { ReferenceLayerCategory.BuildingsAndInfrastructure, "🏢" },
                { ReferenceLayerCategory.NaturalHazards, "🌋" }
            };

            var groups = new List<ReferenceLayerCategoryGroup>();
            foreach (ReferenceLayerCategory cat in Enum.GetValues(typeof(ReferenceLayerCategory)))
            {
                var catLayers = _layers.Where(l => l.Category == cat).ToList();
                var catName = catLayers.FirstOrDefault()?.CategoryName ?? cat.ToString();
                var group = new ReferenceLayerCategoryGroup
                {
                    Category = cat,
                    CategoryName = catName,
                    Icon = iconMap.TryGetValue(cat, out var ic) ? ic : "📁",
                    IsExpanded = true
                };
                foreach (var layer in catLayers)
                {
                    group.Layers.Add(layer);
                }
                groups.Add(group);
            }
            return groups;
        }

        public ReferenceLayerItem? FindById(string id)
        {
            return _layers.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
