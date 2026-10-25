using System;
using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;
using ZEarth.Globe;

namespace ZEarth.App.Views
{
    public partial class OptionsDialog : Window
    {
        private readonly GlobeBridge? _globeBridge;

        public OptionsDialog(GlobeBridge? globeBridge = null)
        {
            InitializeComponent();
            _globeBridge = globeBridge;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingsService.Instance.Settings;

            // Distance
            SelectComboByTag(DistanceUnitCombo, settings.DistanceUnit);
            // Area
            SelectComboByTag(AreaUnitCombo, settings.AreaUnit);
            // Elevation
            SelectComboByTag(ElevationUnitCombo, settings.ElevationUnit);

            // Snapping
            SnapEnabledCheck.IsChecked = settings.SnappingEnabled;
            GroundToleranceSlider.Value = Math.Clamp(settings.GroundToleranceMeters, 1.0, 50.0);
            SnapVerticesCheck.IsChecked = settings.SnapVertices;
            SnapMidpointsCheck.IsChecked = settings.SnapMidpoints;
            SnapEdgesCheck.IsChecked = settings.SnapEdges;
            SnapIntersectionsCheck.IsChecked = settings.SnapIntersections;

            // Basemap
            SelectComboByTag(DefaultBasemapCombo, settings.DefaultBasemap);
            SelectComboByTag(TerrainProviderCombo, settings.TerrainProviderId);
            CesiumIonTokenBox.Password = settings.CesiumIonToken ?? string.Empty;
            CustomTerrainUrlBox.Text = settings.CustomTerrainUrl ?? string.Empty;

            // Theme & Language
            SelectComboByTag(ThemeCombo, settings.Theme);
            SelectComboByTag(LanguageCombo, settings.Language);
            GraticulesCheck.IsChecked = settings.ShowGraticules;
            GraticuleLabelsCheck.IsChecked = settings.ShowGraticuleLabels;
            SelectComboByTag(GridLabelFormatCombo, settings.GridLabelFormat);
            KeyParallelsCheck.IsChecked = settings.ShowKeyParallels;
            LongitudeLinesCheck.IsChecked = settings.ShowLongitudeLines;
            LatitudeLinesCheck.IsChecked = settings.ShowLatitudeLines;
            LongitudeIntervalSlider.Value = Math.Clamp(settings.LongitudeIntervalDegrees, 0.0, 60.0);
            LatitudeIntervalSlider.Value = Math.Clamp(settings.LatitudeIntervalDegrees, 0.0, 60.0);
            UtmZonesCheck.IsChecked = settings.ShowUtmZones;
            SceneMarkersCheck.IsChecked = settings.ShowSceneLocationsOnMap;
            SelectComboByTag(CreditsModeCombo, settings.CreditsDisplayMode);
            ProviderLogosCheck.IsChecked = settings.ShowOptionalProviderLogos;
            ZoomSensitivitySlider.Value = Math.Clamp(settings.ZoomSensitivity, 0.10, 0.60);
        }

        private static void SelectComboByTag(ComboBox combo, string? tagValue)
        {
            if (string.IsNullOrEmpty(tagValue)) return;
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string GetSelectedTag(ComboBox combo, string defaultValue)
        {
            if (combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string s) return s;
            return defaultValue;
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Instance.Settings;

            // Save units
            settings.DistanceUnit = GetSelectedTag(DistanceUnitCombo, "AutoMetric");
            settings.AreaUnit = GetSelectedTag(AreaUnitCombo, "AutoMetric");
            settings.ElevationUnit = GetSelectedTag(ElevationUnitCombo, "Meters");

            if (Enum.TryParse<DistanceUnit>(settings.DistanceUnit, true, out var dUnit))
                UnitService.Instance.ActiveDistanceUnit = dUnit;
            if (Enum.TryParse<AreaUnit>(settings.AreaUnit, true, out var aUnit))
                UnitService.Instance.ActiveAreaUnit = aUnit;
            if (Enum.TryParse<ElevationUnit>(settings.ElevationUnit, true, out var eUnit))
                UnitService.Instance.ActiveElevationUnit = eUnit;

            // Save snapping
            settings.SnappingEnabled = SnapEnabledCheck.IsChecked == true;
            settings.SnappingMode = "GroundDistance";
            settings.GroundToleranceMeters = GroundToleranceSlider.Value;
            settings.SnapVertices = SnapVerticesCheck.IsChecked == true;
            settings.SnapMidpoints = SnapMidpointsCheck.IsChecked == true;
            settings.SnapEdges = SnapEdgesCheck.IsChecked == true;
            settings.SnapIntersections = SnapIntersectionsCheck.IsChecked == true;

            // Save basemap, theme, language, graticules & display
            settings.DefaultBasemap = GetSelectedTag(DefaultBasemapCombo, "offline-vector");
            settings.TerrainProviderId = GetSelectedTag(TerrainProviderCombo, "demTerrain");
            settings.CesiumIonToken = string.IsNullOrWhiteSpace(CesiumIonTokenBox.Password) ? null : CesiumIonTokenBox.Password.Trim();
            settings.CustomTerrainUrl = string.IsNullOrWhiteSpace(CustomTerrainUrlBox.Text) ? null : CustomTerrainUrlBox.Text.Trim();
            settings.Theme = GetSelectedTag(ThemeCombo, "Dark");
            settings.Language = GetSelectedTag(LanguageCombo, "en");
            settings.ShowGraticules = GraticulesCheck.IsChecked == true;
            settings.ShowGraticuleLabels = GraticuleLabelsCheck.IsChecked == true;
            settings.GridLabelFormat = GetSelectedTag(GridLabelFormatCombo, "DMS");
            settings.ShowKeyParallels = KeyParallelsCheck.IsChecked == true;
            settings.ShowLongitudeLines = LongitudeLinesCheck.IsChecked == true;
            settings.ShowLatitudeLines = LatitudeLinesCheck.IsChecked == true;
            settings.LongitudeIntervalDegrees = LongitudeIntervalSlider.Value;
            settings.LatitudeIntervalDegrees = LatitudeIntervalSlider.Value;
            settings.ShowUtmZones = UtmZonesCheck.IsChecked == true;
            settings.ShowSceneLocationsOnMap = SceneMarkersCheck.IsChecked == true;
            settings.CreditsDisplayMode = GetSelectedTag(CreditsModeCombo, "Compact");
            settings.ShowOptionalProviderLogos = ProviderLogosCheck.IsChecked == true;
            settings.ZoomSensitivity = ZoomSensitivitySlider.Value;

            SettingsService.Instance.Save();

            // Apply to globe bridge
            if (_globeBridge != null)
            {
                await _globeBridge.SetUnitsConfigAsync(settings.DistanceUnit, settings.AreaUnit, settings.ElevationUnit);
                await _globeBridge.SetSnappingConfigAsync(
                    settings.SnappingEnabled,
                    settings.SnappingMode,
                    settings.SnappingTolerancePixels,
                    settings.GroundToleranceMeters,
                    settings.SnapVertices,
                    settings.SnapMidpoints,
                    settings.SnapEdges,
                    settings.SnapIntersections);
                await _globeBridge.SetGraticulesConfigAsync(
                    settings.ShowGraticules,
                    settings.ShowGraticuleLabels,
                    settings.ShowKeyParallels,
                    settings.ShowUtmZones,
                    settings.ShowLongitudeLines,
                    settings.ShowLatitudeLines,
                    settings.LongitudeIntervalDegrees,
                    settings.LatitudeIntervalDegrees,
                    settings.GridLabelFormat);
                await _globeBridge.SetCreditsConfigAsync(
                    settings.CreditsDisplayMode,
                    settings.ShowOptionalProviderLogos);
                await _globeBridge.SetZoomSensitivityAsync(settings.ZoomSensitivity);
            }

            DialogResult = true;
            Close();
        }
    }
}
