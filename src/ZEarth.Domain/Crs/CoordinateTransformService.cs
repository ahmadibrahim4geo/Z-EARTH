using System;
using System.Collections.Generic;
using OSGeo.OSR;
using ZEarth.Domain.Gis;

namespace ZEarth.Domain.Crs
{
    public struct UtmResult
    {
        public int Zone { get; set; }
        public bool IsNorthernHemisphere { get; set; }
        public double Easting { get; set; }
        public double Northing { get; set; }

        public UtmResult(int zone, bool isNorth, double easting, double northing)
        {
            Zone = zone;
            IsNorthernHemisphere = isNorth;
            Easting = easting;
            Northing = northing;
        }

        public override string ToString() => $"{Zone}{(IsNorthernHemisphere ? "N" : "S")} {Easting:F1}m E, {Northing:F1}m N";
    }

    /// <summary>
    /// Authoritative Transverse Mercator and UTM transformations implementing Krüger's n-series (PROJ / GeographicLib compatible).
    /// </summary>
    public static class CoordinateTransformService
    {
        private const double WGS84_A = 6378137.0; // semi-major axis (meters)
        private const double WGS84_F = 1.0 / 298.257223563; // flattening
        private const double WGS84_B = WGS84_A * (1.0 - WGS84_F);
        private const double UTM_K0 = 0.9996; // scale factor
        private const double FALSE_EASTING = 500000.0;

        // Krüger coefficients for WGS84
        private static readonly double N_PARAM = WGS84_F / (2.0 - WGS84_F);
        private static readonly double N2 = N_PARAM * N_PARAM;
        private static readonly double N3 = N2 * N_PARAM;
        private static readonly double N4 = N3 * N_PARAM;

        // Rectified radius A_hat
        private static readonly double A_HAT = (WGS84_A / (1.0 + N_PARAM)) * (1.0 + 0.25 * N2 + (1.0 / 64.0) * N4);

        // Forward Krüger alpha coefficients
        private static readonly double ALPHA1 = 0.5 * N_PARAM - (2.0 / 3.0) * N2 + (5.0 / 16.0) * N3 + (41.0 / 180.0) * N4;
        private static readonly double ALPHA2 = (13.0 / 48.0) * N2 - (3.0 / 5.0) * N3 + (557.0 / 1440.0) * N4;
        private static readonly double ALPHA3 = (61.0 / 240.0) * N3 - (103.0 / 140.0) * N4;
        private static readonly double ALPHA4 = (49561.0 / 161280.0) * N4;

        // Reverse Krüger beta coefficients
        private static readonly double BETA1 = 0.5 * N_PARAM - (2.0 / 3.0) * N2 + (37.0 / 96.0) * N3 - (1.0 / 360.0) * N4;
        private static readonly double BETA2 = (1.0 / 48.0) * N2 + (1.0 / 15.0) * N3 - (437.0 / 1440.0) * N4;
        private static readonly double BETA3 = (17.0 / 480.0) * N3 - (37.0 / 840.0) * N4;
        private static readonly double BETA4 = (4397.0 / 161280.0) * N4;

        private static readonly double ECC = Math.Sqrt(2.0 * WGS84_F - WGS84_F * WGS84_F);

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

        /// <summary>
        /// Converts WGS 84 geographic coordinates (longitude, latitude) into UTM projection coordinates using Krüger series.
        /// </summary>
        public static UtmResult Wgs84ToUtm(double lon, double lat, int? overrideZone = null)
        {
            int zone = overrideZone ?? (int)Math.Floor((lon + 180.0) / 6.0) + 1;
            if (zone < 1) zone = 1;
            if (zone > 60) zone = 60;

            bool isNorth = lat >= 0.0;
            double centralMeridian = (zone - 1) * 6 - 180 + 3;

            double phi = ToRadians(lat);
            double dLambda = ToRadians(lon - centralMeridian);

            // Conformal / isometric latitude psi
            double sinPhi = Math.Sin(phi);
            double psi = Math.Log(Math.Tan(Math.PI / 4.0 + phi / 2.0))
                       - ECC * Math.Log(Math.Tan(Math.PI / 4.0 + Math.Asin(ECC * sinPhi) / 2.0));

            double xiPrime = Math.Atan2(Math.Sinh(psi), Math.Cos(dLambda));
            double etaPrime = 0.5 * Math.Log((1.0 + Math.Sin(dLambda) / Math.Cosh(psi)) / (1.0 - Math.Sin(dLambda) / Math.Cosh(psi)));

            double xi = xiPrime;
            double eta = etaPrime;

            xi += ALPHA1 * Math.Sin(2.0 * xiPrime) * Math.Cosh(2.0 * etaPrime);
            eta += ALPHA1 * Math.Cos(2.0 * xiPrime) * Math.Sinh(2.0 * etaPrime);

            xi += ALPHA2 * Math.Sin(4.0 * xiPrime) * Math.Cosh(4.0 * etaPrime);
            eta += ALPHA2 * Math.Cos(4.0 * xiPrime) * Math.Sinh(4.0 * etaPrime);

            xi += ALPHA3 * Math.Sin(6.0 * xiPrime) * Math.Cosh(6.0 * etaPrime);
            eta += ALPHA3 * Math.Cos(6.0 * xiPrime) * Math.Sinh(6.0 * etaPrime);

            xi += ALPHA4 * Math.Sin(8.0 * xiPrime) * Math.Cosh(8.0 * etaPrime);
            eta += ALPHA4 * Math.Cos(8.0 * xiPrime) * Math.Sinh(8.0 * etaPrime);

            double easting = UTM_K0 * A_HAT * eta + FALSE_EASTING;
            double northing = UTM_K0 * A_HAT * xi;

            if (!isNorth)
            {
                northing += 10000000.0;
            }

            return new UtmResult(zone, isNorth, easting, northing);
        }

        /// <summary>
        /// Converts UTM coordinates into WGS 84 geographic coordinates using inverse Krüger series.
        /// </summary>
        public static (double Longitude, double Latitude) UtmToWgs84(double easting, double northing, int zone, bool isNorth)
        {
            double x = easting - FALSE_EASTING;
            double y = isNorth ? northing : northing - 10000000.0;

            double xi = y / (UTM_K0 * A_HAT);
            double eta = x / (UTM_K0 * A_HAT);

            double xiPrime = xi;
            double etaPrime = eta;

            xiPrime -= BETA1 * Math.Sin(2.0 * xi) * Math.Cosh(2.0 * eta);
            etaPrime -= BETA1 * Math.Cos(2.0 * xi) * Math.Sinh(2.0 * eta);

            xiPrime -= BETA2 * Math.Sin(4.0 * xi) * Math.Cosh(4.0 * eta);
            etaPrime -= BETA2 * Math.Cos(4.0 * xi) * Math.Sinh(4.0 * eta);

            xiPrime -= BETA3 * Math.Sin(6.0 * xi) * Math.Cosh(6.0 * eta);
            etaPrime -= BETA3 * Math.Cos(6.0 * xi) * Math.Sinh(6.0 * eta);

            xiPrime -= BETA4 * Math.Sin(8.0 * xi) * Math.Cosh(8.0 * eta);
            etaPrime -= BETA4 * Math.Cos(8.0 * xi) * Math.Sinh(8.0 * eta);

            double sinhEtaPrime = Math.Sinh(etaPrime);
            double cosXiPrime = Math.Cos(xiPrime);
            double sinXiPrime = Math.Sin(xiPrime);
            double coshEtaPrime = Math.Cosh(etaPrime);

            double dLambda = Math.Atan2(sinhEtaPrime, cosXiPrime);
            double psi = Math.Asinh(sinXiPrime / Math.Sqrt(cosXiPrime * cosXiPrime + sinhEtaPrime * sinhEtaPrime));

            // Invert conformal latitude to geodetic latitude using Newton-Raphson iteration
            double phi = psi;
            double e2 = ECC * ECC;
            for (int i = 0; i < 6; i++)
            {
                double sinP = Math.Sin(phi);
                double cosP = Math.Cos(phi);
                double psiCalc = Math.Log(Math.Tan(Math.PI / 4.0 + phi / 2.0))
                               - ECC * Math.Log(Math.Tan(Math.PI / 4.0 + Math.Asin(ECC * sinP) / 2.0));

                double dPsiDPhi = (1.0 - e2) / (cosP * (1.0 - e2 * sinP * sinP));
                double delta = (psi - psiCalc) / dPsiDPhi;
                phi += delta;
                if (Math.Abs(delta) < 1e-13) break;
            }

            double centralMeridian = (zone - 1) * 6 - 180 + 3;
            double lon = centralMeridian + ToDegrees(dLambda);
            double lat = ToDegrees(phi);

            return (lon, lat);
        }

        public static (double X, double Y) Wgs84ToWebMercator(double lon, double lat)
        {
            double x = WGS84_A * ToRadians(lon);
            lat = Math.Max(-85.05112878, Math.Min(85.05112878, lat));
            double y = WGS84_A * Math.Log(Math.Tan(Math.PI / 4.0 + ToRadians(lat) / 2.0));
            return (x, y);
        }

        public static (double Longitude, double Latitude) WebMercatorToWgs84(double x, double y)
        {
            double lon = ToDegrees(x / WGS84_A);
            double lat = ToDegrees(2.0 * Math.Atan(Math.Exp(y / WGS84_A)) - Math.PI / 2.0);
            return (lon, lat);
        }

        /// <summary>
        /// Holds a reusable PROJ transformation. Building a <see cref="SpatialReference"/> /
        /// <see cref="CoordinateTransformation"/> pair is expensive, so it is created once per CRS
        /// pair and reused for every cursor/camera sample (RC-2). Native transforms are not
        /// thread-safe, hence the per-entry gate.
        /// </summary>
        private sealed class CachedTransform
        {
            public SpatialReference Source = null!;
            public SpatialReference Target = null!;
            public CoordinateTransformation Transform = null!;
            public readonly object Gate = new();
        }

        private static readonly object TransformCacheGate = new();
        private static readonly Dictionary<(int From, int To), CachedTransform?> TransformCache = new();

        private static CachedTransform? TryGetCachedTransform(int fromEpsg, int toEpsg)
        {
            var key = (fromEpsg, toEpsg);
            lock (TransformCacheGate)
            {
                if (TransformCache.TryGetValue(key, out var existing)) return existing;

                CachedTransform? created = null;
                try
                {
                    GdalConfiguration.EnsureInitialized();
                    if (GdalConfiguration.IsAvailable)
                    {
                        var source = new SpatialReference("");
                        if (source.ImportFromEPSG(fromEpsg) == 0)
                        {
                            source.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
                            var target = new SpatialReference("");
                            if (target.ImportFromEPSG(toEpsg) == 0)
                            {
                                target.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
                                created = new CachedTransform
                                {
                                    Source = source,
                                    Target = target,
                                    Transform = new CoordinateTransformation(source, target)
                                };
                            }
                            else
                            {
                                target.Dispose();
                                source.Dispose();
                            }
                        }
                        else
                        {
                            source.Dispose();
                        }
                    }
                }
                catch
                {
                    created = null;
                }

                TransformCache[key] = created;
                return created;
            }
        }

        /// <summary>
        /// Eagerly builds (and caches) the transformation for a CRS pair so that later per-sample
        /// conversions never pay initialization cost on the UI thread.
        /// </summary>
        public static void Warmup(int fromEpsg, int toEpsg)
        {
            if (fromEpsg != toEpsg)
            {
                _ = TryGetCachedTransform(fromEpsg, toEpsg);
            }
        }

        /// <summary>
        /// Fast, cache-backed transform intended for high-frequency display paths. Falls back to the
        /// mathematical implementation when PROJ is unavailable or the CRS pair is unsupported.
        /// </summary>
        public static (double X, double Y) TransformFast(double fromX, double fromY, int fromEpsg, int toEpsg)
        {
            if (fromEpsg == toEpsg) return (fromX, fromY);

            var cached = TryGetCachedTransform(fromEpsg, toEpsg);
            if (cached != null)
            {
                try
                {
                    lock (cached.Gate)
                    {
                        double[] pt = new double[] { fromX, fromY, 0.0 };
                        cached.Transform.TransformPoint(pt);
                        return (pt[0], pt[1]);
                    }
                }
                catch
                {
                    // Fall through to the mathematical path below
                }
            }

            return Transform(fromX, fromY, fromEpsg, toEpsg);
        }

        public static (double X, double Y) Transform(double fromX, double fromY, int fromEpsg, int toEpsg)
        {
            if (fromEpsg == toEpsg) return (fromX, fromY);

            // 1. Authoritative PROJ transformation via OSR (ADR-003, DEC-CRS-01, ZE-031)
            GdalConfiguration.EnsureInitialized();
            if (GdalConfiguration.IsAvailable)
            {
                try
                {
                    using var srsSource = new SpatialReference("");
                    int retSource = srsSource.ImportFromEPSG(fromEpsg);
                    if (retSource == 0)
                    {
                        srsSource.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

                        using var srsTarget = new SpatialReference("");
                        int retTarget = srsTarget.ImportFromEPSG(toEpsg);
                        if (retTarget == 0)
                        {
                            srsTarget.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

                            using var transform = new CoordinateTransformation(srsSource, srsTarget);
                            double[] pt = new double[] { fromX, fromY, 0.0 };
                            transform.TransformPoint(pt);
                            return (pt[0], pt[1]);
                        }
                    }
                }
                catch
                {
                    // Fall back to Krüger series implementation below
                }
            }

            // 2. High-precision fallback mathematical transformations (Krüger n-series)
            double lon, lat;
            if (fromEpsg == 4326 || fromEpsg == 4979)
            {
                lon = fromX;
                lat = fromY;
            }
            else if (fromEpsg == 3857)
            {
                var wgs = WebMercatorToWgs84(fromX, fromY);
                lon = wgs.Longitude;
                lat = wgs.Latitude;
            }
            else if (fromEpsg >= 32601 && fromEpsg <= 32660)
            {
                int zone = fromEpsg - 32600;
                var wgs = UtmToWgs84(fromX, fromY, zone, true);
                lon = wgs.Longitude;
                lat = wgs.Latitude;
            }
            else if (fromEpsg >= 32701 && fromEpsg <= 32760)
            {
                int zone = fromEpsg - 32700;
                var wgs = UtmToWgs84(fromX, fromY, zone, false);
                lon = wgs.Longitude;
                lat = wgs.Latitude;
            }
            else if (fromEpsg == 22992)
            {
                // Egypt Red Belt (Helmert 1906 / TM, origin 31°E, 30°N, False Easting 615000, False Northing 810000)
                double dx = fromX - 615000.0;
                double dy = fromY - 810000.0;
                double latRad = 30.0 * Math.PI / 180.0;
                double mPerDegLat = 110850.0;
                double mPerDegLon = 111320.0 * Math.Cos(latRad);
                lon = 31.0 + (dx / mPerDegLon);
                lat = 30.0 + (dy / mPerDegLat);
            }
            else
            {
                throw new NotSupportedException($"Source EPSG:{fromEpsg} transformation is not configured.");
            }

            if (toEpsg == 4326 || toEpsg == 4979)
            {
                return (lon, lat);
            }
            else if (toEpsg == 3857)
            {
                return Wgs84ToWebMercator(lon, lat);
            }
            else if (toEpsg >= 32601 && toEpsg <= 32660)
            {
                int zone = toEpsg - 32600;
                var utm = Wgs84ToUtm(lon, lat, zone);
                return (utm.Easting, utm.Northing);
            }
            else if (toEpsg >= 32701 && toEpsg <= 32760)
            {
                int zone = toEpsg - 32700;
                var utm = Wgs84ToUtm(lon, lat, zone);
                return (utm.Easting, utm.Northing);
            }

            throw new NotSupportedException($"Target EPSG:{toEpsg} transformation is not configured.");
        }
    }
}
