using System.Collections.Concurrent;
using System.IO;
using OSGeo.GDAL;
using OSGeo.OSR;
using ZEarth.Domain.Gis;

namespace ZEarth.App.Services;

/// <summary>
/// Converts an explicitly selected numeric elevation raster into bounded 65 x 65
/// Web-Mercator height tiles for the embedded Cesium heightmap provider. It does
/// preserves missing samples as IEEE NaN so clients can distinguish coverage from fallback.
/// </summary>
public sealed class LocalTerrainRasterService
{
    private const int GridSize = 65;
    private const double WebMercatorHalfWorld = 20037508.342789244;
    private static readonly Lazy<LocalTerrainRasterService> LazyInstance = new(() => new());
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _tileWorkers = new(2, 2);
    private readonly ConcurrentDictionary<string, byte[]> _tileCache = new();
    private string? _path;
    private double _verticalScale = 1d;
    private long _generation;

    public static LocalTerrainRasterService Instance => LazyInstance.Value;
    public LocalTerrainRasterMetadata? Metadata { get; private set; }

    private LocalTerrainRasterService()
    {
    }

    public LocalTerrainRasterMetadata Configure(string path, string verticalUnit)
    {
        var metadata = Inspect(path, verticalUnit);
        lock (_stateLock)
        {
            Metadata = metadata;
            _path = Path.GetFullPath(path);
            _verticalScale = verticalUnit == "Feet" ? 0.3048d : 1d;
            _generation++;
            _tileCache.Clear();
        }
        return metadata;
    }

    /// <summary>Validate a candidate without changing the currently active raster.</summary>
    public LocalTerrainRasterMetadata Inspect(string path, string verticalUnit)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new FileNotFoundException("Choose an existing local elevation raster.", path);

        _ = verticalUnit switch
        {
            "Meters" => 1d,
            "Feet" => 0.3048d,
            _ => throw new InvalidDataException("Choose whether raster elevations are stored in metres or feet.")
        };

        GdalConfiguration.EnsureInitialized();
        if (!GdalConfiguration.IsAvailable)
            throw new InvalidOperationException("GDAL is unavailable; the raster cannot be validated.");

        {
            using var source = Gdal.Open(path, Access.GA_ReadOnly)
                ?? throw new InvalidDataException("GDAL could not open the selected raster.");
            if (source.RasterCount < 1 || source.RasterXSize < 2 || source.RasterYSize < 2)
                throw new InvalidDataException("The selected file does not contain a usable numeric raster band.");
            var projection = source.GetProjectionRef();
            if (string.IsNullOrWhiteSpace(projection))
                throw new InvalidDataException("The raster has no CRS. Assign the correct CRS before using it as terrain.");
            var transform = new double[6];
            source.GetGeoTransform(transform);
            if (!transform.All(double.IsFinite) || Math.Abs(transform[1]) < 1e-15 || Math.Abs(transform[5]) < 1e-15)
                throw new InvalidDataException("The raster has no valid georeferenced pixel transform.");

            using var band = source.GetRasterBand(1);
            band.GetNoDataValue(out var noData, out var hasNoData);
            var unit = band.GetUnitType() ?? string.Empty;
            var minX = transform[0];
            var maxY = transform[3];
            var maxX = transform[0] + source.RasterXSize * transform[1] + source.RasterYSize * transform[2];
            var minY = transform[3] + source.RasterXSize * transform[4] + source.RasterYSize * transform[5];
            var probe = FindProbe(source, band, transform, projection, hasNoData != 0 ? noData : null);
            return new LocalTerrainRasterMetadata(
                Path.GetFileName(path), source.RasterXSize, source.RasterYSize,
                projection, Math.Min(minX, maxX), Math.Min(minY, maxY),
                Math.Max(minX, maxX), Math.Max(minY, maxY),
                hasNoData != 0 ? noData : null, unit, verticalUnit, probe.Level, probe.X, probe.Y);
        }
    }

    public async Task<byte[]?> GetTileAsync(int level, int x, int y)
    {
        if (level < 0 || level > 18) return null;
        var maximum = (1 << level) - 1;
        if (x < 0 || y < 0 || x > maximum || y > maximum) return null;
        // Bound parallel GDAL work; Configure never waits on a slow tile.
        if (!await _tileWorkers.WaitAsync(TimeSpan.FromSeconds(20))) return null;
        try { return await Task.Run(() => GetTile(level, x, y)); }
        finally { _tileWorkers.Release(); }
    }

    private byte[]? GetTile(int level, int x, int y)
    {
        string? path;
        double scale;
        long generation;
        lock (_stateLock) { path = _path; scale = _verticalScale; generation = _generation; }
        if (path is null) return null;
        var cacheKey = $"{generation}|{path}|{scale:R}|{level}/{x}/{y}";
        if (_tileCache.TryGetValue(cacheKey, out var cached)) return cached;

        {
            if (_tileCache.TryGetValue(cacheKey, out cached)) return cached;
            using var source = Gdal.Open(path, Access.GA_ReadOnly);
            if (source is null) return null;
            using var sourceBand = source.GetRasterBand(1);
            sourceBand.GetScale(out var sourceScale, out var hasScale);
            sourceBand.GetOffset(out var sourceOffset, out var hasOffset);
            if (hasScale == 0) sourceScale = 1d;
            if (hasOffset == 0) sourceOffset = 0d;
            if (!double.IsFinite(sourceScale) || !double.IsFinite(sourceOffset)) return null;
            var memoryDriver = Gdal.GetDriverByName("MEM")
                ?? throw new InvalidOperationException("GDAL MEM driver is unavailable.");
            using var destination = memoryDriver.Create(string.Empty, GridSize, GridSize, 1, DataType.GDT_Float32, null);
            var count = 1 << level;
            var span = WebMercatorHalfWorld * 2d / count;
            var west = -WebMercatorHalfWorld + x * span;
            var north = WebMercatorHalfWorld - y * span;
            var step = span / (GridSize - 1d);
            destination.SetGeoTransform(new[] { west - step / 2d, step, 0d, north + step / 2d, 0d, -step });
            using var mercator = new SpatialReference(string.Empty);
            mercator.ImportFromEPSG(3857);
            mercator.ExportToWkt(out var mercatorWkt, null);
            destination.SetProjection(mercatorWkt);
            using var destinationBand = destination.GetRasterBand(1);
            destinationBand.SetNoDataValue(double.NaN);
            destinationBand.Fill(double.NaN, 0d);

            // The high-level warp selects pre-existing overviews (unlike
            // ReprojectImage), making global rasters practical at low zoom.
            using var options = new GDALWarpAppOptions(new[] { "-r", "bilinear", "-ovr", "AUTO", "-dstnodata", "nan" });
            var result = Gdal.Warp(destination, new[] { source }, options, null, null);
            if (result == 0) return null;

            var heights = new float[GridSize * GridSize];
            destinationBand.ReadRaster(0, 0, GridSize, GridSize, heights, GridSize, GridSize, 0, 0);
            for (var i = 0; i < heights.Length; i++)
                if (float.IsFinite(heights[i]))
                    heights[i] = (float)((heights[i] * sourceScale + sourceOffset) * scale);

            var bytes = new byte[heights.Length * sizeof(float)];
            Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
            lock (_stateLock)
            {
                // A slow previous source must never be returned after a switch.
                if (_generation != generation) return null;
                if (_tileCache.Count >= 160) _tileCache.Clear();
                _tileCache[cacheKey] = bytes;
            }
            return bytes;
        }
    }
    private static (int Level, int X, int Y) FindProbe(Dataset source, Band band, double[] transform, string projection, double? noData)
    {
        // Bounded overview read: choose a valid pixel nearest the centre rather
        // than assuming that the active camera falls inside a regional raster.
        const int sampleSize = 64;
        var sample = new float[sampleSize * sampleSize];
        band.ReadRaster(0, 0, source.RasterXSize, source.RasterYSize,
            sample, sampleSize, sampleSize, 0, 0);
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < sample.Length; i++)
        {
            if (!float.IsFinite(sample[i]) || (noData.HasValue && sample[i] == noData.Value)) continue;
            var dx = i % sampleSize - (sampleSize - 1) / 2d;
            var dy = i / sampleSize - (sampleSize - 1) / 2d;
            var distance = dx * dx + dy * dy;
            if (distance < bestDistance) { best = i; bestDistance = distance; }
        }
        if (best < 0) throw new InvalidDataException("The raster contains no valid elevations in the overview sample.");
        var pixelX = (best % sampleSize + 0.5) * source.RasterXSize / sampleSize;
        var pixelY = (best / sampleSize + 0.5) * source.RasterYSize / sampleSize;
        var sourceX = transform[0] + pixelX * transform[1] + pixelY * transform[2];
        var sourceY = transform[3] + pixelX * transform[4] + pixelY * transform[5];
        using var from = new SpatialReference(projection);
        using var to = new SpatialReference(string.Empty);
        to.ImportFromEPSG(4326);
        from.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        to.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        using var converter = new CoordinateTransformation(from, to);
        var point = new double[3];
        converter.TransformPoint(point, sourceX, sourceY, 0);
        var lon = point[0];
        var lat = point[1];
        if (!double.IsFinite(lon) || !double.IsFinite(lat) || lat < -85.05112878 || lat > 85.05112878)
            throw new InvalidDataException("The raster sample is outside the supported Web Mercator latitude range.");
        const int level = 18;
        const int count = 1 << level;
        lon = ((lon + 180) % 360 + 360) % 360;
        var x = Math.Clamp((int)Math.Floor(lon / 360 * count), 0, count - 1);
        var radians = lat * Math.PI / 180;
        var y = Math.Clamp((int)Math.Floor((1 - Math.Log(Math.Tan(radians) + 1 / Math.Cos(radians)) / Math.PI) / 2 * count), 0, count - 1);
        return (level, x, y);
    }
}

public sealed record LocalTerrainRasterMetadata(
    string FileName,
    int Width,
    int Height,
    string Projection,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double? NoData,
    string SourceUnit,
    string SelectedVerticalUnit,
    int ProbeLevel,
    int ProbeX,
    int ProbeY)
{
    public string ToDisplayText() =>
        $"{FileName} • {Width} × {Height} px • CRS present • " +
        $"extent {MinX:0.###}, {MinY:0.###} → {MaxX:0.###}, {MaxY:0.###} • " +
        $"NoData {(NoData.HasValue ? NoData.Value.ToString("0.###") : "not declared")} • " +
        $"source unit {(string.IsNullOrWhiteSpace(SourceUnit) ? "not declared" : SourceUnit)} • interpreted as {SelectedVerticalUnit}";
}
