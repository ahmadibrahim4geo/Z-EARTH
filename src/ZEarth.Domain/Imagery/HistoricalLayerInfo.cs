using System;

namespace ZEarth.Domain.Imagery
{
    public class HistoricalLayerInfo
    {
        public string LayerId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime AcquisitionDate { get; set; } = DateTime.UtcNow;
        public DateTime PublicationDate { get; set; } = DateTime.UtcNow;
        public string Sensor { get; set; } = "Sentinel-2 MSI";
        public double CloudCoverPercentage { get; set; } = 0.0;
        public double ResolutionMeters { get; set; } = 10.0;
        public string Provider { get; set; } = "Copernicus Open Access Hub";

        public HistoricalLayerInfo()
        {
        }

        public HistoricalLayerInfo(string layerId, string title, DateTime acquisitionDate, string sensor, double resolutionMeters = 10.0, double cloudCover = 0.0)
        {
            LayerId = layerId;
            Title = title;
            AcquisitionDate = acquisitionDate;
            PublicationDate = acquisitionDate.AddDays(2);
            Sensor = sensor;
            ResolutionMeters = resolutionMeters;
            CloudCoverPercentage = cloudCover;
        }

        public string GetFormattedBadge()
        {
            return $"Captured: {AcquisitionDate:yyyy-MM-dd} • Sensor: {Sensor} • Res: {ResolutionMeters:0.0}m • Clouds: {CloudCoverPercentage:0.0}%";
        }
    }
}
