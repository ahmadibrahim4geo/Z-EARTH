using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZEarth.App.Services;

public sealed record WaybackRelease(string ReleaseId, DateTime Date, string Title, string? TemplateUrl = null)
{
    public string Id => $"wayback-{ReleaseId}";
    public string DisplayName => $"{Date:yyyy-MM-dd}  •  {Title}";
    public string TileUrl => TemplateUrl ?? $"https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tile/{ReleaseId}/{{z}}/{{y}}/{{x}}";
}

/// <summary>Loads the public Esri World Imagery Wayback release catalog.</summary>
public sealed class WaybackService
{
    public const string CatalogUrl = "https://s3-us-west-2.amazonaws.com/config.maptiles.arcgis.com/waybackconfig.json";
    public const string WmtsCapabilitiesUrl = "https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/WMTS/1.0.0/WMTSCapabilities.xml";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Regex DatePattern = new(
        @"(?<y>20\d{2})[-_/ ](?<m>1[0-2]|0?[1-9])[-_/ ](?<d>3[01]|[12]\d|0?[1-9])(?!\d)",
        RegexOptions.Compiled);

    public async Task<IReadOnlyList<WaybackRelease>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(CatalogUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = ParseReleasesJson(json);
            if (releases.Count > 0) return releases;
            throw new InvalidOperationException("Wayback returned no validated release entries.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggerService.Instance.LogWarning($"Public Wayback catalog unavailable: {ex.Message}");
            throw new InvalidOperationException("Esri Wayback release catalog is unavailable. Check internet access or retry.", ex);
        }
    }

    /// <summary>Offline-fixture-testable parser for Esri waybackconfig.json.</summary>
    public static IReadOnlyList<WaybackRelease> ParseReleasesJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var entries = new Dictionary<string, WaybackRelease>(StringComparer.OrdinalIgnoreCase);
        Discover(doc.RootElement, null, entries);
        return entries.Values.OrderBy(item => item.Date).ThenBy(item => item.ReleaseId, StringComparer.Ordinal).ToList();
    }

    /// <summary>Allow only Esri Wayback HTTPS tile templates; never proxy arbitrary catalog URLs.</summary>
    public static string? NormalizeTileUrl(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var normalized = source.Replace("{level}", "{z}", StringComparison.OrdinalIgnoreCase)
            .Replace("{row}", "{y}", StringComparison.OrdinalIgnoreCase)
            .Replace("{col}", "{x}", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.Port != 443 ||
            !(uri.Host.Equals("wayback.maptiles.arcgis.com", StringComparison.OrdinalIgnoreCase) ||
              (uri.Host.StartsWith("wayback-", StringComparison.OrdinalIgnoreCase) &&
               uri.Host.EndsWith(".maptiles.arcgis.com", StringComparison.OrdinalIgnoreCase))) ||
            !uri.AbsolutePath.Contains("/World_Imagery/", StringComparison.Ordinal) ||
            !uri.AbsolutePath.Contains("/tile/", StringComparison.Ordinal) ||
            !normalized.Contains("{z}", StringComparison.Ordinal) ||
            !normalized.Contains("{y}", StringComparison.Ordinal) ||
            !normalized.Contains("{x}", StringComparison.Ordinal)) return null;
        return normalized;
    }

    private static void Discover(JsonElement element, string? propertyName, IDictionary<string, WaybackRelease> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var title = TryString(element, "itemTitle") ?? TryString(element, "title") ?? TryString(element, "name") ?? string.Empty;
            var id = TryString(element, "releaseNum") ?? TryString(element, "releaseId");
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(propertyName) &&
                propertyName.All(char.IsDigit)) id = propertyName;
            var tile = NormalizeTileUrl(TryString(element, "itemURL"));
            var dateText = TryString(element, "releaseDateLabel") ?? title;
            if (!TryParseDate(dateText, out _))
                dateText = TryString(element, "releaseDate") ?? TryString(element, "date") ?? title;
            if (!string.IsNullOrWhiteSpace(id) && id.Length <= 8 && id.All(char.IsDigit) &&
                tile != null && tile.Contains("/tile/" + id + "/", StringComparison.Ordinal) &&
                TryParseDate(dateText, out var date))
            {
                result[id] = new WaybackRelease(id, date, string.IsNullOrWhiteSpace(title) ?
                    "World Imagery Wayback" : title, tile);
            }

            foreach (var property in element.EnumerateObject())
                Discover(property.Value, property.Name, result);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Discover(child, propertyName, result);
        }
    }

    private static string? TryString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date)) return true;
        var match = DatePattern.Match(value ?? string.Empty);
        return match.Success && DateTime.TryParseExact($"{match.Groups["y"].Value}-{match.Groups["m"].Value.PadLeft(2, '0')}-{match.Groups["d"].Value.PadLeft(2, '0')}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
