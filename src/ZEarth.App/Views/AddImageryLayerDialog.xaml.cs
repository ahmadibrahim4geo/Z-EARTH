using System;
using System.Windows;
using System.Windows.Controls;
using ZEarth.Domain.Imagery;

namespace ZEarth.App.Views
{
    public partial class AddImageryLayerDialog : Window
    {
        public ImageryLayer? CreatedLayer { get; private set; }

        public AddImageryLayerDialog()
        {
            InitializeComponent();
            Services.ThemeService.Instance.ApplyWindowTheme(this, Services.ThemeService.Instance.CurrentTheme);
        }

        private void PresetCartoVoyager_Click(object sender, RoutedEventArgs e)
        {
            TxtLayerName.Text = "CartoDB Voyager";
            CmbProviderType.SelectedIndex = 0; // XYZ
            TxtUrlTemplate.Text = "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png";
            TxtAttribution.Text = "© OpenStreetMap contributors, © CARTO";
        }

        private void PresetStamenToner_Click(object sender, RoutedEventArgs e)
        {
            TxtLayerName.Text = "Stamen Toner Lite";
            CmbProviderType.SelectedIndex = 0; // XYZ
            TxtUrlTemplate.Text = "https://tiles.stadiamaps.com/tiles/stamen_toner_lite/{z}/{x}/{y}.png";
            TxtAttribution.Text = "© Stadia Maps, © Stamen Design, © OpenStreetMap";
        }

        private void PresetUsgs_Click(object sender, RoutedEventArgs e)
        {
            TxtLayerName.Text = "USGS National Map Imagery";
            CmbProviderType.SelectedIndex = 0; // XYZ
            TxtUrlTemplate.Text = "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/tile/{z}/{y}/{x}";
            TxtAttribution.Text = "USGS - The National Map: National Imagery";
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtLayerName.Text?.Trim();
            var url = TxtUrlTemplate.Text?.Trim();
            var attribution = TxtAttribution.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Please enter a display name for this layer.", "Layer Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtLayerName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, "Please enter a valid URL template for the imagery service.", "URL Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUrlTemplate.Focus();
                return;
            }

            var selectedItem = CmbProviderType.SelectedItem as ComboBoxItem;
            var tag = selectedItem?.Tag?.ToString() ?? "XyzTileServer";
            var providerType = tag switch
            {
                "WmsServer" => ImageryProviderType.WmsServer,
                "WmtsServer" => ImageryProviderType.WmtsServer,
                _ => ImageryProviderType.XyzTileServer
            };

            if (!ImageryRegistry.ValidateUrlTemplate(url, providerType))
            {
                var result = MessageBox.Show(this,
                    "The URL format might not match standard XYZ tile patterns (missing {z}, {x}, {y}). Do you want to proceed anyway?",
                    "URL Validation Warning", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    TxtUrlTemplate.Focus();
                    return;
                }
            }

            CreatedLayer = new ImageryLayer(
                id: Guid.NewGuid().ToString("N"),
                name: name,
                providerType: providerType,
                attribution: attribution,
                isBaseLayer: false,
                isOffline: false
            )
            {
                UrlTemplate = url,
                Opacity = 1.0,
                IsVisible = true
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
