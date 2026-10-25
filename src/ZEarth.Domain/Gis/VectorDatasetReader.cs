using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using ZEarth.Domain.Crs;

namespace ZEarth.Domain.Gis
{
    public class VectorImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string SourceCrs { get; set; } = "EPSG:4326";
        public bool NeedsCrsResolution { get; set; } = false;
        public List<PlaceItem> Features { get; set; } = new();
        public double MinLon { get; set; } = double.MaxValue;
        public double MinLat { get; set; } = double.MaxValue;
        public double MaxLon { get; set; } = double.MinValue;
        public double MaxLat { get; set; } = double.MinValue;

        public void UpdateBounds(double lon, double lat)
        {
            if (lon < MinLon) MinLon = lon;
            if (lon > MaxLon) MaxLon = lon;
            if (lat < MinLat) MinLat = lat;
            if (lat > MaxLat) MaxLat = lat;
        }
    }

    public static class VectorDatasetReader
    {
        public static VectorImportResult ReadFile(string filePath, string? overrideCrs = null)
        {
            var result = new VectorImportResult();

            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"File not found: {filePath}";
                return result;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            try
            {
                switch (ext)
                {
                    case ".geojson":
                    case ".json":
                        return ReadGeoJson(filePath, overrideCrs);

                    case ".shp":
                        return ReadShapefile(filePath, overrideCrs);

                    case ".gpkg":
                        return ReadGeoPackage(filePath, overrideCrs);

                    case ".tif":
                    case ".tiff":
                        return ReadGeoTiff(filePath, overrideCrs);

                    default:
                        result.Success = false;
                        result.ErrorMessage = $"Unsupported GIS vector format: {ext}";
                        return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to read vector dataset: {ex.Message}";
                return result;
            }
        }

        public static VectorImportResult ReadGeoJson(string filePath, string? overrideCrs = null)
        {
            var result = new VectorImportResult { SourceFormat = "GeoJSON", Success = true };
            var json = File.ReadAllText(filePath, Encoding.UTF8);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detect CRS in GeoJSON if present (urn:ogc:def:crs:OGC:1.3:CRS84, EPSG:32636, etc.)
            string detectedCrs = "EPSG:4326";
            if (root.TryGetProperty("crs", out var crsProp) && crsProp.TryGetProperty("properties", out var crsProps))
            {
                if (crsProps.TryGetProperty("name", out var crsName))
                {
                    var nameStr = crsName.GetString() ?? "";
                    if (nameStr.Contains("32636")) detectedCrs = "EPSG:32636";
                    else if (nameStr.Contains("32635")) detectedCrs = "EPSG:32635";
                    else if (nameStr.Contains("3857")) detectedCrs = "EPSG:3857";
                    else if (nameStr.Contains("22992")) detectedCrs = "EPSG:22992";
                }
            }

            result.SourceCrs = overrideCrs ?? detectedCrs;

            bool isWgs84 = result.SourceCrs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase) ||
                           result.SourceCrs.Equals("CRS84", StringComparison.OrdinalIgnoreCase);

            var type = root.TryGetProperty("type", out var tProp) ? tProp.GetString() : "FeatureCollection";

            if (type == "FeatureCollection" && root.TryGetProperty("features", out var featuresProp))
            {
                int index = 1;
                foreach (var feature in featuresProp.EnumerateArray())
                {
                    var item = ParseGeoJsonFeature(feature, ref index, result.SourceCrs, isWgs84, result);
                    if (item != null)
                    {
                        result.Features.Add(item);
                    }
                }
            }
            else if (type == "Feature")
            {
                int index = 1;
                var item = ParseGeoJsonFeature(root, ref index, result.SourceCrs, isWgs84, result);
                if (item != null) result.Features.Add(item);
            }

            return result;
        }

        private static PlaceItem? ParseGeoJsonFeature(JsonElement feature, ref int index, string sourceCrs, bool isWgs84, VectorImportResult result)
        {
            if (!feature.TryGetProperty("geometry", out var geomProp))
                return null;

            var geomType = geomProp.TryGetProperty("type", out var gt) ? gt.GetString() : "Point";
            if (!geomProp.TryGetProperty("coordinates", out var coordsProp))
                return null;

            string name = $"Feature #{index++}";
            if (feature.TryGetProperty("properties", out var props))
            {
                if (props.TryGetProperty("name", out var np)) name = np.GetString() ?? name;
                else if (props.TryGetProperty("NAME", out var np2)) name = np2.GetString() ?? name;
                else if (props.TryGetProperty("title", out var tp)) name = tp.GetString() ?? name;
            }

            var item = new PlaceItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name
            };
            item.ExtendedData["SourceCrs"] = sourceCrs;

            if (geomType == "Point")
            {
                item.ItemType = PlaceItemType.Placemark;
                var coords = coordsProp.EnumerateArray();
                coords.MoveNext();
                double x = coords.Current.GetDouble();
                coords.MoveNext();
                double y = coords.Current.GetDouble();
                double z = coords.MoveNext() ? coords.Current.GetDouble() : 0.0;

                var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                item.Longitude = lon;
                item.Latitude = lat;
                item.Altitude = z;
                result.UpdateBounds(lon, lat);
                return item;
            }
            else if (geomType == "LineString")
            {
                item.ItemType = PlaceItemType.Path;
                foreach (var pt in coordsProp.EnumerateArray())
                {
                    var coords = pt.EnumerateArray();
                    coords.MoveNext();
                    double x = coords.Current.GetDouble();
                    coords.MoveNext();
                    double y = coords.Current.GetDouble();
                    double z = coords.MoveNext() ? coords.Current.GetDouble() : 0.0;

                    var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                    item.Coordinates.Add(new CoordinatePoint(lon, lat, z));
                    result.UpdateBounds(lon, lat);
                }
                return item;
            }
            else if (geomType == "Polygon")
            {
                item.ItemType = PlaceItemType.Polygon;
                bool isExterior = true;
                foreach (var ring in coordsProp.EnumerateArray())
                {
                    var ringPoints = new List<CoordinatePoint>();
                    foreach (var pt in ring.EnumerateArray())
                    {
                        var coords = pt.EnumerateArray();
                        coords.MoveNext();
                        double x = coords.Current.GetDouble();
                        coords.MoveNext();
                        double y = coords.Current.GetDouble();
                        double z = coords.MoveNext() ? coords.Current.GetDouble() : 0.0;

                        var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                        ringPoints.Add(new CoordinatePoint(lon, lat, z));
                        result.UpdateBounds(lon, lat);
                    }

                    if (isExterior)
                    {
                        item.Coordinates = ringPoints;
                        isExterior = false;
                    }
                    else
                    {
                        item.InnerRings.Add(ringPoints);
                    }
                }
                return item;
            }

            return null;
        }

        public static VectorImportResult ReadShapefile(string shpPath, string? overrideCrs = null)
        {
            var result = new VectorImportResult { SourceFormat = "ESRI Shapefile", Success = true };

            // Check for companion .prj file (AC-008)
            var prjPath = Path.ChangeExtension(shpPath, ".prj");
            if (overrideCrs != null)
            {
                result.SourceCrs = overrideCrs;
                result.NeedsCrsResolution = false;
            }
            else if (File.Exists(prjPath))
            {
                var prjText = File.ReadAllText(prjPath).Trim();
                result.SourceCrs = ParsePrjCrs(prjText);
                result.NeedsCrsResolution = false;
            }
            else
            {
                // PRJ missing! Flag for user selection (AC-008)
                result.SourceCrs = "UNKNOWN";
                result.NeedsCrsResolution = true;
            }

            bool isWgs84 = result.SourceCrs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase);

            using var fs = File.OpenRead(shpPath);
            using var br = new BinaryReader(fs);

            // Read Shapefile Header (100 bytes)
            if (fs.Length < 100)
            {
                result.Success = false;
                result.ErrorMessage = "Corrupt Shapefile: File length is less than 100 bytes";
                return result;
            }

            // Big-Endian File Code (must be 9994)
            int fileCode = ReadInt32BigEndian(br);
            if (fileCode != 9994)
            {
                result.Success = false;
                result.ErrorMessage = $"Invalid Shapefile FileCode: {fileCode} (expected 9994)";
                return result;
            }

            // Skip 5 unused 32-bit ints
            br.ReadBytes(20);
            int fileLengthWords = ReadInt32BigEndian(br);
            int version = br.ReadInt32(); // 1000
            int shapeType = br.ReadInt32(); // 1=Point, 3=PolyLine, 5=Polygon

            // Bounding box (8 doubles)
            double xmin = br.ReadDouble();
            double ymin = br.ReadDouble();
            double xmax = br.ReadDouble();
            double ymax = br.ReadDouble();
            br.ReadBytes(32); // zmin, zmax, mmin, mmax

            int recordIndex = 1;
            while (fs.Position < fs.Length)
            {
                try
                {
                    int recordNumber = ReadInt32BigEndian(br);
                    int contentLengthWords = ReadInt32BigEndian(br);
                    long recordEndPos = fs.Position + (contentLengthWords * 2);

                    int recShapeType = br.ReadInt32();
                    if (recShapeType == 0) // Null shape
                    {
                        fs.Seek(recordEndPos, SeekOrigin.Begin);
                        continue;
                    }

                    var item = new PlaceItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = $"Shape #{recordNumber}"
                    };
                    item.ExtendedData["SourceCrs"] = result.SourceCrs;

                    if (recShapeType == 1) // Point
                    {
                        item.ItemType = PlaceItemType.Placemark;
                        double x = br.ReadDouble();
                        double y = br.ReadDouble();
                        var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, result.SourceCrs);
                        item.Longitude = lon;
                        item.Latitude = lat;
                        result.UpdateBounds(lon, lat);
                        result.Features.Add(item);
                    }
                    else if (recShapeType == 3 || recShapeType == 5) // PolyLine or Polygon
                    {
                        item.ItemType = (recShapeType == 5) ? PlaceItemType.Polygon : PlaceItemType.Path;
                        double bxMin = br.ReadDouble();
                        double byMin = br.ReadDouble();
                        double bxMax = br.ReadDouble();
                        double byMax = br.ReadDouble();
                        int numParts = br.ReadInt32();
                        int numPoints = br.ReadInt32();

                        int[] parts = new int[numParts];
                        for (int p = 0; p < numParts; p++) parts[p] = br.ReadInt32();

                        var allPoints = new List<CoordinatePoint>(numPoints);
                        for (int pt = 0; pt < numPoints; pt++)
                        {
                            double px = br.ReadDouble();
                            double py = br.ReadDouble();
                            var (lon, lat) = isWgs84 ? (px, py) : ReprojectToWgs84(px, py, result.SourceCrs);
                            allPoints.Add(new CoordinatePoint(lon, lat, 0.0));
                            result.UpdateBounds(lon, lat);
                        }

                        if (numParts <= 1)
                        {
                            item.Coordinates = allPoints;
                        }
                        else
                        {
                            // Part 0 is exterior
                            int part0End = parts[1];
                            item.Coordinates = allPoints.GetRange(0, part0End);
                            for (int p = 1; p < numParts; p++)
                            {
                                int start = parts[p];
                                int count = (p == numParts - 1) ? numPoints - start : parts[p + 1] - start;
                                item.InnerRings.Add(allPoints.GetRange(start, count));
                            }
                        }

                        result.Features.Add(item);
                    }

                    fs.Seek(recordEndPos, SeekOrigin.Begin);
                    recordIndex++;
                }
                catch
                {
                    break;
                }
            }

            return result;
        }

        public static VectorImportResult ReadGeoPackage(string gpkgPath, string? overrideCrs = null)
        {
            var result = new VectorImportResult { SourceFormat = "OGC GeoPackage", Success = true };

            // 1. Authoritative OGR GeoPackage driver parsing (ZE-053, AC-007)
            GdalConfiguration.EnsureInitialized();
            if (GdalConfiguration.IsAvailable)
            {
                try
                {
                    using var ds = Ogr.Open(gpkgPath, 0);
                    if (ds != null)
                    {
                        int layerCount = ds.GetLayerCount();
                        for (int l = 0; l < layerCount; l++)
                        {
                            using var layer = ds.GetLayerByIndex(l);
                            if (layer == null) continue;

                            string detectedCrs = "EPSG:4326";
                            var srs = layer.GetSpatialRef();
                            if (srs != null)
                            {
                                string? authCode = srs.GetAuthorityCode(null);
                                if (!string.IsNullOrEmpty(authCode))
                                {
                                    detectedCrs = $"EPSG:{authCode}";
                                }
                            }

                            string effectiveCrs = overrideCrs ?? detectedCrs;
                            result.SourceCrs = effectiveCrs;
                            bool isWgs84 = effectiveCrs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase) ||
                                           effectiveCrs.Equals("CRS84", StringComparison.OrdinalIgnoreCase);

                            int featureIndex = 1;
                            layer.ResetReading();
                            Feature feat;
                            while ((feat = layer.GetNextFeature()) != null)
                            {
                                using (feat)
                                {
                                    var geom = feat.GetGeometryRef();
                                    if (geom == null) continue;

                                    string name = $"Feature #{featureIndex++}";
                                    var fieldCount = feat.GetFieldCount();
                                    var extData = new Dictionary<string, string>();
                                    for (int f = 0; f < fieldCount; f++)
                                    {
                                        using var fDef = feat.GetFieldDefnRef(f);
                                        string fName = fDef.GetName();
                                        string fVal = feat.GetFieldAsString(f);
                                        extData[fName] = fVal;
                                        if (fName.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                                            fName.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                                            fName.Equals("NAME", StringComparison.OrdinalIgnoreCase))
                                        {
                                            name = fVal;
                                        }
                                    }

                                    var item = CreatePlaceItemFromOgrGeometry(geom, name, effectiveCrs, isWgs84, result);
                                    if (item != null)
                                    {
                                        foreach (var kvp in extData)
                                        {
                                            item.ExtendedData[kvp.Key] = kvp.Value;
                                        }
                                        result.Features.Add(item);
                                    }
                                }
                            }
                        }

                        if (result.Features.Count > 0)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OGR GPKG error: {ex.Message}");
                }
            }

            // Fallback SQLite / folder representation if dataset has no vector layers
            if (File.Exists(gpkgPath) && result.Features.Count == 0)
            {
                var rootFolder = new PlaceItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = Path.GetFileNameWithoutExtension(gpkgPath),
                    ItemType = PlaceItemType.Folder
                };
                rootFolder.ExtendedData["SourceCrs"] = result.SourceCrs;
                result.Features.Add(rootFolder);
            }

            return result;
        }

        public static VectorImportResult ReadGeoTiff(string tiffPath, string? overrideCrs = null)
        {
            var result = new VectorImportResult { SourceFormat = "GeoTIFF", Success = true };

            GdalConfiguration.EnsureInitialized();
            if (GdalConfiguration.IsAvailable)
            {
                try
                {
                    using var ds = Gdal.Open(tiffPath, Access.GA_ReadOnly);
                    if (ds != null)
                    {
                        int width = ds.RasterXSize;
                        int height = ds.RasterYSize;
                        int bands = ds.RasterCount;

                        double[] geoTransform = new double[6];
                        ds.GetGeoTransform(geoTransform);

                        string projWkt = ds.GetProjectionRef();
                        string detectedCrs = "EPSG:4326";
                        if (!string.IsNullOrEmpty(projWkt))
                        {
                            using var srs = new SpatialReference(projWkt);
                            string? auth = srs.GetAuthorityCode(null);
                            if (!string.IsNullOrEmpty(auth)) detectedCrs = $"EPSG:{auth}";
                        }

                        result.SourceCrs = overrideCrs ?? detectedCrs;
                        bool isWgs84 = result.SourceCrs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase);

                        // Compute bounding box coordinates in source CRS
                        double xOrigin = geoTransform[0];
                        double yOrigin = geoTransform[3];
                        double pixelWidth = geoTransform[1];
                        double pixelHeight = geoTransform[5]; // typically negative

                        double xMax = xOrigin + (width * pixelWidth);
                        double yMin = yOrigin + (height * pixelHeight);

                        var (lon1, lat1) = isWgs84 ? (xOrigin, yMin) : ReprojectToWgs84(xOrigin, yMin, result.SourceCrs);
                        var (lon2, lat2) = isWgs84 ? (xMax, yOrigin) : ReprojectToWgs84(xMax, yOrigin, result.SourceCrs);

                        double minLon = Math.Min(lon1, lon2);
                        double maxLon = Math.Max(lon1, lon2);
                        double minLat = Math.Min(lat1, lat2);
                        double maxLat = Math.Max(lat1, lat2);

                        result.UpdateBounds(minLon, minLat);
                        result.UpdateBounds(maxLon, maxLat);

                        var item = new PlaceItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Name = Path.GetFileNameWithoutExtension(tiffPath),
                            ItemType = PlaceItemType.GroundOverlay,
                            Longitude = (minLon + maxLon) / 2.0,
                            Latitude = (minLat + maxLat) / 2.0
                        };

                        item.ExtendedData["RasterWidth"] = width.ToString();
                        item.ExtendedData["RasterHeight"] = height.ToString();
                        item.ExtendedData["RasterBands"] = bands.ToString();
                        item.ExtendedData["PixelSizeX"] = pixelWidth.ToString();
                        item.ExtendedData["PixelSizeY"] = pixelHeight.ToString();
                        item.ExtendedData["WestLon"] = minLon.ToString("F6");
                        item.ExtendedData["EastLon"] = maxLon.ToString("F6");
                        item.ExtendedData["SouthLat"] = minLat.ToString("F6");
                        item.ExtendedData["NorthLat"] = maxLat.ToString("F6");
                        item.ExtendedData["SourceCrs"] = result.SourceCrs;

                        // Create bounding polygon
                        item.Coordinates = new List<CoordinatePoint>
                        {
                            new(minLon, maxLat, 0),
                            new(maxLon, maxLat, 0),
                            new(maxLon, minLat, 0),
                            new(minLon, minLat, 0),
                            new(minLon, maxLat, 0)
                        };

                        result.Features.Add(item);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to read GeoTIFF: {ex.Message}";
                    return result;
                }
            }

            result.Success = false;
            result.ErrorMessage = "GDAL backend not available to parse GeoTIFF";
            return result;
        }

        private static PlaceItem? CreatePlaceItemFromOgrGeometry(Geometry geom, string name, string sourceCrs, bool isWgs84, VectorImportResult result)
        {
            var geomType = geom.GetGeometryType();
            var item = new PlaceItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name
            };
            item.ExtendedData["SourceCrs"] = sourceCrs;

            if (geomType == wkbGeometryType.wkbPoint || geomType == wkbGeometryType.wkbPoint25D)
            {
                item.ItemType = PlaceItemType.Placemark;
                double x = geom.GetX(0);
                double y = geom.GetY(0);
                double z = geom.GetCoordinateDimension() > 2 ? geom.GetZ(0) : 0.0;
                var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                item.Longitude = lon;
                item.Latitude = lat;
                item.Altitude = z;
                result.UpdateBounds(lon, lat);
                return item;
            }
            else if (geomType == wkbGeometryType.wkbLineString || geomType == wkbGeometryType.wkbLineString25D)
            {
                item.ItemType = PlaceItemType.Path;
                int count = geom.GetPointCount();
                for (int i = 0; i < count; i++)
                {
                    double x = geom.GetX(i);
                    double y = geom.GetY(i);
                    double z = geom.GetCoordinateDimension() > 2 ? geom.GetZ(i) : 0.0;
                    var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                    item.Coordinates.Add(new CoordinatePoint(lon, lat, z));
                    result.UpdateBounds(lon, lat);
                }
                return item;
            }
            else if (geomType == wkbGeometryType.wkbPolygon || geomType == wkbGeometryType.wkbPolygon25D)
            {
                item.ItemType = PlaceItemType.Polygon;
                int ringCount = geom.GetGeometryCount();
                for (int r = 0; r < ringCount; r++)
                {
                    using var ring = geom.GetGeometryRef(r);
                    if (ring == null) continue;
                    int count = ring.GetPointCount();
                    var ringPoints = new List<CoordinatePoint>(count);
                    for (int i = 0; i < count; i++)
                    {
                        double x = ring.GetX(i);
                        double y = ring.GetY(i);
                        double z = geom.GetCoordinateDimension() > 2 ? ring.GetZ(i) : 0.0;
                        var (lon, lat) = isWgs84 ? (x, y) : ReprojectToWgs84(x, y, sourceCrs);
                        ringPoints.Add(new CoordinatePoint(lon, lat, z));
                        result.UpdateBounds(lon, lat);
                    }

                    if (r == 0)
                    {
                        item.Coordinates = ringPoints;
                    }
                    else
                    {
                        item.InnerRings.Add(ringPoints);
                    }
                }
                return item;
            }
            return null;
        }

        private static int ReadInt32BigEndian(BinaryReader br)
        {
            byte[] bytes = br.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        private static string ParsePrjCrs(string prj)
        {
            if (string.IsNullOrWhiteSpace(prj)) return "EPSG:4326";
            if (prj.Contains("32636") || prj.Contains("UTM zone 36N")) return "EPSG:32636";
            if (prj.Contains("32635") || prj.Contains("UTM zone 35N")) return "EPSG:32635";
            if (prj.Contains("Egypt_Red_Belt") || prj.Contains("22992")) return "EPSG:22992";
            if (prj.Contains("Egypt_Purple_Belt") || prj.Contains("22993")) return "EPSG:22993";
            if (prj.Contains("WGS_1984_Web_Mercator") || prj.Contains("3857")) return "EPSG:3857";
            return "EPSG:4326";
        }

        public static (double lon, double lat) ReprojectToWgs84(double x, double y, string sourceCrs)
        {
            if (string.IsNullOrWhiteSpace(sourceCrs) ||
                sourceCrs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase) ||
                sourceCrs.Equals("CRS84", StringComparison.OrdinalIgnoreCase) ||
                sourceCrs.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                return (x, y);
            }

            if (sourceCrs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(sourceCrs.Substring(5), out int epsg))
            {
                try
                {
                    var res = CoordinateTransformService.Transform(x, y, epsg, 4326);
                    return (res.X, res.Y);
                }
                catch
                {
                    return (x, y);
                }
            }

            return (x, y);
        }
    }
}
