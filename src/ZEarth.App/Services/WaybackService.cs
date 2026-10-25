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

public sealed record WaybackRelease(string ReleaseId, DateTime Date, string Title)
{
    public string Id => $"wayback-{ReleaseId}";
    public string DisplayName => $"{Date:yyyy-MM-dd}  •  {Title}";
    public string TileUrl => $"https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tile/{ReleaseId}/{{z}}/{{y}}/{{x}}";
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
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var releases = new Dictionary<string, WaybackRelease>(StringComparer.OrdinalIgnoreCase);
            Discover(document.RootElement, null, releases);
            if (releases.Count > 0)
                return releases.Values.OrderByDescending(r => r.Date).ToList();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogWarning($"Wayback catalog unavailable: {ex.Message}");
        }

        // Known public releases keep the feature usable if the catalog endpoint is temporarily offline.
        return new[]
        {
            new WaybackRelease("63116", new DateTime(2026, 9, 24), "World Imagery Wayback"),
            new WaybackRelease("32246", new DateTime(2026, 6, 30), "World Imagery Wayback")
        };
    }

    private static void Discover(JsonElement element, string? propertyName, IDictionary<string, WaybackRelease> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var title = TryString(element, "itemTitle") ?? TryString(element, "title") ?? TryString(element, "name") ?? string.Empty;
            var id = TryString(element, "releaseNum") ?? TryString(element, "releaseId") ?? TryString(element, "id");
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(propertyName) && propertyName.All(char.IsDigit)) id = propertyName;
            // The item title carries the imagery vintage; releaseDate can be the
            // catalog publication timestamp and may differ from the capture vintage.
            var dateText = title;
            if (!TryParseDate(dateText, out _))
                dateText = TryString(element, "releaseDate") ?? TryString(element, "date") ?? title;
            if (!string.IsNullOrWhiteSpace(id) && TryParseDate(dateText, out var date))
            {
                result[id] = new WaybackRelease(id, date, string.IsNullOrWhiteSpace(title) ? "World Imagery Wayback" : title);
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
