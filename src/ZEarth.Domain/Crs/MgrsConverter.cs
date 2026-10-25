using System;

namespace ZEarth.Domain.Crs
{
    public static class MgrsConverter
    {
        private static readonly string LATITUDE_BANDS = "CDEFGHJKLMNPQRSTUVWX";
        private static readonly string[] COLUMN_SETS = new[]
        {
            "STUVWXYZ", // Set 3 (zone % 3 == 0)
            "ABCDEFGH", // Set 1 (zone % 3 == 1)
            "JKLMNPQR"  // Set 2 (zone % 3 == 2)
        };
        private static readonly string ROW_LETTERS = "ABCDEFGHJKLMNPQRSTUV";

        /// <summary>
        /// Gets the MGRS latitude band letter for a given geodetic latitude (-80° to +84°).
        /// </summary>
        public static char GetLatitudeBand(double latitude)
        {
            if (latitude < -80.0 || latitude > 84.0)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude), "MGRS coordinates are only defined between 80°S and 84°N.");
            }

            if (latitude >= 72.0) return 'X';
            int bandIndex = (int)((latitude + 80.0) / 8.0);
            if (bandIndex < 0) bandIndex = 0;
            if (bandIndex >= LATITUDE_BANDS.Length) bandIndex = LATITUDE_BANDS.Length - 1;
            return LATITUDE_BANDS[bandIndex];
        }

        /// <summary>
        /// Converts WGS84 geographic coordinates (lon, lat) to an authoritative MGRS string.
        /// Precision: 5 = 1m (10 digits), 4 = 10m (8 digits), 3 = 100m (6 digits), 2 = 1km (4 digits), 1 = 10km (2 digits), 0 = 100km square.
        /// </summary>
        public static string FromLatLon(double lon, double lat, int precision = 5)
        {
            var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
            char band = GetLatitudeBand(lat);
            return FromUtm(utm.Zone, band, utm.Easting, utm.Northing, precision);
        }

        public static string FromWgs84(double lon, double lat, int precision = 5) => FromLatLon(lon, lat, precision);

        /// <summary>
        /// Converts UTM coordinates and latitude band to an authoritative MGRS string.
        /// </summary>
        public static string FromUtm(int zone, char band, double easting, double northing, int precision = 5)
        {
            if (precision < 0) precision = 0;
            if (precision > 5) precision = 5;

            // 1. Grid Zone Designation (GZD)
            string gzd = $"{zone}{band}";

            // 2. 100,000-meter Square Identification
            int setIndex = zone % 3; // 0 -> Set 3, 1 -> Set 1, 2 -> Set 2
            string colSet = COLUMN_SETS[setIndex];
            int colIndex = (int)(easting / 100000.0) - 1;
            if (colIndex < 0) colIndex = 0;
            if (colIndex >= colSet.Length) colIndex = colSet.Length - 1;
            char colLetter = colSet[colIndex];

            int rowBaseOffset = (zone % 2 == 1) ? 0 : 5; // Odd zones offset 0, Even zones offset 5
            int rowIndex = (rowBaseOffset + (int)((northing % 2000000.0) / 100000.0)) % 20;
            if (rowIndex < 0) rowIndex += 20;
            char rowLetter = ROW_LETTERS[rowIndex];

            string square100k = $"{colLetter}{rowLetter}";

            if (precision == 0)
            {
                return $"{gzd}{square100k}";
            }

            // 3. Coordinate Truncation
            double remE = easting % 100000.0;
            double remN = northing % 100000.0;

            int divisor = (int)Math.Pow(10, 5 - precision);
            int eTrunc = (int)(remE / divisor);
            int nTrunc = (int)(remN / divisor);

            string format = $"D{precision}";
            return $"{gzd}{square100k}{eTrunc.ToString(format)}{nTrunc.ToString(format)}";
        }
    }
}
