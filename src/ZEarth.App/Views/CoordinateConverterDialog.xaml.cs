using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZEarth.Domain.Crs;

namespace ZEarth.App.Views
{
    public partial class CoordinateConverterDialog : Window
    {
        private bool _isUpdating;
        public double ConvertedLat { get; private set; }
        public double ConvertedLon { get; private set; }
        public bool RequestedFlyTo { get; private set; }

        public CoordinateConverterDialog(double initialLat = 30.0444, double initialLon = 31.2357)
        {
            InitializeComponent();
            ConvertedLat = initialLat;
            ConvertedLon = initialLon;

            _isUpdating = true;
            DdLatText.Text = initialLat.ToString("F6", CultureInfo.InvariantCulture);
            DdLonText.Text = initialLon.ToString("F6", CultureInfo.InvariantCulture);
            _isUpdating = false;

            UpdateAllFormats(initialLat, initialLon);
        }

        private void OnDdChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (double.TryParse(DdLatText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(DdLonText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                if (lat >= -90.0 && lat <= 90.0 && lon >= -180.0 && lon <= 180.0)
                {
                    ConvertedLat = lat;
                    ConvertedLon = lon;
                    UpdateAllFormats(lat, lon);
                }
            }
        }

        private void UpdateAllFormats(double lat, double lon)
        {
            try
            {
                // DMS
                string dmsFull = CoordinateFormatter.ToDms(lon, lat);
                var parts = dmsFull.Split('|');
                if (parts.Length == 2)
                {
                    DmsLatText.Text = parts[0].Trim();
                    DmsLonText.Text = parts[1].Trim();
                }

                // UTM
                var utm = CoordinateTransformService.Wgs84ToUtm(lon, lat);
                UtmZoneText.Text = $"{utm.Zone}{(utm.IsNorthernHemisphere ? "N" : "S")}";
                UtmEastingText.Text = utm.Easting.ToString("F1", CultureInfo.InvariantCulture);
                UtmNorthingText.Text = utm.Northing.ToString("F1", CultureInfo.InvariantCulture);

                // MGRS
                MgrsText.Text = MgrsConverter.FromWgs84(lon, lat, 5);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoordConverter] Error: {ex.Message}");
            }
        }

        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Decimal Degrees: {DdLatText.Text}, {DdLonText.Text}");
            sb.AppendLine($"DMS: {DmsLatText.Text} | {DmsLonText.Text}");
            sb.AppendLine($"UTM Zone: {UtmZoneText.Text}, Easting: {UtmEastingText.Text}m, Northing: {UtmNorthingText.Text}m");
            sb.AppendLine($"MGRS: {MgrsText.Text}");

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("All coordinate representations copied to clipboard.", "Coordinates Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnFlyToClick(object sender, RoutedEventArgs e)
        {
            RequestedFlyTo = true;
            DialogResult = true;
            Close();
        }
    }
}
