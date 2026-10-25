using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ZEarth.App.Services;
using ZEarth.Domain.Imagery;

namespace ZEarth.App.Views
{
    public class BasemapGalleryItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Attribution { get; set; } = string.Empty;
        public string LayerType { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public BasemapCategory Category { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string ThumbnailIcon { get; set; } = "🗺";
        public string ThumbnailBackground { get; set; } = "#1e293b";
    }

    public partial class BasemapGalleryDialog : Window
    {
        public string? SelectedBasemapId { get; private set; }
        public bool IsSwipeRequested { get; private set; }
        public string? SwipeLeftId { get; private set; }
        public string? SwipeRightId { get; private set; }

        private readonly List<BasemapGalleryItem> _allItems = new();
        private string _activeSource = "All";
        private string _searchFilter = string.Empty;

        public BasemapGalleryDialog()
        {
            InitializeComponent();
            ThemeService.Instance.ApplyWindowTheme(this, ThemeService.Instance.CurrentTheme);
            LoadBasemaps();
        }

        private void LoadBasemaps()
        {
            _allItems.Clear();
            foreach (var p in ImageryRegistry.Providers)
            {
                string source = string.IsNullOrWhiteSpace(p.ProviderGroup) ? "Custom" : p.ProviderGroup;

                string icon = p.Id switch
                {
                    "esri-hybrid" or "esri-imagery" => "🛰",
                    "esri-topo" or "opentopo" => "⛰",
                    "carto-dark" => "🌙",
                    "carto-light" => "☀️",
                    "esri-streets" or "osm-standard" => "🛣",
                    "offline-vector" => "🌐",
                    _ => "🗺"
                };

                string bg = p.Id switch
                {
                    "esri-hybrid" => "#0f2b38",
                    "esri-imagery" => "#0f172a",
                    "esri-topo" or "opentopo" => "#451a03",
                    "esri-streets" => "#1e3a8a",
                    "osm-standard" => "#1e40af",
                    "carto-dark" => "#020617",
                    "carto-light" => "#334155",
                    "offline-vector" => "#164e63",
                    _ => "#1e293b"
                };

                _allItems.Add(new BasemapGalleryItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Attribution = p.Attribution,
                    LayerType = p.ProviderType.ToString(),
                    Category = p.Category,
                    CategoryName = p.CategoryName,
                    SourceName = source,
                    ThumbnailIcon = icon,
                    ThumbnailBackground = bg,
                    StatusText = string.Equals(p.Id, SettingsService.Instance.Settings.DefaultBasemap, StringComparison.OrdinalIgnoreCase)
                        ? "✓ DEFAULT"
                        : p.IsOfflineAvailable ? "Offline Ready" : p.AuthRequired ? "Requires Key" : "Online"
                });
            }

            // Populate Swipe ComboBoxes
            CmbSwipeLeft.ItemsSource = _allItems.ToList();
            CmbSwipeLeft.DisplayMemberPath = "Name";
            CmbSwipeLeft.SelectedValuePath = "Id";

            CmbSwipeRight.ItemsSource = _allItems.ToList();
            CmbSwipeRight.DisplayMemberPath = "Name";
            CmbSwipeRight.SelectedValuePath = "Id";

            // Default Swipe presets: Esri Hybrid on Left, OpenTopo on Right
            CmbSwipeLeft.SelectedValue = ImageryRegistry.ProviderEsriHybrid;
            CmbSwipeRight.SelectedValue = ImageryRegistry.ProviderOpenTopoMap;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = _allItems.AsEnumerable();

            if (_activeSource != "All")
            {
                if (_activeSource == "Esri") filtered = filtered.Where(i => i.SourceName == "Esri");
                else if (_activeSource == "OSM") filtered = filtered.Where(i => i.SourceName == "OpenStreetMap");
                else if (_activeSource == "Carto") filtered = filtered.Where(i => i.SourceName == "CARTO");
                else if (_activeSource == "Offline") filtered = filtered.Where(i => i.SourceName == "Natural Earth");
            }

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                var query = _searchFilter.Trim().ToLowerInvariant();
                filtered = filtered.Where(i => i.Name.ToLowerInvariant().Contains(query) ||
                                               i.Attribution.ToLowerInvariant().Contains(query) ||
                                               i.CategoryName.ToLowerInvariant().Contains(query) ||
                                               i.SourceName.ToLowerInvariant().Contains(query));
            }

            var ordered = filtered.OrderBy(i => i.SourceName).ThenBy(i => i.CategoryName).ThenBy(i => i.Name).ToList();
            var view = new ListCollectionView(ordered);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BasemapGalleryItem.SourceName)));
            BasemapsItemsControl.ItemsSource = view;
        }

        private void OnSourceFilterChecked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string src)
            {
                _activeSource = src;
                ApplyFilter();
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = TxtSearch.Text ?? string.Empty;
            ApplyFilter();
        }

        private void OnSelectBasemapClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                SelectedBasemapId = id;
                IsSwipeRequested = false;
                DialogResult = true;
                Close();
            }
        }

        private void OnSwapSwipeClick(object sender, RoutedEventArgs e)
        {
            var left = CmbSwipeLeft.SelectedValue;
            var right = CmbSwipeRight.SelectedValue;
            CmbSwipeLeft.SelectedValue = right;
            CmbSwipeRight.SelectedValue = left;
        }

        private void OnStartSwipeClick(object sender, RoutedEventArgs e)
        {
            var leftId = CmbSwipeLeft.SelectedValue as string;
            var rightId = CmbSwipeRight.SelectedValue as string;

            if (string.IsNullOrEmpty(leftId) || string.IsNullOrEmpty(rightId))
            {
                MessageBox.Show(this, "Please select both Left and Right basemaps for comparison.", "Swipe Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (leftId == rightId)
            {
                MessageBox.Show(this, "Please select two distinct basemaps to compare with Swipe.", "Distinct Basemaps Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsSwipeRequested = true;
            SwipeLeftId = leftId;
            SwipeRightId = rightId;
            DialogResult = true;
            Close();
        }
    }
}
