using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Layers;

namespace ZEarth.Domain.Search
{
    public class SearchResultItem
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double Altitude { get; set; } = 1500.0;
        public string Source { get; set; } = "Local";
        public string SourceBadge { get; set; } = "Local";
        public string Icon { get; set; } = "📍";
        public bool IsLocal { get; set; } = true;
        public PlaceItem? AssociatedPlace { get; set; }

        public string CoordinateDisplay => $"{Latitude:F5}°, {Longitude:F5}°";
    }

    public class CoordinateValidationException : Exception
    {
        public CoordinateValidationException(string message) : base(message) { }
    }

    public class SearchService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        static SearchService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZEarth-Desktop-GIS/2.0 (Windows desktop application)");
        }

        public string? LastOnlineError { get; private set; }

        /// <summary>
        /// Searches places by name: local My Places + Temporary Places first, then Photon online geocoder with fallback.
        /// Fully handles Unicode/Arabic queries such as 'القاهرة' or 'Cairo'.
        /// </summary>
        public async Task<List<SearchResultItem>> SearchByNameAsync(
            string query,
            IEnumerable<PlaceItem> localPlaces,
            IEnumerable<ReferenceLayerItem>? referenceLayers = null,
            CancellationToken ct = default)
        {
            var results = new List<SearchResultItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;
            LastOnlineError = null;

            string trimmedQuery = query.Trim();

            // 1. Search local user places (My Places & Temporary Places)
            void SearchPlaceRecursive(PlaceItem item)
            {
                if (item.Name.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(item.Description) && item.Description.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    double lon = item.Longitude ?? (item.Coordinates.Count > 0 ? item.Coordinates[0].Longitude : 0.0);
                    double lat = item.Latitude ?? (item.Coordinates.Count > 0 ? item.Coordinates[0].Latitude : 0.0);
                    double alt = item.Altitude ?? (item.Coordinates.Count > 0 ? item.Coordinates[0].Altitude : 2000.0);

                    results.Add(new SearchResultItem
                    {
                        Title = item.Name,
                        Subtitle = $"Local Place ({item.ItemType}) • {item.Description}",
                        Longitude = lon,
                        Latitude = lat,
                        Altitude = Math.Max(500.0, alt),
                        Source = "Local My Places",
                        SourceBadge = "Local",
                        Icon = item.ItemType == PlaceItemType.Folder ? "📁" : "📍",
                        IsLocal = true,
                        AssociatedPlace = item
                    });
                }

                foreach (var child in item.Children)
                {
                    SearchPlaceRecursive(child);
                }
            }

            foreach (var place in localPlaces)
            {
                SearchPlaceRecursive(place);
            }

            // Reference layers are intentionally not returned as geographic places. A catalog
            // item has no single coordinate/extent and the old implementation incorrectly sent
            // every matching layer to Cairo.

            // 2. Online Geocoder query (Photon by Komoot)
            if (trimmedQuery.Length >= 2 && !ct.IsCancellationRequested)
            {
                try
                {
                    string encoded = Uri.EscapeDataString(trimmedQuery);
                    string url = $"https://photon.komoot.io/api/?q={encoded}&limit=5";

                    using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        var features = doc.RootElement.GetProperty("features");

                        foreach (var feat in features.EnumerateArray())
                        {
                            var geom = feat.GetProperty("geometry");
                            var coords = geom.GetProperty("coordinates");
                            double lon = coords[0].GetDouble();
                            double lat = coords[1].GetDouble();

                            var props = feat.GetProperty("properties");
                            string name = props.TryGetProperty("name", out var np) ? np.GetString() ?? trimmedQuery : trimmedQuery;

                            var subParts = new List<string>();
                            if (props.TryGetProperty("city", out var cp) && !string.IsNullOrEmpty(cp.GetString())) subParts.Add(cp.GetString()!);
                            if (props.TryGetProperty("state", out var sp) && !string.IsNullOrEmpty(sp.GetString())) subParts.Add(sp.GetString()!);
                            if (props.TryGetProperty("country", out var countP) && !string.IsNullOrEmpty(countP.GetString())) subParts.Add(countP.GetString()!);

                            string subtitle = subParts.Count > 0 ? string.Join(", ", subParts) : $"Online match ({lat:F4}°, {lon:F4}°)";

                            results.Add(new SearchResultItem
                            {
                                Title = name,
                                Subtitle = $"{subtitle} • Photon Online",
                                Longitude = lon,
                                Latitude = lat,
                                Altitude = 15000.0,
                                Source = "Photon Online Geocoder",
                                SourceBadge = "Online",
                                Icon = "🌍",
                                IsLocal = false
                            });
                        }
                    }
                    else
                    {
                        LastOnlineError = $"Online geocoder returned HTTP {(int)response.StatusCode}.";
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LastOnlineError = ex is TaskCanceledException
                        ? "Online geocoder timed out. Local results are still available."
                        : $"Online geocoder unavailable: {ex.Message}";
                }
            }

            return results;
        }

        /// <summary>
        /// Searches by coordinates with X strictly above Y in the UI layout.
        /// Handles geographic WGS84 (Lon/Lat) in DD or DMS, and projected coordinates (Easting/Northing) via PROJ.
        /// </summary>
        public SearchResultItem SearchByCoordinates(string xText, string yText, int epsgCode = 4326)
        {
            if (string.IsNullOrWhiteSpace(xText))
                throw new CoordinateValidationException("X coordinate field cannot be empty.");
            if (string.IsNullOrWhiteSpace(yText))
                throw new CoordinateValidationException("Y coordinate field cannot be empty.");

            double x = ParseCoordinateValue(xText, isLongitudeOrEasting: true);
            double y = ParseCoordinateValue(yText, isLongitudeOrEasting: false);

            // Range validation per CRS
            if (epsgCode == 4326 || epsgCode == 4979)
            {
                // Geographic: X = Longitude [-180, 180], Y = Latitude [-90, 90]
                if (x < -180.0 || x > 180.0)
                {
                    if (y >= -180.0 && y <= 180.0 && x >= -90.0 && x <= 90.0)
                    {
                        throw new CoordinateValidationException(
                            $"X (Longitude) value {x:F4}° is out of valid range [-180°, +180°].\n" +
                            $"Notice: Y value {y:F4}° is within longitude bounds while X is within latitude bounds. Did you swap X (Longitude) and Y (Latitude)?");
                    }
                    throw new CoordinateValidationException($"X (Longitude) must be between -180.0° and +180.0°. Received: {x}");
                }

                if (y < -90.0 || y > 90.0)
                {
                    throw new CoordinateValidationException($"Y (Latitude) must be between -90.0° and +90.0°. Received: {y}");
                }

                return new SearchResultItem
                {
                    Title = $"Coordinates: {x:F5}°, {y:F5}°",
                    Subtitle = $"WGS 84 (EPSG:4326) • Longitude: {x:F5}°, Latitude: {y:F5}°",
                    Longitude = x,
                    Latitude = y,
                    Altitude = 3000.0,
                    Source = "WGS 84 Coordinates",
                    SourceBadge = "WGS84",
                    Icon = "🎯",
                    IsLocal = true
                };
            }
            else
            {
                // Projected CRS validation
                if (epsgCode == 22992) // Egypt 1907 / Red Belt
                {
                    if (x < 100000.0 || x > 1200000.0 || y < 200000.0 || y > 2500000.0)
                    {
                        if (y >= 100000.0 && y <= 1200000.0 && x >= 200000.0 && x <= 2500000.0)
                        {
                            throw new CoordinateValidationException(
                                $"Projected coordinates appear swapped for Egypt Red Belt (EPSG:22992).\n" +
                                $"X (Easting) should be ~300,000-900,000m, Y (Northing) ~500,000-1,500,000m. Please verify X above Y.");
                        }
                    }
                }
                else if (epsgCode >= 32601 && epsgCode <= 32660) // UTM Northern
                {
                    if (x < 50000.0 || x > 950000.0)
                    {
                        throw new CoordinateValidationException($"UTM X (Easting) must typically be between 100,000m and 900,000m. Received: {x:F1}m");
                    }
                    if (y < 0.0 || y > 10000000.0)
                    {
                        throw new CoordinateValidationException($"UTM Y (Northing) Northern Hemisphere must be between 0m and 10,000,000m. Received: {y:F1}m");
                    }
                }

                // Reproject to WGS84
                var (lon, lat) = CoordinateTransformService.Transform(x, y, epsgCode, 4326);

                if (double.IsNaN(lon) || double.IsNaN(lat) || lon < -180 || lon > 180 || lat < -90 || lat > 90)
                {
                    throw new CoordinateValidationException($"Reprojection to WGS84 resulted in invalid coordinates: Lon {lon}, Lat {lat}");
                }

                return new SearchResultItem
                {
                    Title = $"Projected: X={x:F1}m, Y={y:F1}m",
                    Subtitle = $"EPSG:{epsgCode} -> WGS 84 ({lon:F5}°, {lat:F5}°)",
                    Longitude = lon,
                    Latitude = lat,
                    Altitude = 3000.0,
                    Source = $"EPSG:{epsgCode} Projected",
                    SourceBadge = $"EPSG:{epsgCode}",
                    Icon = "🎯",
                    IsLocal = true
                };
            }
        }

        /// <summary>
        /// Parses coordinate strings in decimal format (e.g. 31.2357, -122.4) or DMS (e.g. 31° 14' 8.5" E).
        /// </summary>
        public static double ParseCoordinateValue(string input, bool isLongitudeOrEasting)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new CoordinateValidationException("Coordinate input is empty.");

            string clean = input.Trim();

            // Try standard decimal parsing first
            if (double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out double decimalVal))
            {
                return decimalVal;
            }
            if (double.TryParse(clean, NumberStyles.Float, CultureInfo.CurrentCulture, out double localDecimalVal))
            {
                return localDecimalVal;
            }

            // Try DMS format (e.g. 31° 14' 8.5" E or 30 2 39.8 N or 31d 14m 8.5s E)
            var dmsRegex = new Regex(@"^\s*([+-]?\d+(?:\.\d+)?)[°\s_dD]+(?:(\d+(?:\.\d+)?)[′'\s_mM]+)?(?:(\d+(?:\.\d+)?)[″""\s_sS]*)?\s*([NSEWnsew])?\s*$", RegexOptions.Compiled);
            var match = dmsRegex.Match(clean);
            if (match.Success)
            {
                double deg = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                double min = match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value)
                    ? double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                    : 0.0;
                double sec = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value)
                    ? double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 0.0;

                double val = Math.Abs(deg) + (min / 60.0) + (sec / 3600.0);
                if (deg < 0) val = -val;

                if (match.Groups[4].Success && !string.IsNullOrEmpty(match.Groups[4].Value))
                {
                    char hem = char.ToUpperInvariant(match.Groups[4].Value[0]);
                    if (hem == 'S' || hem == 'W')
                    {
                        val = -Math.Abs(val);
                    }
                    else if (hem == 'N' || hem == 'E')
                    {
                        val = Math.Abs(val);
                    }
                }

                return val;
            }

            throw new CoordinateValidationException($"Unable to parse '{clean}' as a valid coordinate value.");
        }
    }
}
