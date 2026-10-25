using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ZEarth.Domain
{
    public enum PlaceItemType
    {
        Folder,
        Placemark,
        Path,
        Polygon,
        GroundOverlay,
        Circle,
        SavedScene
    }

    public class CoordinatePoint
    {
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }

        public CoordinatePoint() { }

        public CoordinatePoint(double lon, double lat, double alt = 0.0)
        {
            Longitude = lon;
            Latitude = lat;
            Altitude = alt;
        }

        public CoordinatePoint Clone() => new CoordinatePoint(Longitude, Latitude, Altitude);

        public override string ToString() => $"{Longitude:F6},{Latitude:F6},{Altitude:F1}";
    }

    public class PlaceItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "New Feature";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("itemType")]
        public PlaceItemType ItemType { get; set; } = PlaceItemType.Placemark;

        [JsonPropertyName("isVisible")]
        public bool IsVisible { get; set; } = true;

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double? Altitude { get; set; }

        [JsonPropertyName("radiusMeters")]
        public double? RadiusMeters { get; set; }

        [JsonPropertyName("colorHex")]
        public string ColorHex { get; set; } = "#00c8ff";

        [JsonIgnore]
        public string Glyph => IsStartupScene ? "★" : ItemType switch
        {
            PlaceItemType.Folder => "📁",
            PlaceItemType.SavedScene => "🎬",
            PlaceItemType.Placemark => "●",
            PlaceItemType.Path => "〰",
            PlaceItemType.Polygon => "⬡",
            PlaceItemType.Circle => "◯",
            PlaceItemType.GroundOverlay => "🖼",
            _ => "●"
        };

        [JsonIgnore]
        public string EffectiveColorHex => IsStartupScene ? "#F59E0B" :
            (ItemType == PlaceItemType.Folder ? "#EAB308" :
             ItemType == PlaceItemType.SavedScene ? "#A855F7" :
             (string.IsNullOrWhiteSpace(ColorHex) ? "#22A8DE" : ColorHex));

        [JsonPropertyName("isExpanded")]
        public bool IsExpanded { get; set; } = true;

        [JsonPropertyName("isProtected")]
        public bool IsProtected { get; set; } = false;

        [JsonPropertyName("isStartupScene")]
        public bool IsStartupScene { get; set; } = false;

        [JsonPropertyName("camera")]
        public CameraState? Camera { get; set; }

        [JsonPropertyName("coordinates")]
        public List<CoordinatePoint> Coordinates { get; set; } = new List<CoordinatePoint>();

        [JsonPropertyName("innerRings")]
        public List<List<CoordinatePoint>> InnerRings { get; set; } = new List<List<CoordinatePoint>>();

        [JsonPropertyName("extendedData")]
        public Dictionary<string, string> ExtendedData { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("styleUrl")]
        public string? StyleUrl { get; set; }

        [JsonPropertyName("timeSpanBegin")]
        public string? TimeSpanBegin { get; set; }

        [JsonPropertyName("timeSpanEnd")]
        public string? TimeSpanEnd { get; set; }

        [JsonPropertyName("preservedXmlFragments")]
        public List<string> PreservedXmlFragments { get; set; } = new List<string>();

        [JsonPropertyName("children")]
        public ObservableCollection<PlaceItem> Children { get; set; } = new ObservableCollection<PlaceItem>();

        public PlaceItem() { }

        public PlaceItem(string name, PlaceItemType itemType, double? lat = null, double? lon = null, double? alt = null)
        {
            Name = name;
            ItemType = itemType;
            Latitude = lat;
            Longitude = lon;
            Altitude = alt;
        }

        public PlaceItem(string name, CameraState camera, double? groundLat = null, double? groundLon = null, double? groundAlt = null)
        {
            Name = name;
            ItemType = PlaceItemType.SavedScene;
            Latitude = groundLat;
            Longitude = groundLon;
            Altitude = groundAlt;
            Camera = camera.Clone();
        }

        public void AddChild(PlaceItem item)
        {
            Children.Add(item);
        }

        public PlaceItem Clone()
        {
            var clone = new PlaceItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Name,
                Description = Description,
                ItemType = ItemType,
                IsVisible = IsVisible,
                Latitude = Latitude,
                Longitude = Longitude,
                Altitude = Altitude,
                RadiusMeters = RadiusMeters,
                ColorHex = ColorHex,
                IsExpanded = IsExpanded,
                IsProtected = IsProtected,
                IsStartupScene = IsStartupScene,
                Camera = Camera?.Clone(),
                StyleUrl = StyleUrl,
                TimeSpanBegin = TimeSpanBegin,
                TimeSpanEnd = TimeSpanEnd
            };

            foreach (var pt in Coordinates)
            {
                clone.Coordinates.Add(pt.Clone());
            }

            foreach (var ring in InnerRings)
            {
                var ringClone = new List<CoordinatePoint>();
                foreach (var pt in ring)
                {
                    ringClone.Add(pt.Clone());
                }
                clone.InnerRings.Add(ringClone);
            }

            foreach (var kvp in ExtendedData)
            {
                clone.ExtendedData[kvp.Key] = kvp.Value;
            }

            foreach (var xml in PreservedXmlFragments)
            {
                clone.PreservedXmlFragments.Add(xml);
            }

            foreach (var child in Children)
            {
                clone.AddChild(child.Clone());
            }
            return clone;
        }
    }
}
