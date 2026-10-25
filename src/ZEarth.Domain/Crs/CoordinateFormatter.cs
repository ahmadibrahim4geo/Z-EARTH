using System;
using System.Globalization;

namespace ZEarth.Domain.Crs
{
    public enum CoordinateDisplayFormat
    {
        DecimalDegrees,
        DegreesMinutesSeconds,
        Utm,
        Mgrs
    }

    public static class CoordinateFormatter
    {
        public static string Format(double lon, double lat, CoordinateDisplayFormat format, int precision = 5)
        {
            switch (format)
            {
                case CoordinateDisplayFormat.DecimalDegrees:
                    return ToDecimalDegrees(lon, lat);

                case CoordinateDisplayFormat.DegreesMinutesSeconds:
                    return ToDms(lon, lat);

                case CoordinateDisplayFormat.Utm:
                    return ToUtmString(lon, lat);

                case CoordinateDisplayFormat.Mgrs:
                    return ToMgrsString(lon, lat, precision);

                default:
                    return ToDecimalDegrees(lon, lat);
            }
        }

        public static string ToDecimalDegrees(double lon, double lat)
        {
            char latHem = lat >= 0.0 ? 'N' : 'S';
            char lonHem = lon >= 0.0 ? 'E' : 'W';
            return string.Format(CultureInfo.InvariantCulture, "{0:F4}° {1} | {2:F4}° {3}", Math.Abs(lat), latHem, Math.Abs(lon), lonHem);
        }

        public static string ToDms(double lon, double lat)
        {
            string FormatDmsPart(double deg, char posHem, char negHem)
            {
                char hem = deg >= 0 ? posHem : negHem;
                double abs = Math.Abs(deg);
                int d = (int)abs;
                double mDouble = (abs - d) * 60.0;
                int m = (int)mDouble;
                double s = (mDouble - m) * 60.0;
                return string.Format(CultureInfo.InvariantCulture, "{0:D2}° {1:D2}' {2:F1}\" {3}", d, m, s, hem);
            }

            return $"{FormatDmsPart(lat, 'N', 'S')} | {FormatDmsPart(lon, 'E', 'W')}";
        }

        public static string ToUtmString(double lon, double lat)
        {
            var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
            return string.Format(CultureInfo.InvariantCulture, "UTM Zone {0}{1} | {2:F0}m E | {3:F0}m N",
                utm.Zone,
                utm.IsNorthernHemisphere ? "N" : "S",
                utm.Easting,
                utm.Northing);
        }

        public static string ToMgrsString(double lon, double lat, int precision = 5)
        {
            string raw = MgrsConverter.FromLatLon(lon, lat, precision);
            if (raw.Length >= 5)
            {
                string gzd = raw.Substring(0, 3);
                string sq = raw.Substring(3, 2);
                string coords = raw.Substring(5);
                if (coords.Length > 0 && coords.Length % 2 == 0)
                {
                    int half = coords.Length / 2;
                    string e = coords.Substring(0, half);
                    string n = coords.Substring(half);
                    return $"MGRS {gzd} {sq} {e} {n}";
                }
                return $"MGRS {gzd} {sq}";
            }
            return $"MGRS {raw}";
        }
    }
}
