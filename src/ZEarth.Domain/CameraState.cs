using System;
using System.Text.Json.Serialization;

namespace ZEarth.Domain
{
    public enum HomePreset
    {
        EgyptAfrica,
        World,
        Custom
    }

    /// <summary>
    /// Represents the geographic camera perspective in Z EARTH Desktop.
    /// </summary>
    public sealed class CameraState
    {
        public const double MinAltitudeMeters = 50.0;
        public const double MaxAltitudeMeters = 60_000_000.0; // 60,000 km
        public const double DefaultLatitude = 26.8206;  // Regional Home: Egypt / North Africa
        public const double DefaultLongitude = 30.8025;
        public const double DefaultAltitude = 4_500_000.0; // Regional continent overview

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }

        [JsonPropertyName("heading")]
        public double Heading { get; set; } // 0° = North, clockwise

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; } // -90° = Nadir (straight down), 0° = Horizon

        [JsonPropertyName("roll")]
        public double Roll { get; set; }

        public CameraState()
        {
            Latitude = DefaultLatitude;
            Longitude = DefaultLongitude;
            Altitude = DefaultAltitude;
            Heading = 0.0;
            Pitch = -90.0;
            Roll = 0.0;
        }

        public CameraState(double latitude, double longitude, double altitude, double heading = 0.0, double pitch = -90.0, double roll = 0.0)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
            Heading = heading;
            Pitch = pitch;
            Roll = roll;
            Sanitize();
        }

        public static CameraState CreateHome(HomePreset preset = HomePreset.EgyptAfrica)
        {
            return preset switch
            {
                HomePreset.World => CreateFullExtent(),
                HomePreset.EgyptAfrica or _ => new CameraState(DefaultLatitude, DefaultLongitude, DefaultAltitude, 0.0, -90.0, 0.0)
            };
        }

        public static CameraState CreateFullExtent()
        {
            return new CameraState(20.0, 15.0, 22_000_000.0, 0.0, -90.0, 0.0);
        }

        /// <summary>
        /// Computes camera position framing a bounding box, supporting antimeridian (dateline) crossing.
        /// </summary>
        public static CameraState CreateForBoundingBox(double west, double south, double east, double north)
        {
            double centerLat = (south + north) / 2.0;
            double centerLon;

            // Handle antimeridian crossing (west > east)
            if (west > east)
            {
                double spanLon = (180.0 - west) + (east - (-180.0));
                centerLon = west + (spanLon / 2.0);
                if (centerLon > 180.0) centerLon -= 360.0;
            }
            else
            {
                centerLon = (west + east) / 2.0;
            }

            double spanLat = Math.Max(0.01, Math.Abs(north - south));
            double spanLonActual = west > east ? (360.0 - west + east) : Math.Max(0.01, Math.Abs(east - west));
            double maxSpanDeg = Math.Max(spanLat, spanLonActual);

            // Approximate altitude to frame degree span (~111 km per degree, with 2.5x camera framing buffer)
            double approxAltitude = maxSpanDeg * 111_320.0 * 2.2;
            approxAltitude = Math.Clamp(approxAltitude, MinAltitudeMeters, MaxAltitudeMeters);

            return new CameraState(centerLat, centerLon, approxAltitude, 0.0, -90.0, 0.0);
        }

        public bool IsValid()
        {
            if (double.IsNaN(Latitude) || double.IsInfinity(Latitude)) return false;
            if (double.IsNaN(Longitude) || double.IsInfinity(Longitude)) return false;
            if (double.IsNaN(Altitude) || double.IsInfinity(Altitude)) return false;
            if (double.IsNaN(Heading) || double.IsInfinity(Heading)) return false;
            if (double.IsNaN(Pitch) || double.IsInfinity(Pitch)) return false;
            if (double.IsNaN(Roll) || double.IsInfinity(Roll)) return false;

            if (Latitude < -90.0 || Latitude > 90.0) return false;
            if (Longitude < -180.0 || Longitude > 180.0) return false;
            if (Altitude < MinAltitudeMeters || Altitude > MaxAltitudeMeters) return false;

            return true;
        }

        public void Sanitize()
        {
            if (double.IsNaN(Latitude) || double.IsInfinity(Latitude)) Latitude = DefaultLatitude;
            if (double.IsNaN(Longitude) || double.IsInfinity(Longitude)) Longitude = DefaultLongitude;
            if (double.IsNaN(Altitude) || double.IsInfinity(Altitude)) Altitude = DefaultAltitude;
            if (double.IsNaN(Heading) || double.IsInfinity(Heading)) Heading = 0.0;
            if (double.IsNaN(Pitch) || double.IsInfinity(Pitch)) Pitch = -90.0;
            if (double.IsNaN(Roll) || double.IsInfinity(Roll)) Roll = 0.0;

            Latitude = Math.Clamp(Latitude, -89.9999, 89.9999);
            while (Longitude > 180.0) Longitude -= 360.0;
            while (Longitude < -180.0) Longitude += 360.0;

            Altitude = Math.Clamp(Altitude, MinAltitudeMeters, MaxAltitudeMeters);
            Heading = ((Heading % 360.0) + 360.0) % 360.0;
            Pitch = Math.Clamp(Pitch, -90.0, -5.0);
            Roll = Math.Clamp(Roll, -45.0, 45.0);
        }

        public CameraState Clone()
        {
            return new CameraState(Latitude, Longitude, Altitude, Heading, Pitch, Roll);
        }

        public override string ToString()
        {
            return $"Lat: {Latitude:F4}°, Lon: {Longitude:F4}°, Alt: {Altitude:N0}m, Head: {Heading:F1}°, Pitch: {Pitch:F1}°";
        }
    }
}
