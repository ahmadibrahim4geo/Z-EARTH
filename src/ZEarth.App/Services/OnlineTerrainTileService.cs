using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using OSGeo.GDAL;
using OSGeo.OSR;
using ZEarth.Domain.Gis;

namespace ZEarth.App.Services;

/// <summary>
/// Adapters for official online numeric DEM APIs that are not Cesium terrain
/// services. Responses are converted to the same 65 x 65 metre height grid used
/// by the embedded CustomHeightmapTerrainProvider.
/// </summary>
public sealed class OnlineTerrainTileService
{
    private const int GridSize = 65;
    private const double WebMercatorHalfWorld = 20037508.342789244;
    private const string CopernicusProcessUrl = "https://sh.dataspace.copernicus.eu/process/v1";
    // The Australian Antarctic Division publishes the GEBCO grid as a
    // range-readable, internally tiled GeoTIFF on Source Cooperative. The old
    // CEDA /vsicurl NetCDF path was a file-download URL, not an OPeNDAP API.
    private const string GebcoDataset = "/vsicurl/https://data.source.coop/ausantarctic/gebco/GEBCO_2026.tif";
    private static readonly Lazy<OnlineTerrainTileService> LazyInstance = new(() => new());
    private static readonly Lazy<string> WindowsTrustBundle = new(CreateWindowsTrustBundle);
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(35) };
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly object _gdalLock = new();

    public static OnlineTerrainTileService Instance => LazyInstance.Value;

    private OnlineTerrainTileService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZEarth-Desktop-GIS/2.2");
    }

    public async Task<byte[]?> GetCopernicusGlo30TileAsync(int level, int x, int y, string accessToken)
    {
        ValidateTile(level, x, y);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Copernicus Data Space OAuth access token is required.");
        // Cesium's heightmap is a regular Web-Mercator grid, not a grid evenly
        // spaced in latitude. Request in EPSG:3857 and pad by half a sample so
        // API pixel centres coincide with the heightmap vertices at tile edges.
        var (west, south, east, north) = GetMercatorSampleBounds(level, x, y);
        var requestBody = new
        {
            input = new
            {
                bounds = new
                {
                    properties = new { crs = "http://www.opengis.net/def/crs/EPSG/0/3857" },
                    bbox = new[] { west, south, east, north }
                },
                data = new[]
                {
                    new
                    {
                        type = "dem",
                        dataFilter = new { demInstance = "COPERNICUS_30" },
                        processing = new { upsampling = "BILINEAR", downsampling = "BILINEAR" }
                    }
                }
            },
            output = new
            {
                width = GridSize,
                height = GridSize,
                responses = new[] { new { identifier = "default", format = new { type = "image/tiff" } } }
            },
            evalscript = "//VERSION=3\nfunction setup(){return {input:[\"DEM\"],output:{id:\"default\",bands:1,sampleType:SampleType.FLOAT32}};}\nfunction evaluatePixel(sample){return [sample.DEM];}"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, CopernicusProcessUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Copernicus Process API returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        var tiff = await response.Content.ReadAsByteArrayAsync();
        // Do not reuse data across credentials: a later token may be expired or
        // lack GLO-30 access, and a cache hit would conceal that failure.
        return ReadFloat32GeoTiff(tiff);
    }

    public Task<byte[]?> GetGebco2026TileAsync(int level, int x, int y)
    {
        ValidateTile(level, x, y);
        var cacheKey = $"gebco2026/{level}/{x}/{y}";
        if (_cache.TryGetValue(cacheKey, out var cached)) return Task.FromResult<byte[]?>(cached);
        return Task.Run(() =>
        {
            lock (_gdalLock)
            {
                if (_cache.TryGetValue(cacheKey, out cached)) return cached;
                GdalConfiguration.EnsureInitialized();
                if (!GdalConfiguration.IsAvailable) throw new InvalidOperationException("GDAL is unavailable.");
                // Limit individual HTTP calls and avoid probing nonexistent
                // sidecar files. Restore prior thread-local settings afterwards.
                var settings = new Dictionary<string, string?>
                {
                    ["GDAL_HTTP_CONNECTTIMEOUT"] = "8", ["GDAL_HTTP_TIMEOUT"] = "15",
                    ["GDAL_HTTP_MAX_RETRY"] = "0", ["GDAL_DISABLE_READDIR_ON_OPEN"] = "EMPTY_DIR",
                    ["GDAL_PAM_ENABLED"] = "NO"
                };
                // MaxRev's Windows libcurl uses OpenSSL and may not have a CA
                // bundle. Export server-authentication trusted roots; do not
                // disable TLS checks or replace a supplied bundle.
                if (string.IsNullOrWhiteSpace(Gdal.GetConfigOption("CURL_CA_BUNDLE", null)))
                    settings["CURL_CA_BUNDLE"] = WindowsTrustBundle.Value;
                var previous = settings.ToDictionary(item => item.Key,
                    item => Gdal.GetThreadLocalConfigOption(item.Key, null));
                try
                {
                    foreach (var (key, value) in settings)
                        Gdal.SetThreadLocalConfigOption(key, value);
                    using var source = Gdal.Open(GebcoDataset, Access.GA_ReadOnly)
                        ?? throw new InvalidDataException($"GEBCO 2026 GeoTIFF could not be opened: {Gdal.GetLastErrorMsg()}");
                    var result = ReprojectSourceToTile(source, level, x, y);
                    if (result is not null) Store(cacheKey, result);
                    return result;
                }
                finally
                {
                    foreach (var (key, value) in previous)
                        Gdal.SetThreadLocalConfigOption(key, value);
                }
            }
        });
    }

    private static string CreateWindowsTrustBundle()
    {
        var certificates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var disallowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            using var roots = new X509Store(StoreName.Root, location);
            roots.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in roots.Certificates)
            {
                var eku = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
                if (eku is null || eku.EnhancedKeyUsages.Cast<Oid>().Any(oid =>
                    oid.Value is "1.3.6.1.5.5.7.3.1" or "2.5.29.37.0"))
                    certificates[cert.Thumbprint] = cert.RawData;
            }
            using var denied = new X509Store(StoreName.Disallowed, location);
            denied.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in denied.Certificates)
                disallowed.Add(cert.Thumbprint);
        }
        var pem = new StringBuilder();
        foreach (var (thumbprint, data) in certificates)
            if (!disallowed.Contains(thumbprint))
                pem.AppendLine(PemEncoding.WriteString("CERTIFICATE", data));
        if (pem.Length == 0)
            throw new InvalidOperationException("Windows has no trusted root certificates available for the GEBCO connection.");
        var path = Path.Combine(Path.GetTempPath(), $"zearth-ca-{Guid.NewGuid():N}.pem");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            writer.Write(pem.ToString());
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        return path;
    }

    private static byte[] ReadFloat32GeoTiff(byte[] bytes)
    {
        if (bytes.Length == 0) throw new InvalidDataException("The DEM API returned an empty GeoTIFF.");
        GdalConfiguration.EnsureInitialized();
        if (!GdalConfiguration.IsAvailable) throw new InvalidOperationException("GDAL is unavailable.");
        var virtualPath = $"/vsimem/zearth-dem-{Guid.NewGuid():N}.tif";
        try
        {
            Gdal.FileFromMemBuffer(virtualPath, bytes);
            using var dataset = Gdal.Open(virtualPath, Access.GA_ReadOnly)
                ?? throw new InvalidDataException("GDAL could not read the DEM API GeoTIFF response.");
            if (dataset.RasterXSize != GridSize || dataset.RasterYSize != GridSize || dataset.RasterCount < 1)
                throw new InvalidDataException("The DEM API returned an unexpected raster grid.");
            using var band = dataset.GetRasterBand(1);
            var heights = new float[GridSize * GridSize];
            band.GetNoDataValue(out var noData, out var hasNoData);
            if (band.ReadRaster(0, 0, GridSize, GridSize, heights, GridSize, GridSize, 0, 0) != CPLErr.CE_None ||
                heights.Any(value => !float.IsFinite(value) || (hasNoData != 0 && value == noData)))
                throw new InvalidDataException("The DEM API tile contains unresolved NoData values.");
            return ToBytes(heights);
        }
        finally
        {
            Gdal.Unlink(virtualPath);
        }
    }

    private static byte[]? ReprojectSourceToTile(Dataset source, int level, int x, int y)
    {
        var count = 1 << level;
        var span = WebMercatorHalfWorld * 2d / count;
        var west = -WebMercatorHalfWorld + x * span;
        var north = WebMercatorHalfWorld - y * span;
        var step = span / (GridSize - 1d);
        // A grid node at +180 is on the half-open raster boundary. Nudge the
        // easternmost node 0.1 m inside that boundary (far below 15 arc-second
        // source resolution) so GDAL samples the actual last source cell.
        var east = west + span + step / 2d - (x == count - 1 ? 0.1d : 0d);
        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        using var options = new GDALWarpAppOptions(new[]
        {
            "-of", "MEM", "-t_srs", "EPSG:3857", "-te",
            Number(west - step / 2d), Number(north - span - step / 2d),
            Number(east), Number(north + step / 2d),
            "-ts", GridSize.ToString(CultureInfo.InvariantCulture), GridSize.ToString(CultureInfo.InvariantCulture),
            "-ot", "Float32", "-r", "bilinear", "-dstnodata", "nan"
        });
        // The warp application selects source overviews. ReprojectImage reads
        // the full-resolution global raster and makes root tiles impractically slow.
        using var destination = Gdal.Warp(string.Empty, new[] { source }, options, null, null)
            ?? throw new InvalidDataException($"GEBCO 2026 tile conversion failed: {Gdal.GetLastErrorMsg()}");
        using var band = destination.GetRasterBand(1);
        var heights = new float[GridSize * GridSize];
        if (band.ReadRaster(0, 0, GridSize, GridSize, heights, GridSize, GridSize, 0, 0) != CPLErr.CE_None ||
            heights.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("GEBCO 2026 returned a tile with missing or invalid elevations.");
        return ToBytes(heights);
    }

    public static (double West, double South, double East, double North) GetMercatorSampleBounds(int level, int x, int y)
    {
        ValidateTile(level, x, y);
        var span = WebMercatorHalfWorld * 2d / (1 << level);
        var halfSample = span / (GridSize - 1d) / 2d;
        var west = -WebMercatorHalfWorld + x * span;
        var north = WebMercatorHalfWorld - y * span;
        return (Math.Max(-WebMercatorHalfWorld, west - halfSample),
            Math.Max(-WebMercatorHalfWorld, north - span - halfSample),
            Math.Min(WebMercatorHalfWorld, west + span + halfSample),
            Math.Min(WebMercatorHalfWorld, north + halfSample));
    }

    private static void ValidateTile(int level, int x, int y)
    {
        if (level < 0 || level > 14) throw new ArgumentOutOfRangeException(nameof(level));
        var maximum = (1 << level) - 1;
        if (x < 0 || y < 0 || x > maximum || y > maximum)
            throw new ArgumentOutOfRangeException(nameof(x), "Tile coordinates are outside the selected level.");
    }

    private void Store(string key, byte[] value)
    {
        if (_cache.Count >= 160) _cache.Clear();
        _cache[key] = value;
    }

    private static byte[] ToBytes(float[] heights)
    {
        var bytes = new byte[heights.Length * sizeof(float)];
        Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
