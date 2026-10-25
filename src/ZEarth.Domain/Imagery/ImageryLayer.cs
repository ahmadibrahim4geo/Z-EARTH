using System;

namespace ZEarth.Domain.Imagery
{
    public enum ImageryProviderType
    {
        NaturalEarthOffline,
        OpenStreetMap,
        OpenTopoMap,
        CartoDbDark,
        CartoDbLight,
        EsriWorldImagery,
        EsriWorldHybrid,
        EsriWorldStreets,
        EsriWorldTopo,
        EsriLightGray,
        XyzTileServer,
        WmsServer,
        WmtsServer,
        ArcGisRest
    }

    public enum BasemapCategory
    {
        All,
        Offline,
        Satellite,
        Hybrid,
        Streets,
        Topographic,
        Cartographic,
        Geology,
        Climate,
        Ocean,
        Historical,
        Custom
    }

    public enum BasemapProviderStatus
    {
        Ready,
        Loading,
        Offline,
        AuthRequired,
        Error
    }

    public class ImageryLayer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public ImageryProviderType ProviderType { get; set; } = ImageryProviderType.NaturalEarthOffline;
        public BasemapCategory Category { get; set; } = BasemapCategory.Offline;
        public string ProviderGroup { get; set; } = "Other";
        public string CategoryName => Category switch
        {
            BasemapCategory.Offline => "Offline",
            BasemapCategory.Satellite => "Satellite / Imagery",
            BasemapCategory.Hybrid => "Satellite + Labels (Hybrid)",
            BasemapCategory.Streets => "Streets / Navigation",
            BasemapCategory.Topographic => "Topographic / Terrain",
            BasemapCategory.Cartographic => "Light / Dark / Cartographic",
            BasemapCategory.Geology => "Geology / Physical Earth",
            BasemapCategory.Climate => "Climate / Earth Observation",
            BasemapCategory.Ocean => "Ocean / Bathymetry",
            BasemapCategory.Historical => "Historical Imagery",
            BasemapCategory.Custom => "Custom Services",
            _ => "General"
        };
        public string UrlTemplate { get; set; } = string.Empty;
        public string Attribution { get; set; } = string.Empty;
        public double Opacity { get; set; } = 1.0;
        public bool IsVisible { get; set; } = true;
        public bool IsBaseLayer { get; set; } = false;
        public int Order { get; set; } = 0;
        public int MinZoom { get; set; } = 0;
        public int MaxZoom { get; set; } = 19;
        public bool IsOfflineAvailable { get; set; } = false;
        public bool AuthRequired { get; set; } = false;
        public BasemapProviderStatus Status { get; set; } = BasemapProviderStatus.Ready;
        public string StatusMessage { get; set; } = "Ready";
        public string ExtentDescription { get; set; } = "Global";

        public ImageryLayer()
        {
        }

        public ImageryLayer(string id, string name, ImageryProviderType providerType, string attribution, bool isBaseLayer = false, bool isOffline = false, bool authRequired = false)
        {
            Id = id;
            Name = name;
            ProviderType = providerType;
            Attribution = attribution;
            IsBaseLayer = isBaseLayer;
            IsOfflineAvailable = isOffline;
            AuthRequired = authRequired;
            Status = authRequired ? BasemapProviderStatus.AuthRequired : (isOffline ? BasemapProviderStatus.Offline : BasemapProviderStatus.Ready);
            StatusMessage = authRequired ? "Account Required" : (isOffline ? "Offline Vector" : "Online");
        }

        public void SetOpacity(double opacity)
        {
            if (opacity < 0.0) Opacity = 0.0;
            else if (opacity > 1.0) Opacity = 1.0;
            else Opacity = opacity;
        }
    }
}
