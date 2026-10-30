using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using ZEarth.App.Services;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;
using ZEarth.Globe;

namespace ZEarth.App.Views
{
    public enum OptionsDialogTab
    {
        MeasurementUnits = 0,
        SpatialSnapping = 1,
        BasemapDisplay = 2,
        TerrainModels = 3,
        MapGrid = 4
    }

    public partial class OptionsDialog : Window
    {
        private readonly GlobeBridge? _globeBridge;
        private readonly string _originalTerrainProvider;
        private readonly string? _originalTerrainUrl;
        private readonly string? _originalTerrainToken;
        private readonly string? _originalLocalDemPath;
        private readonly string _originalLocalDemUnit;
        private string? _selectedLocalDemPath;
        private bool _terrainPreviewChanged;
        private bool _committed;
        private bool _closingRequested;
        private bool _updatingTolerance;
        private bool _updatingTokenVisibility;

        private static readonly string[] GridLabelSizes = GridSettingsDefaults.LabelSizes;

        public OptionsDialog(GlobeBridge? globeBridge = null, bool openGridTab = false)
            : this(globeBridge, openGridTab ? OptionsDialogTab.MapGrid : OptionsDialogTab.MeasurementUnits)
        {
        }

        public OptionsDialog(GlobeBridge? globeBridge, OptionsDialogTab initialTab)
        {
            InitializeComponent();
            ConstrainToWorkingArea();
            _globeBridge = globeBridge;
            var originalSettings = SettingsService.Instance.Settings;
            _originalTerrainProvider = originalSettings.TerrainProviderId;
            _originalTerrainUrl = originalSettings.CustomTerrainUrl;
            _originalTerrainToken = string.Equals(_originalTerrainProvider, "copernicusGlo30", StringComparison.OrdinalIgnoreCase)
                ? originalSettings.CopernicusAccessToken
                : originalSettings.CesiumIonToken;
            _selectedLocalDemPath = originalSettings.LocalTerrainRasterPath;
            _originalLocalDemPath = originalSettings.LocalTerrainRasterPath;
            _originalLocalDemUnit = originalSettings.LocalTerrainVerticalUnit;
            LoadSettings();
            UpdateUnitPreviews();
            SyncGridFormatCards();
            UpdateGridStatePill();
            UpdateGridSizeLabels();
            SettingsTabs.SelectedIndex = (int)initialTab;
            LocalizationService.Instance.ApplyWindow(this);
        }

        /// <summary>
        /// Start as a compact resizable dialog and keep its chrome reachable on
        /// small screens. Individual tabs scroll vertically when space is limited.
        /// </summary>
        private void ConstrainToWorkingArea()
        {
            const double workingAreaMargin = 24;
            var workingArea = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, Math.Max(320, workingArea.Width - workingAreaMargin));
            MinHeight = Math.Min(MinHeight, Math.Max(240, workingArea.Height - workingAreaMargin));
            MaxWidth = Math.Max(MinWidth, workingArea.Width - workingAreaMargin);
            MaxHeight = Math.Max(MinHeight, workingArea.Height - workingAreaMargin);
            Width = Math.Min(Width, Math.Max(MinWidth, workingArea.Width * 0.86));
            Height = Math.Min(Height, Math.Max(MinHeight, workingArea.Height * 0.86));
        }

        private void OnOptionsDialogLoaded(object sender, RoutedEventArgs e)
        {
            if (Owner is null || Owner.ActualWidth <= 0 || Owner.ActualHeight <= 0) return;

            const double ownerMargin = 24;
            MinWidth = Math.Min(MinWidth, Math.Max(320, Owner.ActualWidth - ownerMargin));
            MinHeight = Math.Min(MinHeight, Math.Max(240, Owner.ActualHeight - ownerMargin));
            MaxWidth = Math.Min(MaxWidth, Math.Max(MinWidth, Owner.ActualWidth - ownerMargin));
            MaxHeight = Math.Min(MaxHeight, Math.Max(MinHeight, Owner.ActualHeight - ownerMargin));
            Width = Math.Min(Width, Math.Max(MinWidth, Owner.ActualWidth * 0.86));
            Height = Math.Min(Height, Math.Max(MinHeight, Owner.ActualHeight * 0.86));
            Left = Owner.Left + Math.Max(0, (Owner.ActualWidth - Width) / 2);
            Top = Owner.Top + Math.Max(0, (Owner.ActualHeight - Height) / 2);
        }

        /// <summary>
        /// The dialog never owns grid visibility: View → Show Grid is the single
        /// authoritative state. The pill reflects the persisted value so the user
        /// cannot be misled by a stale dialog.
        /// </summary>
        private void UpdateGridStatePill()
        {
            var visible = SettingsService.Instance.Settings.ShowGraticules;
            GridStateText.Text = visible ? "Grid ON" : "Grid OFF";
            GridStateDot.Fill = visible
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
            GridStatePill.Background = visible
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 74, 222, 128))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 34, 48, 58));
        }

        private void UpdateGridSizeLabels()
        {
            var index = (int)Math.Round(GridLabelSizeSlider.Value);
            var labels = new[] { SizeSmallText, SizeMediumText, SizeLargeText, SizeExtraLargeText };
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
                labels[i].Foreground = i == index
                    ? (System.Windows.Media.Brush)FindResource("Brush.Accent")
                    : (System.Windows.Media.Brush)FindResource("Brush.Text.Secondary");
            }
        }

        private void OnGridLabelSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SizeSmallText != null) UpdateGridSizeLabels();
        }

        private void OnGridFormatChecked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string format } || GridLabelFormatCombo == null) return;
            SelectComboByTag(GridLabelFormatCombo, format);
        }

        private void SyncGridFormatCards()
        {
            if (GridLabelFormatCombo == null) return;
            var format = GetSelectedTag(GridLabelFormatCombo, GridSettingsDefaults.GridLabelFormat);
            GridFormatDdRadio.IsChecked = format == "DD";
            GridFormatDmRadio.IsChecked = format == "DM";
            GridFormatDmsRadio.IsChecked = format == "DMS";
            GridFormatDegreesRadio.IsChecked = format == "Degrees";
        }

        private void OnUnitSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DistancePreviewText != null) UpdateUnitPreviews();
        }

        private void UpdateUnitPreviews()
        {
            if (Enum.TryParse<DistanceUnit>(GetSelectedTag(DistanceUnitCombo, "AutoMetric"), true, out var distance))
                DistancePreviewText.Text = UnitService.Instance.FormatDistance(1234.56, distance, 2);
            if (Enum.TryParse<AreaUnit>(GetSelectedTag(AreaUnitCombo, "AutoMetric"), true, out var area))
                AreaPreviewText.Text = UnitService.Instance.FormatArea(1_250_000, area, 2);
            if (Enum.TryParse<ElevationUnit>(GetSelectedTag(ElevationUnitCombo, "Meters"), true, out var elevation))
                ElevationPreviewText.Text = UnitService.Instance.FormatElevation(987.65, elevation, 2);
        }

        private void OnGroundToleranceSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingTolerance || GroundToleranceInput == null) return;
            _updatingTolerance = true;
            GroundToleranceInput.Text = e.NewValue.ToString("0.0", CultureInfo.InvariantCulture);
            _updatingTolerance = false;
        }

        private void OnGroundToleranceInputLostFocus(object sender, RoutedEventArgs e)
        {
            if (_updatingTolerance) return;
            if (!double.TryParse(GroundToleranceInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
                !double.TryParse(GroundToleranceInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                value = GroundToleranceSlider.Value;
            value = Math.Clamp(value, 0.1, 100.0);
            _updatingTolerance = true;
            GroundToleranceSlider.Value = value;
            GroundToleranceInput.Text = value.ToString("0.0", CultureInfo.InvariantCulture);
            _updatingTolerance = false;
        }

        /// <summary>
        /// Restores the documented defaults in the dialog only. Nothing is
        /// persisted or rendered until the user presses Save Settings.
        /// </summary>
        private void OnResetGridDefaultsClick(object sender, RoutedEventArgs e)
        {
            KeyParallelsCheck.IsChecked = GridSettingsDefaults.ShowKeyParallels;
            LongitudeLinesCheck.IsChecked = GridSettingsDefaults.ShowLongitudeLines;
            LatitudeLinesCheck.IsChecked = GridSettingsDefaults.ShowLatitudeLines;
            UtmZonesCheck.IsChecked = GridSettingsDefaults.ShowUtmZones;
            SelectComboByTag(GridLabelFormatCombo, GridSettingsDefaults.GridLabelFormat);
            SyncGridFormatCards();
            LongitudeIntervalInput.Text = GridSettingsDefaults.LongitudeIntervalDegrees.ToString(CultureInfo.InvariantCulture);
            LatitudeIntervalInput.Text = GridSettingsDefaults.LatitudeIntervalDegrees.ToString(CultureInfo.InvariantCulture);
            SelectComboByTag(UtmLabelModeCombo, GridSettingsDefaults.UtmLabelMode);
            SelectComboByTag(UtmLabelPlacementCombo, GridSettingsDefaults.UtmLabelPlacement);
            MajorColorPicker.ColorHex = GridSettingsDefaults.GridMajorColor;
            LongitudeColorPicker.ColorHex = GridSettingsDefaults.GridLongitudeColor;
            LatitudeColorPicker.ColorHex = GridSettingsDefaults.GridLatitudeColor;
            LabelColorPicker.ColorHex = GridSettingsDefaults.GridLabelColor;
            UtmColorPicker.ColorHex = GridSettingsDefaults.GridUtmColor;
            UtmBoundaryColorPicker.ColorHex = GridSettingsDefaults.GridUtmColor;
            GridLabelSizeSlider.Value = Array.IndexOf(GridLabelSizes, GridSettingsDefaults.GridLabelSize);
            UpdateGridSizeLabels();
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
            GroundToleranceSlider.Value = Math.Clamp(settings.GroundToleranceMeters, 0.1, 100.0);
            GroundToleranceInput.Text = GroundToleranceSlider.Value.ToString("0.0", CultureInfo.InvariantCulture);
            SnapVerticesCheck.IsChecked = settings.SnapVertices;
            SnapMidpointsCheck.IsChecked = settings.SnapMidpoints;
            SnapEdgesCheck.IsChecked = settings.SnapEdges;
            SnapIntersectionsCheck.IsChecked = settings.SnapIntersections;

            // Basemap
            SelectComboByTag(DefaultBasemapCombo, settings.DefaultBasemap);
            SelectComboByTag(TerrainProviderCombo, settings.TerrainProviderId);
            ScreenshotDirectoryInput.Text = settings.ScreenshotDirectory;
            CesiumIonTokenBox.Password = settings.CesiumIonToken ?? string.Empty;
            CopernicusAccessTokenBox.Password = settings.CopernicusAccessToken ?? string.Empty;
            CustomTerrainUrlBox.Text = settings.CustomTerrainUrl ?? string.Empty;
            SelectComboByTag(LocalTerrainVerticalUnitCombo, settings.LocalTerrainVerticalUnit);
            if (!string.IsNullOrWhiteSpace(settings.LocalTerrainRasterPath))
                LocalDemMetadataText.Text = $"Configured file: {Path.GetFileName(settings.LocalTerrainRasterPath)}. Use Test Connection to validate its numeric grid.";

            // Theme & Language
            SelectComboByTag(ThemeCombo, settings.Theme);
            SelectComboByTag(LanguageCombo, settings.Language);
            // Map Grid: visibility components, colours, label appearance and UTM.
            KeyParallelsCheck.IsChecked = settings.ShowKeyParallels;
            LongitudeLinesCheck.IsChecked = settings.ShowLongitudeLines;
            LatitudeLinesCheck.IsChecked = settings.ShowLatitudeLines;
            UtmZonesCheck.IsChecked = settings.ShowUtmZones;
            SelectComboByTag(GridLabelFormatCombo, settings.GridLabelFormat);
            SyncGridFormatCards();
            LongitudeIntervalInput.Text = settings.LongitudeIntervalDegrees.ToString(CultureInfo.InvariantCulture);
            LatitudeIntervalInput.Text = settings.LatitudeIntervalDegrees.ToString(CultureInfo.InvariantCulture);
            SelectComboByTag(UtmLabelModeCombo, settings.UtmLabelMode);
            SelectComboByTag(UtmLabelPlacementCombo, settings.UtmLabelPlacement);
            MajorColorPicker.ColorHex = settings.GridMajorColor;
            LongitudeColorPicker.ColorHex = settings.GridLongitudeColor;
            LatitudeColorPicker.ColorHex = settings.GridLatitudeColor;
            LabelColorPicker.ColorHex = settings.GridLabelColor;
            UtmColorPicker.ColorHex = settings.GridUtmColor;
            UtmBoundaryColorPicker.ColorHex = settings.GridUtmColor;
            var sizeIndex = Array.IndexOf(GridLabelSizes, settings.GridLabelSize);
            GridLabelSizeSlider.Value = sizeIndex >= 0 ? sizeIndex : 2;
            // The two UTM colour controls are one value, never two properties.
            UtmColorPicker.ColorChanged += (_, _) => UtmBoundaryColorPicker.ColorHex = UtmColorPicker.ColorHex;
            UtmBoundaryColorPicker.ColorChanged += (_, _) => UtmColorPicker.ColorHex = UtmBoundaryColorPicker.ColorHex;
            SceneMarkersCheck.IsChecked = settings.ShowSceneLocationsOnMap;
            SelectComboByTag(CreditsModeCombo, settings.CreditsDisplayMode);
            ProviderLogosCheck.IsChecked = settings.ShowOptionalProviderLogos;
            ZoomSensitivitySlider.Value = Math.Clamp(settings.ZoomSensitivity, 0.10, 0.60);
            UpdateTerrainSourceUi();
        }

        private void OnTerrainProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TerrainSourceTitle != null) UpdateTerrainSourceUi();
        }

        private void UpdateTerrainSourceUi()
        {
            var provider = GetSelectedTag(TerrainProviderCombo, "demTerrain");
            var needsIon = provider is "worldTerrain" or "worldBathymetry";
            var isCustom = provider == "custom";
            var isLocalWorkflow = provider == "localDem";
            IonConfigurationPanel.Visibility = needsIon ? Visibility.Visible : Visibility.Collapsed;
            CopernicusApiPanel.Visibility = provider == "copernicusGlo30" ? Visibility.Visible : Visibility.Collapsed;
            CustomTerrainPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            LocalTerrainPanel.Visibility = isLocalWorkflow ? Visibility.Visible : Visibility.Collapsed;
            TerrainAssetIdPanel.Visibility = provider == "worldBathymetry" ? Visibility.Visible : Visibility.Collapsed;
            GebcoGuideButton.Visibility = Visibility.Collapsed;
            GetCesiumIonTokenButton.Visibility = needsIon ? Visibility.Visible : Visibility.Collapsed;

            (TerrainSourceTitle.Text, TerrainSourceSummary.Text, TerrainRequirementsText.Text,
                TerrainHelpTitle.Text, TerrainHelpText.Text) = provider switch
            {
                "worldTerrain" => ("Cesium World Terrain", "High-resolution global land terrain.",
                    "Requires a valid Cesium ion token with access to asset 1.",
                    "Using Cesium World Terrain",
                    "This is Cesium ion asset 1. It is separate from the direct Copernicus Data Space provider listed in this gallery."),
                "copernicusGlo30" => ("Copernicus DEM GLO-30", "Official 30 m global DSM through the Copernicus Data Space Process API.",
                    "Free access requires a CDSE account, CCM registration, and a session OAuth access token; no Cesium ion token is used.",
                    "Using Copernicus GLO-30",
                    "Z EARTH requests numeric FLOAT32 height tiles from the official CDSE Process API. GLO-30 is a surface model and can include buildings and vegetation."),
                "srtm30" => ("SRTM-backed Terrain", "Online numeric height tiles hosted by AWS Open Data.",
                    "No API key is required. The global Mapzen/Tilezen mosaic uses SRTM in covered source regions and other public DEMs elsewhere.",
                    "Using SRTM-backed Terrain",
                    "This is not relabelled as pure SRTM worldwide. Z EARTH preserves the provider's blended lineage and required attribution."),
                "aw3d30" => ("JAXA AW3D30", "Official online ~30 m DSM through JAXA Earth API.",
                    "No registration or API key is required by JAXA Earth API.",
                    "Using JAXA AW3D30",
                    "Z EARTH retrieves numeric AW3D30 COG/STAC tiles through the official JAXA JavaScript API and reports missing sea/no-data honestly."),
                "worldBathymetry" => ("Cesium World Bathymetry", "Combined land and seabed relief using Cesium ion asset 2426648.",
                    "Requires a valid Cesium ion token and access to the bathymetry asset.",
                    "Using World Bathymetry",
                    "This is numeric 3D terrain that includes ocean bathymetry. It is distinct from an ocean shaded-relief imagery layer and requires authorized Cesium ion access."),
                "ellipsoid" => ("WGS84 Ellipsoid", "Smooth offline globe with no elevation or bathymetry.",
                    "No network or credentials are required.",
                    "Using the Ellipsoid",
                    "Use this offline fallback when relief is not needed. It must never be described as a DEM or as containing terrain elevations."),
                "gebco2026" => ("GEBCO 2026 Bathymetry", "Official global numeric land and seafloor grid at 15 arc-seconds.",
                    "No API key is required. Z EARTH reads the GEBCO 2026 numeric grid from the Australian Antarctic Division mirror on Source Cooperative; the GEBCO WMS remains imagery only.",
                    "Using GEBCO 2026",
                    "GEBCO values are metres with heterogeneous mean-sea-level assumptions. They are not for navigation, and service/network failure falls back visibly to the ellipsoid."),
                "localDem" => ("Local DEM / Bathymetry", "Render an explicitly selected georeferenced numeric raster.",
                    "GDAL validates CRS, extent, numeric band, NoData and the selected vertical unit before activation.",
                    "Using a Local DEM",
                    "Select a georeferenced GeoTIFF, IMG, or DEM. Outside its coverage or in NoData gaps, the globe displays the ellipsoid and elevation readings are unavailable."),
                "custom" => ("Custom Quantized-Mesh Service", "Connect to an HTTPS Cesium terrain service you control or are authorized to use.",
                    "Enter an HTTPS service root that exposes layer.json and quantized-mesh tiles.",
                    "Using a Custom Terrain Service",
                    "A compatible service must publish Cesium quantized-mesh metadata and tiles. A WMS, XYZ image layer, local GeoTIFF path, or arbitrary URL is not a terrain service."),
                _ => ("AWS Terrarium DEM", "Global numeric land elevation from AWS Open Data; no key required.",
                    "No credentials are required for AWS Terrarium DEM.",
                    "Using AWS Terrarium DEM",
                    "A public heightmap terrain source that supplies numeric land elevation to Cesium. Coverage and source quality vary by region; it is not guaranteed bare-earth terrain everywhere.")
            };

            if (LocalizationService.Instance.IsArabic)
            {
                TerrainSourceTitle.Text = LocalizationService.Instance.TranslateLiteral(TerrainSourceTitle.Text);
                TerrainSourceSummary.Text = LocalizationService.Instance.TranslateLiteral(TerrainSourceSummary.Text);
                TerrainRequirementsText.Text = LocalizationService.Instance.TranslateLiteral(TerrainRequirementsText.Text);
                TerrainHelpTitle.Text = LocalizationService.Instance.TranslateLiteral(TerrainHelpTitle.Text);
                TerrainHelpText.Text = LocalizationService.Instance.TranslateLiteral(TerrainHelpText.Text);
            }

            DefaultTerrainSummary.Text = TerrainSourceTitle.Text;
            SetTerrainConnectionState(null, "Not tested");
        }

        private void OnCesiumTokenPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_updatingTokenVisibility || CesiumIonTokenVisibleBox == null) return;
            _updatingTokenVisibility = true;
            CesiumIonTokenVisibleBox.Text = CesiumIonTokenBox.Password;
            _updatingTokenVisibility = false;
        }

        private void OnCesiumTokenVisibleChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingTokenVisibility || CesiumIonTokenBox == null) return;
            _updatingTokenVisibility = true;
            CesiumIonTokenBox.Password = CesiumIonTokenVisibleBox.Text;
            _updatingTokenVisibility = false;
        }

        private void OnToggleTokenVisibilityClick(object sender, RoutedEventArgs e)
        {
            var show = CesiumIonTokenVisibleBox.Visibility != Visibility.Visible;
            _updatingTokenVisibility = true;
            CesiumIonTokenVisibleBox.Text = CesiumIonTokenBox.Password;
            CesiumIonTokenVisibleBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CesiumIonTokenBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            _updatingTokenVisibility = false;
            (show ? (Control)CesiumIonTokenVisibleBox : CesiumIonTokenBox).Focus();
        }

        private void SetTerrainConnectionState(bool? success, string message)
        {
            TerrainConnectionText.Text = LocalizationService.Instance.TranslateLiteral(message);
            var color = success switch
            {
                true => Color.FromRgb(74, 222, 128),
                false => Color.FromRgb(239, 83, 80),
                null => Color.FromRgb(156, 163, 175)
            };
            TerrainConnectionDot.Fill = new SolidColorBrush(color);
            TerrainConnectionPill.Background = new SolidColorBrush(Color.FromArgb(42, color.R, color.G, color.B));
        }

        private static bool TryValidateCustomTerrainUrl(string? raw, out Uri? uri, out string error)
        {
            uri = null;
            error = string.Empty;
            if (!Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
            {
                error = "Enter an absolute HTTPS terrain service URL.";
                return false;
            }

            if (string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parsed.Host, "localhost.localdomain", StringComparison.OrdinalIgnoreCase))
            {
                error = "Localhost terrain services are not accepted in this configuration field.";
                return false;
            }

            if (IPAddress.TryParse(parsed.Host, out var address) && IsPrivateOrLocalAddress(address))
            {
                error = "Private, loopback, and link-local terrain service addresses are not accepted.";
                return false;
            }

            uri = parsed;
            return true;
        }

        private static bool IsPrivateOrLocalAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
            if (address.AddressFamily != AddressFamily.InterNetwork) return false;
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 127 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        private async void OnTestTerrainConnectionClick(object sender, RoutedEventArgs e)
        {
            if (_globeBridge == null)
            {
                SetTerrainConnectionState(false, "Globe unavailable");
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral("The embedded globe is not available. Open Options from the running main window and try again.");
                return;
            }

            var provider = GetSelectedTag(TerrainProviderCombo, "demTerrain");
            var token = provider == "copernicusGlo30"
                ? CopernicusAccessTokenBox.Password.Trim()
                : CesiumIonTokenBox.Password.Trim();
            string? url = null;
            if (provider is "worldTerrain" or "worldBathymetry" && string.IsNullOrWhiteSpace(token))
            {
                SetTerrainConnectionState(false, "Token required");
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral("Paste a valid Cesium ion token before testing this source.");
                return;
            }
            if (provider == "copernicusGlo30" && string.IsNullOrWhiteSpace(token))
            {
                SetTerrainConnectionState(false, "Token required");
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral("Paste a Copernicus Data Space OAuth access token. This provider does not use a Cesium ion token.");
                return;
            }
            if (provider == "custom")
            {
                if (!TryValidateCustomTerrainUrl(CustomTerrainUrlBox.Text, out var parsed, out var error))
                {
                    SetTerrainConnectionState(false, "Invalid URL");
                    TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral(error);
                    return;
                }
                url = parsed!.AbsoluteUri;
            }
            if (provider == "localDem")
            {
                if (sender is Button localTestButton) localTestButton.IsEnabled = false;
                _terrainPreviewChanged = true;
                try
                {
                    var path = _selectedLocalDemPath ?? string.Empty;
                    var unit = GetSelectedTag(LocalTerrainVerticalUnitCombo, "Meters");
                    SetTerrainConnectionState(null, "Inspecting raster…");
                    var metadata = await Task.Run(() => LocalTerrainRasterService.Instance.Configure(path, unit));
                    if (_closingRequested)
                    {
                        await RestoreOriginalTerrainAsync();
                        return;
                    }
                    LocalDemMetadataText.Text = metadata.ToDisplayText();
                }
                catch (Exception ex)
                {
                    SetTerrainConnectionState(false, "Invalid raster");
                    TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral(ex.Message);
                    return;
                }
                finally
                {
                    if (sender is Button completedLocalTestButton) completedLocalTestButton.IsEnabled = true;
                }
            }

            if (sender is Button button) button.IsEnabled = false;
            SetTerrainConnectionState(null, "Testing…");
            TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral("Waiting for the Cesium engine to load and validate the selected source…");
            try
            {
                _terrainPreviewChanged = true;
                var success = await _globeBridge.SetTerrainProviderAsync(provider, url, token);
                SetTerrainConnectionState(success, success ? "Connected" : "Failed");
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral(success
                    ? "The embedded Cesium engine acknowledged this terrain source. Visible relief still requires native GPU and tile acceptance."
                    : "The terrain source failed. The globe falls back to the WGS84 ellipsoid; check credentials, URL, network, and provider access.");
            }
            catch (Exception)
            {
                SetTerrainConnectionState(false, "Failed");
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral("Connection failed. See the application log for the provider error.");
            }
            finally
            {
                if (sender is Button testButton) testButton.IsEnabled = true;
                if (_closingRequested && !_committed) await RestoreOriginalTerrainAsync();
            }
        }

        private void OnBrowseScreenshotFolderClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Choose screenshot save folder",
                Multiselect = false
            };
            if (Directory.Exists(ScreenshotDirectoryInput.Text)) dialog.InitialDirectory = ScreenshotDirectoryInput.Text;
            if (dialog.ShowDialog(this) == true) ScreenshotDirectoryInput.Text = dialog.FolderName;
        }

        private async void OnBrowseLocalDemClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Inspect local elevation or bathymetry raster",
                Filter = "Elevation rasters (*.tif;*.tiff;*.img;*.dem)|*.tif;*.tiff;*.img;*.dem|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            _selectedLocalDemPath = dialog.FileName;
            if (sender is Button browseButton) browseButton.IsEnabled = false;
            try
            {
                var unit = GetSelectedTag(LocalTerrainVerticalUnitCombo, "Meters");
                LocalDemMetadataText.Text = LocalizationService.Instance.TranslateLiteral("Inspecting raster…");
                var metadata = await Task.Run(() => LocalTerrainRasterService.Instance.Inspect(dialog.FileName, unit));
                LocalDemMetadataText.Text = metadata.ToDisplayText();
                TerrainTestResultText.Text = LocalizationService.Instance.TranslateLiteral(
                    "Numeric raster validated. Use Test Connection to verify an actual viewport tile in Cesium.");
            }
            catch (Exception ex)
            {
                LocalDemMetadataText.Text = ex.Message;
                SetTerrainConnectionState(false, "Invalid raster");
            }
            finally
            {
                if (sender is Button completedBrowseButton) completedBrowseButton.IsEnabled = true;
            }
        }

        private static void OpenExternalGuidance(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void OnOpenCesiumIonClick(object sender, RoutedEventArgs e) => OpenExternalGuidance("https://ion.cesium.com/");

        private void OnOpenGebcoGuidanceClick(object sender, RoutedEventArgs e) => OpenExternalGuidance("https://www.gebco.net/");

        private static void SelectComboByTag(Selector selector, string? tagValue)
        {
            if (string.IsNullOrEmpty(tagValue)) return;
            foreach (var item in selector.Items)
            {
                if (item is FrameworkElement element && string.Equals(element.Tag?.ToString(), tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
            if (selector.Items.Count > 0) selector.SelectedIndex = 0;
        }

        private static string GetSelectedTag(Selector selector, string defaultValue)
        {
            if (selector.SelectedItem is FrameworkElement { Tag: string tag }) return tag;
            return defaultValue;
        }

        private static bool TryReadGridInterval(TextBox box, out double degrees)
        {
            var raw = box.Text?.Trim() ?? string.Empty;
            return (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees) ||
                    double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out degrees)) &&
                   double.IsFinite(degrees) &&
                   (degrees == 0 || (degrees >= 0.05 && degrees <= 60));
        }

        private static bool TryValidateScreenshotDirectory(string? raw, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            try
            {
                if (!Path.IsPathFullyQualified(raw.Trim())) return false;
                fullPath = Path.GetFullPath(raw.Trim());
                return !string.IsNullOrWhiteSpace(Path.GetPathRoot(fullPath));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (!TryReadGridInterval(LongitudeIntervalInput, out var longitudeInterval) ||
                !TryReadGridInterval(LatitudeIntervalInput, out var latitudeInterval))
            {
                SettingsTabs.SelectedIndex = 4;
                MessageBox.Show("Enter 0 for automatic spacing, or an interval between 0.05° and 60° for both axes.",
                    "Invalid coordinate grid interval", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryValidateScreenshotDirectory(ScreenshotDirectoryInput.Text, out var screenshotDirectory))
            {
                SettingsTabs.SelectedIndex = (int)OptionsDialogTab.BasemapDisplay;
                MessageBox.Show("Choose an absolute local folder for saved screenshots.",
                    "Invalid screenshot folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var terrainProvider = GetSelectedTag(TerrainProviderCombo, "demTerrain");
            var terrainToken = CesiumIonTokenBox.Password.Trim();
            string? customTerrainUrl = null;
            if (terrainProvider == "localDem")
            {
                _terrainPreviewChanged = true;
                try
                {
                    var path = _selectedLocalDemPath ?? string.Empty;
                    var unit = GetSelectedTag(LocalTerrainVerticalUnitCombo, "Meters");
                    if (sender is Button saveButton) saveButton.IsEnabled = false;
                    await Task.Run(() => LocalTerrainRasterService.Instance.Configure(path, unit));
                    if (_closingRequested)
                    {
                        await RestoreOriginalTerrainAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SettingsTabs.SelectedIndex = (int)OptionsDialogTab.TerrainModels;
                    MessageBox.Show(ex.Message, "Invalid local elevation raster", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                finally
                {
                    if (sender is Button saveButton) saveButton.IsEnabled = true;
                }
            }
            if (terrainProvider is "worldTerrain" or "worldBathymetry" && string.IsNullOrWhiteSpace(terrainToken))
            {
                SettingsTabs.SelectedIndex = (int)OptionsDialogTab.TerrainModels;
                MessageBox.Show("This terrain source requires a Cesium ion access token. Paste a valid token or choose a source that does not require one.",
                    "Cesium ion token required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var copernicusToken = CopernicusAccessTokenBox.Password.Trim();
            if (terrainProvider == "copernicusGlo30" && string.IsNullOrWhiteSpace(copernicusToken))
            {
                SettingsTabs.SelectedIndex = (int)OptionsDialogTab.TerrainModels;
                MessageBox.Show("Copernicus GLO-30 requires a CDSE OAuth access token. It does not use Cesium ion.",
                    "Copernicus token required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (terrainProvider == "custom")
            {
                if (!TryValidateCustomTerrainUrl(CustomTerrainUrlBox.Text, out var parsed, out var error))
                {
                    SettingsTabs.SelectedIndex = (int)OptionsDialogTab.TerrainModels;
                    MessageBox.Show(error, "Invalid terrain service URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                customTerrainUrl = parsed!.AbsoluteUri;
            }
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
            OnGroundToleranceInputLostFocus(GroundToleranceInput, new RoutedEventArgs());
            settings.GroundToleranceMeters = GroundToleranceSlider.Value;
            settings.SnapVertices = SnapVerticesCheck.IsChecked == true;
            settings.SnapMidpoints = SnapMidpointsCheck.IsChecked == true;
            settings.SnapEdges = SnapEdgesCheck.IsChecked == true;
            settings.SnapIntersections = SnapIntersectionsCheck.IsChecked == true;

            // Save basemap, theme, language, graticules & display
            settings.DefaultBasemap = GetSelectedTag(DefaultBasemapCombo, ZEarth.Domain.Imagery.ImageryRegistry.ProviderEsriHybrid);
            settings.TerrainProviderId = terrainProvider;
            settings.LocalTerrainRasterPath = terrainProvider == "localDem" ? _selectedLocalDemPath : settings.LocalTerrainRasterPath;
            settings.LocalTerrainVerticalUnit = GetSelectedTag(LocalTerrainVerticalUnitCombo, "Meters");
            settings.ScreenshotDirectory = screenshotDirectory;
            settings.CesiumIonToken = string.IsNullOrWhiteSpace(terrainToken) ? null : terrainToken;
            settings.CopernicusAccessToken = string.IsNullOrWhiteSpace(copernicusToken) ? null : copernicusToken;
            settings.CustomTerrainUrl = customTerrainUrl;
            settings.Theme = GetSelectedTag(ThemeCombo, "Dark");
            settings.Language = GetSelectedTag(LanguageCombo, "en");
            // Labels are automatic now; the persisted legacy flag stays true so
            // older engines that still read it never suppress every label.
            settings.ShowGraticuleLabels = true;
            settings.GridLabelFormat = GetSelectedTag(GridLabelFormatCombo, GridSettingsDefaults.GridLabelFormat);
            settings.ShowKeyParallels = KeyParallelsCheck.IsChecked == true;
            settings.ShowLongitudeLines = LongitudeLinesCheck.IsChecked == true;
            settings.ShowLatitudeLines = LatitudeLinesCheck.IsChecked == true;
            settings.LongitudeIntervalDegrees = longitudeInterval;
            settings.LatitudeIntervalDegrees = latitudeInterval;
            settings.ShowUtmZones = UtmZonesCheck.IsChecked == true;
            settings.UtmLabelMode = GetSelectedTag(UtmLabelModeCombo, GridSettingsDefaults.UtmLabelMode);
            settings.UtmLabelPlacement = GetSelectedTag(UtmLabelPlacementCombo, GridSettingsDefaults.UtmLabelPlacement);
            settings.GridLabelSize = GridLabelSizes[Math.Clamp((int)Math.Round(GridLabelSizeSlider.Value), 0, GridLabelSizes.Length - 1)];
            // One UTM colour value even though it appears in two sections.
            settings.GridUtmColor = GridColor.NormalizeOrFallback(
                UtmColorPicker.ColorHex, GridSettingsDefaults.GridUtmColor);
            settings.GridMajorColor = GridColor.NormalizeOrFallback(
                MajorColorPicker.ColorHex, GridSettingsDefaults.GridMajorColor);
            settings.GridLongitudeColor = GridColor.NormalizeOrFallback(
                LongitudeColorPicker.ColorHex, GridSettingsDefaults.GridLongitudeColor);
            settings.GridLatitudeColor = GridColor.NormalizeOrFallback(
                LatitudeColorPicker.ColorHex, GridSettingsDefaults.GridLatitudeColor);
            settings.GridLabelColor = GridColor.NormalizeOrFallback(
                LabelColorPicker.ColorHex, GridSettingsDefaults.GridLabelColor);
            GridSettingsDefaults.Normalize(settings);
            settings.ShowSceneLocationsOnMap = SceneMarkersCheck.IsChecked == true;
            settings.CreditsDisplayMode = GetSelectedTag(CreditsModeCombo, "Compact");
            settings.ShowOptionalProviderLogos = ProviderLogosCheck.IsChecked == true;
            settings.ZoomSensitivity = ZoomSensitivitySlider.Value;

            // Keep the domain unit and snapping models in sync BEFORE serialization.
            // A later unrelated Save must not silently revert these selections.
            SettingsService.Instance.ApplyToDomain();
            UnitService.Instance.NotifyUnitsChanged();
            SnappingSettings.Instance.NotifySettingsChanged();
            SettingsService.Instance.Save();

            // Apply to globe bridge
            if (_globeBridge != null)
            {
                var basemapAcknowledged = await _globeBridge.SetBasemapAsync(settings.DefaultBasemap);
                if (!basemapAcknowledged)
                {
                    MessageBox.Show("Your basemap preference was saved, but the live globe did not acknowledge the imagery change. The saved source will be retried at startup.",
                        "Basemap engine not ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
                var gridAcknowledged = await _globeBridge.SetGraticulesConfigAsync(
                    settings.ShowGraticules,
                    settings.ShowGraticuleLabels,
                    settings.ShowKeyParallels,
                    settings.ShowUtmZones,
                    settings.ShowLongitudeLines,
                    settings.ShowLatitudeLines,
                    settings.LongitudeIntervalDegrees,
                    settings.LatitudeIntervalDegrees,
                    settings.GridLabelFormat,
                    settings.UtmLabelMode,
                    settings.GridMajorColor,
                    settings.GridLongitudeColor,
                    settings.GridLatitudeColor,
                    settings.GridLabelColor,
                    settings.GridUtmColor,
                    settings.GridLabelSize,
                    settings.UtmLabelPlacement);
                if (!gridAcknowledged)
                {
                    MessageBox.Show("Your settings were saved. The globe did not confirm the coordinate-grid update; it will be reapplied on the next engine startup.",
                        "Grid engine not ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Never trap the user in Options or block theme/language changes
                    // simply because WebView2 has not initialized yet.
                }
                await _globeBridge.SetCreditsConfigAsync(
                    settings.CreditsDisplayMode,
                    settings.ShowOptionalProviderLogos);
                await _globeBridge.SetZoomSensitivityAsync(settings.ZoomSensitivity);
            }

            _committed = true;
            DialogResult = true;
            Close();
        }

        private void OnOptionsDialogClosing(object? sender, CancelEventArgs e)
        {
            _closingRequested = true;
            if (_committed || !_terrainPreviewChanged || _globeBridge == null) return;
            // Test Connection may temporarily apply a provider. Cancel/close restores
            // the provider that was active when the dialog opened.
            _ = RestoreOriginalTerrainAsync();
        }

        private async Task RestoreOriginalTerrainAsync()
        {
            if (_globeBridge == null) return;
            if (string.Equals(_originalTerrainProvider, "localDem", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await Task.Run(() => LocalTerrainRasterService.Instance.Configure(
                        _originalLocalDemPath ?? string.Empty, _originalLocalDemUnit));
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogWarning($"Could not restore the original local terrain: {ex.Message}");
                    await _globeBridge.SetTerrainProviderAsync("ellipsoid");
                    return;
                }
            }
            await _globeBridge.SetTerrainProviderAsync(
                _originalTerrainProvider,
                _originalTerrainProvider == "custom" ? _originalTerrainUrl : null,
                _originalTerrainToken);
        }
    }
}
