using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ZEarth.App.Services;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Domain.Kml;
using ZEarth.Domain.Measurement;
using ZEarth.Domain.Snapping;
using ZEarth.Domain.Settings;
using ZEarth.Domain.Units;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Terrain;
using ZEarth.Domain.Gis;
using ZEarth.App.Views;
using ZEarth.Globe;
using ZEarth.Domain.Layers;
using ZEarth.Domain.Search;

namespace ZEarth.App.ViewModels
{
    public class CrsSearchOption
    {
        public int Epsg { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Display => $"EPSG:{Epsg} — {Name}";
    }

    public sealed class BasemapRibbonItem
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ToolTip { get; init; } = string.Empty;
        public bool IsDefault { get; init; }

        // Distinguish imagery themes at a glance instead of displaying the same globe
        // symbol for every provider. These are icons, not an assertion of source availability.
        // Distinct color families make monochrome Segoe UI symbols readable at a glance.
        public string GlyphBackground => Id switch
        {
            "offline-vector" => "#266A64",
            "osm-standard" or "osm-humanitarian" or "osm-cycling" => "#285A75",
            "opentopo" or "esri-topo" or "esri-physical" or "esri-shaded-relief" => "#56783F",
            "carto-dark" => "#383854",
            "carto-light" => "#8B6836",
            "carto-voyager" => "#8B4C62",
            "esri-imagery" or "esri-hybrid" => "#2D5278",
            "esri-streets" => "#7B543A",
            "esri-gray" => "#596572",
            "esri-ocean" => "#126F81",
            _ => "#526079"
        };

        public string Glyph => Id switch
        {
            "offline-vector" => "🗺",
            "osm-standard" => "🛣",
            "osm-humanitarian" => "🏥",
            "osm-cycling" => "🚲",
            "opentopo" => "⛰",
            "carto-dark" => "🌙",
            "carto-light" => "☀",
            "carto-voyager" => "🧭",
            "esri-imagery" => "🛰",
            "esri-hybrid" => "🌐",
            "esri-streets" => "🚗",
            "esri-topo" => "🏔",
            "esri-gray" => "▦",
            "esri-physical" => "🌍",
            "esri-shaded-relief" => "⛰",
            "esri-ocean" => "🌊",
            _ => "🗺"
        };
    }

    public sealed class BasemapRibbonGroup
    {
        public string ProviderName { get; init; } = string.Empty;
        public IReadOnlyList<BasemapRibbonItem> Items { get; init; } = Array.Empty<BasemapRibbonItem>();
    }

    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly GlobeBridge _globeBridge;
        private readonly SearchService _searchService = new();
        private readonly TemporarySessionService _temporarySessionService = new();
        private readonly HashSet<PlaceItem> _pendingSessionItems = new();
        private readonly List<CameraState> _cameraHistory = new();
        private readonly List<CoordinatePoint> _lastMeasurementPoints = new();
        private int _historyIndex = -1;
        private bool _isNavigatingHistory = false;
        private const int MaxHistorySize = 50;

        private readonly UndoRedoManager _undoRedo = new();
        public UndoRedoManager UndoRedo => _undoRedo;
        public bool CanUndo => _undoRedo.CanUndo;
        public bool CanRedo => _undoRedo.CanRedo;
        public bool IsModified => _projectService.IsModified;

        /// <summary>True while one or more long-running operations are in flight (drives the busy UI).</summary>
        public bool IsBusy => _busyOperations > 0;

        /// <summary>Short human-readable status/busy text for the status bar.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool CanGoPreviousExtent => _historyIndex > 0;
        public bool CanGoNextExtent => _historyIndex < _cameraHistory.Count - 1;
        public int ExtentHistoryCount => _cameraHistory.Count;
        public int ExtentHistoryIndex => _historyIndex;

        private HomePreset _selectedHomePreset = HomePreset.EgyptAfrica;
        public HomePreset SelectedHomePreset
        {
            get => _selectedHomePreset;
            set
            {
                if (SetProperty(ref _selectedHomePreset, value))
                {
                    OnPropertyChanged(nameof(HomeTooltip));
                }
            }
        }

        public string HomeTooltip => SelectedHomePreset switch
        {
            HomePreset.World => "Global Planetary View (World)",
            HomePreset.Custom => "Custom Configured Home View",
            _ => "Regional Home View (Egypt & Nile Basin) (Home)"
        };

        public PrivacySettings PrivacySettings { get; } = new();
        private readonly System.Windows.Threading.DispatcherTimer _autoSaveTimer;

        // Telemetry coalescing (RC-2): inbound camera/center/cursor samples are stored and applied on
        // a low-frequency UI timer instead of updating bindings on every WebView2 message.
        private System.Windows.Threading.DispatcherTimer? _telemetryTimer;
        private CameraState? _pendingCamera;
        private MapCenterInfo? _pendingCenter;
        private CursorPositionInfo? _pendingCursor;
        private MapCenterInfo? _lastKnownCenter;
        private int _warmedCrsEpsg = 4326;

        // Busy / status feedback (RC-3)
        private int _busyOperations;
        private string _statusMessage = "Ready";

        private string _windowTitle = "Z EARTH Desktop GIS - Developed by Ahmad Ibrahim";
        private CameraState _currentCamera = CameraState.CreateHome();
        private SceneMode _currentSceneMode = SceneMode.Scene3D;
        private string _statusCoordinates = "26.8206° N, 30.8025° E";
        private string _statusAltitude = "4,500 km";
        private string _statusHeading = "0.0°";
        private string _statusCrs = "WGS 84 (EPSG:4326)";
        private string _statusProvider = "Offline Local Globe • Zero Network • No Paid Token";
        private string _searchQuery = string.Empty;
        private PlaceItem? _selectedPlace;
        private int _selectedRibbonTab = 1; // Default to 'Home/Navigation'
        private int _selectedSidebarTab = 0; // 0 = Places, 1 = Layers

        private bool _isBordersVisible = true;
        private bool _isGraticulesVisible = false;
        private bool _isRoadsVisible = false;
        private bool _isBuildingsVisible = false;
        private string _activeBasemap = "Esri World Imagery (Hybrid Labels)";

        private bool _isSidebarDockedLeft = true;
        private bool _isSidebarCollapsed = false;
        private double _sidebarWidth = 330;

        public bool IsSidebarDockedLeft
        {
            get => _isSidebarDockedLeft;
            set
            {
                if (SetProperty(ref _isSidebarDockedLeft, value))
                {
                    OnPropertyChanged(nameof(DockToggleLabel));
                    OnPropertyChanged(nameof(DockToggleIcon));
                }
            }
        }

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(CollapseToggleLabel));
                    OnPropertyChanged(nameof(CollapseToggleIcon));
                }
            }
        }

        public double SidebarWidth
        {
            get => _sidebarWidth;
            set => SetProperty(ref _sidebarWidth, value);
        }

        public string DockToggleLabel => IsSidebarDockedLeft ? "Dock Right" : "Dock Left";
        public string DockToggleIcon => IsSidebarDockedLeft ? "⮂" : "⮃";
        public string CollapseToggleLabel => IsSidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar";
        public string CollapseToggleIcon => IsSidebarCollapsed ? "📌" : "✖";

        public AppTheme CurrentTheme => ThemeService.Instance.CurrentTheme;
        public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;
        public bool IsLightTheme => CurrentTheme == AppTheme.Light;
        public string ThemeToggleLabel => IsDarkTheme ? "Switch to Light Theme" : "Switch to Dark Theme";
        public string ThemeToggleIcon => IsDarkTheme ? "☀️" : "🌙";

        public int SelectedSidebarTab
        {
            get => _selectedSidebarTab;
            set => SetProperty(ref _selectedSidebarTab, value);
        }

        public bool IsBordersVisible
        {
            get => _isBordersVisible;
            set
            {
                if (SetProperty(ref _isBordersVisible, value))
                {
                    _ = _globeBridge.SendJsonToWebView?.Invoke(JsonSerializer.Serialize(new { action = "layers.setBorders", payload = new { visible = value } }));
                }
            }
        }

        public bool IsGraticulesVisible
        {
            get => _isGraticulesVisible;
            set
            {
                if (SetProperty(ref _isGraticulesVisible, value))
                {
                    OnPropertyChanged(nameof(GridToggleBrush));
                    SettingsService.Instance.Settings.ShowGraticules = value;
                    SettingsService.Instance.Save();
                    // The ribbon command awaits the globe; no unobserved fire-and-forget state change.
                }
            }
        }

        public Brush GridToggleBrush => IsGraticulesVisible
            ? new SolidColorBrush(Color.FromRgb(74, 222, 128))
            : (Application.Current?.TryFindResource("Brush.Text.Secondary") as Brush ?? Brushes.Gray);

        public bool IsRoadsVisible
        {
            get => _isRoadsVisible;
            set => SetProperty(ref _isRoadsVisible, value);
        }

        public bool IsBuildingsVisible
        {
            get => _isBuildingsVisible;
            set => SetProperty(ref _isBuildingsVisible, value);
        }

        public string ActiveBasemap
        {
            get => _activeBasemap;
            set
            {
                if (SetProperty(ref _activeBasemap, value))
                {
                    OnPropertyChanged(nameof(StatusEngineInfo));
                    OnPropertyChanged(nameof(StatusProvider));
                }
            }
        }

        private readonly ImageryRegistry _imageryRegistry = new();
        public ImageryRegistry ImageryRegistry => _imageryRegistry;
        public IReadOnlyList<ImageryLayer> AvailableBasemaps => _imageryRegistry.Basemaps.ToList();
        public ObservableCollection<BasemapRibbonGroup> BasemapRibbonGroups { get; } = new();
        public string DefaultBasemapDisplay
        {
            get
            {
                var id = SettingsService.Instance.Settings.DefaultBasemap;
                var item = _imageryRegistry.GetLayer(id);
                return $"✓ Default: {item?.Name ?? id}";
            }
        }
        public ObservableCollection<ImageryLayerViewModel> ActiveLayers { get; } = new();

        private bool _isHybridLabelsVisible = true;
        public bool IsHybridLabelsVisible
        {
            get => _isHybridLabelsVisible;
            set
            {
                if (SetProperty(ref _isHybridLabelsVisible, value))
                {
                    _ = _globeBridge.ToggleHybridLabelsAsync(value);
                }
            }
        }

        private readonly TerrainProviderConfig _terrainConfig = TerrainProviderConfig.CreateEllipsoid();
        public TerrainProviderConfig TerrainConfig => _terrainConfig;

        private bool _isTerrainEnabled;
        public bool IsTerrainEnabled
        {
            get => _isTerrainEnabled;
            private set
            {
                if (SetProperty(ref _isTerrainEnabled, value))
                {
                    OnPropertyChanged(nameof(TerrainToggleState));
                    OnPropertyChanged(nameof(TerrainIconBrush));
                }
            }
        }

        public string TerrainToggleState => IsTerrainEnabled ? "ON" : "OFF";
        public Brush TerrainIconBrush => IsTerrainEnabled
            ? new SolidColorBrush(Color.FromRgb(163, 230, 53))
            : (Application.Current?.TryFindResource("Brush.Text.Secondary") as Brush ?? Brushes.Gray);

        private string _selectedBasemapId = ImageryRegistry.ProviderEsriHybrid;
        public string SelectedBasemapId
        {
            get => _selectedBasemapId;
            set
            {
                if (SetProperty(ref _selectedBasemapId, value))
                {
                    ExecuteSelectBasemap(value);
                }
            }
        }

        private string _compassMode = "cardinal";
        public string CompassMode
        {
            get => _compassMode;
            set
            {
                if (SetProperty(ref _compassMode, value))
                {
                    _ = _globeBridge.SetCompassModeAsync(value);
                }
            }
        }

        private bool _isSunlightEnabled = false;
        public bool IsSunlightEnabled
        {
            get => _isSunlightEnabled;
            set
            {
                if (SetProperty(ref _isSunlightEnabled, value))
                {
                    // Change appearance only when the engine echoed a state or an action was acknowledged.
                    OnPropertyChanged(nameof(SunlightIconBrush));
                }
            }
        }

        public Brush SunlightIconBrush => IsSunlightEnabled
            ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) // #fbbf24 Warm amber / sun gold
            : (Application.Current?.TryFindResource("Brush.Text.Secondary") as Brush ?? Brushes.Gray);

        private bool _isAtmosphereEnabled = true;
        public bool IsAtmosphereEnabled
        {
            get => _isAtmosphereEnabled;
            set
            {
                if (SetProperty(ref _isAtmosphereEnabled, value))
                {
                    OnPropertyChanged(nameof(AtmosphereIconBrush));
                }
            }
        }

        public Brush AtmosphereIconBrush => IsAtmosphereEnabled
            ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) // #38bdf8 Atmospheric Sky
            : (Application.Current?.TryFindResource("Brush.Text.Secondary") as Brush ?? Brushes.Gray);

        // UTC is unambiguous; Cesium's sun/terminator is geographic and varies by the
        // map location. Never invent a country's civil time zone from longitude.
        private DateTime? _sunlightDateUtc = DateTime.UtcNow.Date;
        public DateTime? SunlightDateUtc
        {
            get => _sunlightDateUtc;
            set
            {
                if (SetProperty(ref _sunlightDateUtc, value?.Date))
                {
                    if (IsSunlightEnabled)
                        _ = _globeBridge.SetTimeOfDayAsync(_timeOfDayHours,
                            _sunlightDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
            }
        }

        private double _timeOfDayHours = 12.0; // Known stable noon default; date and UTC hours remain user-selectable.
        public double TimeOfDayHours
        {
            get => _timeOfDayHours;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetProperty(ref _timeOfDayHours, Math.Round(clamped, 2)))
                {
                    UpdateTimeOfDayDisplay();
                    if (IsSunlightEnabled)
                    {
                        _ = _globeBridge.SetTimeOfDayAsync(_timeOfDayHours,
                            SunlightDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        private string _timeOfDayDisplay = "12:00 PM (UTC)";
        public string TimeOfDayDisplay
        {
            get => _timeOfDayDisplay;
            set => SetProperty(ref _timeOfDayDisplay, value);
        }

        private void UpdateTimeOfDayDisplay()
        {
            int h = (int)Math.Floor(_timeOfDayHours);
            int m = (int)Math.Floor((_timeOfDayHours - h) * 60.0);
            if (h == 24) h = 0;
            string ampm = h >= 12 ? "PM" : "AM";
            int displayHour = h % 12;
            if (displayHour == 0) displayHour = 12;
            TimeOfDayDisplay = $"{displayHour}:{m:D2} {ampm} (UTC)";
        }

        private double _verticalExaggeration = 1.0;
        private int _exaggerationRevision;
        private string _latestTerrainProbe = "No verified geographic DEM samples recorded.";
        public double VerticalExaggeration
        {
            get => _verticalExaggeration;
            set
            {
                var clamped = Math.Max(TerrainProviderConfig.MinExaggeration, Math.Min(TerrainProviderConfig.MaxExaggeration, value));
                if (SetProperty(ref _verticalExaggeration, Math.Round(clamped, 2)))
                {
                    OnPropertyChanged(nameof(VerticalExaggerationDisplay));
                    // Slider changes are debounced; acknowledge only a real active terrain.
                    _ = ApplyVerticalExaggerationAsync(++_exaggerationRevision);
                }
            }
        }

        public string VerticalExaggerationDisplay => $"{_verticalExaggeration:0.0}x";

        private async Task ApplyVerticalExaggerationAsync(int revision)
        {
            await Task.Delay(120); // Avoid a WebView2 request for every slider tick.
            if (revision != _exaggerationRevision) return;
            if (!IsTerrainEnabled)
            {
                StatusMessage = $"Exaggeration {_verticalExaggeration:0.0}x selected; load a DEM to apply it.";
                return;
            }
            var requested = _verticalExaggeration;
            var ok = await _globeBridge.SetTerrainExaggerationAsync(requested);
            if (revision == _exaggerationRevision)
                StatusMessage = ok
                    ? $"Terrain exaggeration applied: {requested:0.0}x (verify visible relief)."
                    : "Terrain exaggeration was not acknowledged by the 3D engine.";
        }

        private string _terrainProviderType = "WGS84 Smooth Ellipsoid (Offline)";
        public string TerrainProviderType
        {
            get => _terrainProviderType;
            set => SetProperty(ref _terrainProviderType, value);
        }

        private string _activeAttribution = "Source: Esri, Maxar, Earthstar Geographics, CNES/Airbus DS, USDA, USGS, AeroGRID, IGN, and GIS User Community";
        public string ActiveAttribution
        {
            get => _activeAttribution;
            set
            {
                if (SetProperty(ref _activeAttribution, value))
                {
                    OnPropertyChanged(nameof(StatusEngineInfo));
                    OnPropertyChanged(nameof(StatusProvider));
                }
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public CameraState CurrentCamera
        {
            get => _currentCamera;
            set
            {
                if (SetProperty(ref _currentCamera, value))
                {
                    UpdateTelemetryStrings();
                }
            }
        }

        public SceneMode CurrentSceneMode
        {
            get => _currentSceneMode;
            set
            {
                if (SetProperty(ref _currentSceneMode, value))
                {
                    OnPropertyChanged(nameof(Is3DMode));
                    OnPropertyChanged(nameof(Is2DMode));
                    OnPropertyChanged(nameof(StatusViewMode));
                    OnPropertyChanged(nameof(StatusEngineInfo));
                }
            }
        }

        public string StatusEngineInfo => $"CesiumJS 1.146.0 • {(CurrentSceneMode == SceneMode.Scene3D ? "3D Globe" : "2D Map")} • {ActiveBasemap} • {ActiveAttribution}";

        public bool Is3DMode
        {
            get => CurrentSceneMode == SceneMode.Scene3D;
            set
            {
                if (value && CurrentSceneMode != SceneMode.Scene3D)
                {
                    CurrentSceneMode = SceneMode.Scene3D;
                    _ = _globeBridge.SetSceneModeAsync(SceneMode.Scene3D);
                }
                OnPropertyChanged(nameof(Is3DMode));
                OnPropertyChanged(nameof(Is2DMode));
            }
        }

        public bool Is2DMode
        {
            get => CurrentSceneMode == SceneMode.Scene2D;
            set
            {
                if (value && CurrentSceneMode != SceneMode.Scene2D)
                {
                    CurrentSceneMode = SceneMode.Scene2D;
                    _ = _globeBridge.SetSceneModeAsync(SceneMode.Scene2D);
                }
                OnPropertyChanged(nameof(Is3DMode));
                OnPropertyChanged(nameof(Is2DMode));
            }
        }

        private CoordinateDisplayFormat _selectedCoordinateFormat = CoordinateDisplayFormat.DecimalDegrees;
        public CoordinateDisplayFormat SelectedCoordinateFormat
        {
            get => _selectedCoordinateFormat;
            set
            {
                if (SetProperty(ref _selectedCoordinateFormat, value))
                {
                    UpdateTelemetryStrings();
                }
            }
        }

        private string _statusCenterCoordinates = "Center: 26.8206° N, 30.8025° E";
        public string StatusCenterCoordinates
        {
            get => _statusCenterCoordinates;
            set => SetProperty(ref _statusCenterCoordinates, value);
        }

        private string _statusCursorCoordinates = "Cursor: 26.8206° N, 30.8025° E";
        public string StatusCursorCoordinates
        {
            get => _statusCursorCoordinates;
            set => SetProperty(ref _statusCursorCoordinates, value);
        }

        private string _statusCursorElevation = "Elev: 0 m";
        public string StatusCursorElevation
        {
            get => _statusCursorElevation;
            set => SetProperty(ref _statusCursorElevation, value);
        }

        public string StatusViewMode => CurrentSceneMode == SceneMode.Scene3D ? "3D Globe" : "2D Map";

        public string StatusCoordinates
        {
            get => _statusCoordinates;
            set => SetProperty(ref _statusCoordinates, value);
        }

        public string StatusAltitude
        {
            get => _statusAltitude;
            set => SetProperty(ref _statusAltitude, value);
        }

        public string StatusHeading
        {
            get => _statusHeading;
            set => SetProperty(ref _statusHeading, value);
        }

        public string StatusCrs
        {
            get => _statusCrs;
            set => SetProperty(ref _statusCrs, value);
        }

        public string StatusProvider
        {
            get => _statusProvider;
            set => SetProperty(ref _statusProvider, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public PlaceItem? SelectedPlace
        {
            get => _selectedPlace;
            set => SetProperty(ref _selectedPlace, value);
        }

        public int SelectedRibbonTab
        {
            get => _selectedRibbonTab;
            set => SetProperty(ref _selectedRibbonTab, value);
        }

        public ProjectDocument CurrentProject => _projectService.CurrentProject;
        public ObservableCollection<PlaceItem> MyPlaces => CurrentProject.MyPlaces;
        public PlaceItem TemporaryPlacesFolder => CurrentProject.EnsureTemporaryPlacesFolder();
        public ObservableCollection<PlaceItem> TemporaryPlaces => TemporaryPlacesFolder.Children;
        public ObservableCollection<PlaceItem> SavedScenes => CurrentProject.SavedScenes;

        public GlobeBridge GlobeBridge => _globeBridge;

        // The Explorer intentionally has two top-level sections: Search and My Places.
        private bool _isSearchExpanded = true;
        private bool _isMyPlacesExpanded = true;

        public bool IsSearchExpanded
        {
            get => _isSearchExpanded;
            set => SetProperty(ref _isSearchExpanded, value);
        }

        public bool IsMyPlacesExpanded
        {
            get => _isMyPlacesExpanded;
            set => SetProperty(ref _isMyPlacesExpanded, value);
        }

        // Search Section State (Two explicit modes: By Name & By Coordinates)
        private int _selectedSearchMode = 0; // 0 = By Name, 1 = By Coordinates
        public int SelectedSearchMode
        {
            get => _selectedSearchMode;
            set => SetProperty(ref _selectedSearchMode, value);
        }

        private string _searchNameQuery = string.Empty;
        public string SearchNameQuery
        {
            get => _searchNameQuery;
            set => SetProperty(ref _searchNameQuery, value);
        }

        private string _searchCoordinateX = string.Empty;
        public string SearchCoordinateX
        {
            get => _searchCoordinateX;
            set => SetProperty(ref _searchCoordinateX, value);
        }

        private string _searchCoordinateY = string.Empty;
        public string SearchCoordinateY
        {
            get => _searchCoordinateY;
            set => SetProperty(ref _searchCoordinateY, value);
        }

        private int _selectedSearchCrsEpsg = 4326;
        public int SelectedSearchCrsEpsg
        {
            get => _selectedSearchCrsEpsg;
            set
            {
                if (SetProperty(ref _selectedSearchCrsEpsg, value))
                {
                    OnPropertyChanged(nameof(SearchCoordinateXLabel));
                    OnPropertyChanged(nameof(SearchCoordinateYLabel));
                    OnPropertyChanged(nameof(CoordinateFormatPlaceholderX));
                    OnPropertyChanged(nameof(CoordinateFormatPlaceholderY));
                }
            }
        }

        public string SearchCoordinateXLabel => (_selectedSearchCrsEpsg == 4326 || _selectedSearchCrsEpsg == 4979)
            ? "X — Longitude (degrees):"
            : "X — Easting (m):";

        public string SearchCoordinateYLabel => (_selectedSearchCrsEpsg == 4326 || _selectedSearchCrsEpsg == 4979)
            ? "Y — Latitude (degrees):"
            : "Y — Northing (m):";

        public string CoordinateFormatPlaceholderX => (_selectedSearchCrsEpsg == 4326 || _selectedSearchCrsEpsg == 4979)
            ? "e.g. 31.2357 or 31° 14' 8.5\" E"
            : "e.g. 615000.0";

        public string CoordinateFormatPlaceholderY => (_selectedSearchCrsEpsg == 4326 || _selectedSearchCrsEpsg == 4979)
            ? "e.g. 30.0444 or 30° 2' 39.8\" N"
            : "e.g. 810000.0";

        private string _coordinateValidationMessage = string.Empty;
        public string CoordinateValidationMessage
        {
            get => _coordinateValidationMessage;
            set => SetProperty(ref _coordinateValidationMessage, value);
        }

        private bool _isCoordinateValidationSuccess = true;
        public bool IsCoordinateValidationSuccess
        {
            get => _isCoordinateValidationSuccess;
            set => SetProperty(ref _isCoordinateValidationSuccess, value);
        }

        private bool _isSearching = false;
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public ObservableCollection<SearchResultItem> SearchResults { get; } = new();

        private SearchResultItem? _selectedSearchResult;
        public SearchResultItem? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        public List<CrsSearchOption> AvailableSearchCrsList { get; } = new()
        {
            new CrsSearchOption { Epsg = 4326, Name = "WGS 84 (Geographic Lon/Lat)" },
            new CrsSearchOption { Epsg = 4979, Name = "WGS 84 3D (Geographic Ellipsoidal)" },
            new CrsSearchOption { Epsg = 22992, Name = "Egypt 1907 / Red Belt (Cairo & Nile Valley)" },
            new CrsSearchOption { Epsg = 32636, Name = "WGS 84 / UTM Zone 36N (Egypt East & Red Sea)" },
            new CrsSearchOption { Epsg = 32635, Name = "WGS 84 / UTM Zone 35N (Egypt West)" },
            new CrsSearchOption { Epsg = 3857, Name = "WGS 84 / Pseudo-Mercator (Web)" },
            new CrsSearchOption { Epsg = 22991, Name = "Egypt 1907 / Purple Belt" },
            new CrsSearchOption { Epsg = 22993, Name = "Egypt 1907 / Blue Belt" }
        };

        // Sidebar Navigation & Action Commands
        public ICommand ExpandAllSectionsCommand { get; }
        public ICommand CollapseAllSectionsCommand { get; }
        public ICommand SearchByNameCommand { get; }
        public ICommand LocateCoordinatesCommand { get; }
        public ICommand FlyToSearchResultCommand { get; }
        public ICommand SaveSearchResultToMyPlacesCommand { get; }
        public ICommand SaveSearchResultToTemporaryPlacesCommand { get; }
        public ICommand SetAsStartupSceneCommand { get; }
        public ICommand MoveSavedSceneUpCommand { get; }
        public ICommand MoveSavedSceneDownCommand { get; }
        public ICommand OpenPropertiesCommand { get; }
        public ICommand KeepInTemporaryCommand { get; }
        public ICommand SaveTemporaryToProjectCommand { get; }
        public ICommand MoveToMyPlacesCommand { get; }
        public ICommand MoveToTemporaryPlacesCommand { get; }
        public ICommand ExportSelectedItemCommand { get; }
        public ICommand ExportAllMyPlacesCommand { get; }
        public ICommand ExportAllTemporaryPlacesCommand { get; }
        public ICommand RemoveFromSessionCommand { get; }
        public ICommand ExportTemporaryKmlCommand { get; }
        public ICommand AddPlacemarkToTemporaryCommand { get; }
        public ICommand DrawLineToTemporaryCommand { get; }
        public ICommand DrawPolygonToTemporaryCommand { get; }
        public ICommand AddFolderToTemporaryCommand { get; }
        public ICommand ImportKmlToTemporaryCommand { get; }
        public ICommand SetCompassModeCommand { get; }
        public ICommand ToggleSunlightCommand { get; }
        public ICommand ToggleAtmosphereCommand { get; }

        // Commands
        public ICommand HomeCommand { get; }
        public ICommand FullExtentCommand { get; }
        public ICommand FitSelectionCommand { get; }
        public ICommand SetHomePresetCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetNorthCommand { get; }
        public ICommand ResetPitchCommand { get; }
        public RelayCommand PreviousExtentCommand { get; }
        public RelayCommand NextExtentCommand { get; }
        public ICommand Set3DModeCommand { get; }
        public ICommand Set2DModeCommand { get; }

        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand SaveAsProjectCommand { get; }
        public ICommand ExportKmlCommand { get; }
        public ICommand ExportKmzCommand { get; }
        public ICommand ImportKmlCommand { get; }
        public ICommand ToggleCoordinateFormatCommand { get; }
        public ICommand SelectCrsCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public ICommand SearchCommand { get; }
        public ICommand FlyToPlaceCommand { get; }
        public ICommand AddPlacemarkCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand DeletePlaceCommand { get; }
        public ICommand SaveToMyPlacesCommand { get; }
        public ICommand AddSavedSceneCommand { get; }
        public ICommand RenamePlaceCommand { get; }

        public ICommand DrawPlacemarkCommand { get; }
        public ICommand DrawLineCommand { get; }
        public ICommand DrawPolygonCommand { get; }
        public ICommand DrawCircleCommand { get; }
        public ICommand DrawRectangleCommand { get; }
        public ICommand OverlayImageCommand { get; }
        public ICommand Model3DCommand { get; }

        public ICommand MeasureDistanceCommand { get; }
        public ICommand MeasureAreaCommand { get; }
        public ICommand ClearMeasurementCommand { get; }
        public ICommand ToggleSnapCommand { get; }
        public ICommand ElevationProfileCommand { get; }

        public ICommand ToggleBordersCommand { get; }
        public ICommand ToggleGraticulesCommand { get; }
        public ICommand GridOptionsCommand { get; }
        public ICommand ToggleTerrainCommand { get; }
        public ICommand SelectBasemapCommand { get; }
        public ICommand HistoricalImageryCommand { get; }
        public ICommand AddImageryLayerCommand { get; }
        public ICommand RemoveImageryLayerCommand { get; }
        public ICommand MoveLayerUpCommand { get; }
        public ICommand MoveLayerDownCommand { get; }
        public ICommand SetVerticalExaggerationCommand { get; }
        public ICommand ResetVerticalExaggerationCommand { get; }

        public ICommand CoordinateConverterCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand OptionsCommand { get; }
        public ICommand FlyToCoordinatesCommand { get; }

        public ICommand ToggleSidebarDockCommand { get; }
        public ICommand ToggleSidebarCollapseCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ToggleLanguageCommand { get; }
        public ICommand ImportVectorCommand { get; }

        public string CurrentLanguageCode => LocalizationService.Instance.CurrentLanguage;
        public string CurrentLanguageText => LocalizationService.Instance.IsArabic ? "English (EN)" : "العربية (AR)";

        public ICommand AboutCommand { get; }
        public ICommand DiagnosticsCommand { get; }

        public MainViewModel(IProjectService projectService, GlobeBridge globeBridge)
        {
            _projectService = projectService;
            _globeBridge = globeBridge;

            var configuredBasemap = SettingsService.Instance.Settings.DefaultBasemap;
            if (!string.IsNullOrWhiteSpace(configuredBasemap) && _imageryRegistry.GetLayer(configuredBasemap) != null)
            {
                _selectedBasemapId = configuredBasemap;
            }

            _imageryRegistry.SetBasemap(_selectedBasemapId);
            var defaultBasemap = _imageryRegistry.GetLayer(_selectedBasemapId);
            if (defaultBasemap != null)
            {
                _activeBasemap = defaultBasemap.Name;
                _activeAttribution = defaultBasemap.Attribution;
            }
            RefreshBasemapRibbonGroups();
            SyncTerrainSettingsDisplay();

            _globeBridge.CameraChanged += OnGlobeCameraChanged;
            _globeBridge.CameraMoveEnded += OnGlobeCameraMoveEnded;
            _globeBridge.SceneModeChanged += (s, m) =>
            {
                void update()
                {
                    CurrentSceneMode = m;
                    OnPropertyChanged(nameof(StatusViewMode));
                    OnPropertyChanged(nameof(StatusEngineInfo));
                    OnPropertyChanged(nameof(Is3DMode));
                    OnPropertyChanged(nameof(Is2DMode));
                }

                // Never block the raising thread on the UI thread (RC-4).
                RunOnUi(update);
            };
            _globeBridge.MapCenterChanged += (s, center) =>
            {
                _lastKnownCenter = center;
                if (Application.Current?.Dispatcher == null)
                {
                    UpdateCenterCoordinates(center);
                    return;
                }
                RunOnUi(() => _pendingCenter = center);
            };
            _globeBridge.CursorPositionChanged += (s, pos) =>
            {
                if (Application.Current?.Dispatcher == null)
                {
                    UpdateCursorCoordinates(pos);
                    return;
                }
                RunOnUi(() => _pendingCursor = pos);
            };
            _globeBridge.DrawCompleted += OnGlobeDrawCompleted;
            _globeBridge.RequestFailed += (s, failure) =>
            {
                RunOnUi(() =>
                {
                    if (failure.TimedOut)
                    {
                        LoggerService.Instance.LogWarning(
                            $"Globe request '{failure.Action}' {failure.RequestId} timed out: {failure.Reason}");
                        StatusMessage = $"Globe request timed out: {failure.Action}";
                    }
                    else
                    {
                        // Immediate, non-timeout failures usually mean an action the engine does not
                        // implement; recorded at info level so they are visible without being alarming.
                        LoggerService.Instance.LogInfo(
                            $"Globe request '{failure.Action}' {failure.RequestId} not completed: {failure.Reason}");
                    }
                });
            };
            _globeBridge.MeasurementUpdated += OnGlobeMeasurementUpdated;
            _globeBridge.MeasurementCompleted += OnGlobeMeasurementCompleted;
            _globeBridge.LayerAttributionChanged += (s, attr) =>
            {
                Application.Current?.Dispatcher?.Invoke(() => ActiveAttribution = attr);
            };
            _globeBridge.BasemapChanged += (s, bmap) =>
            {
                void update()
                {
                    _selectedBasemapId = bmap;
                    OnPropertyChanged(nameof(SelectedBasemapId));
                    var layer = _imageryRegistry.GetLayer(bmap);
                    if (layer != null)
                    {
                        ActiveBasemap = layer.Name;
                        ActiveAttribution = layer.Attribution;
                    }
                    else if (bmap == "wayback-live")
                    {
                        ActiveBasemap = "Esri Wayback historical archive";
                        ActiveAttribution = "© Esri World Imagery Wayback (archived basemap release)";
                    }
                    OnPropertyChanged(nameof(StatusEngineInfo));
                }

                // Never block the raising thread on the UI thread (RC-4).
                RunOnUi(update);
            };
            _globeBridge.TerrainChanged += (s, info) =>
            {
                var summary = "Provider activated; no numeric DEM probe reported by this source.";
                try
                {
                    using var doc = JsonDocument.Parse(info);
                    if (doc.RootElement.TryGetProperty("probe", out var probe) &&
                        probe.TryGetProperty("level", out var level) &&
                        probe.TryGetProperty("x", out var x) &&
                        probe.TryGetProperty("y", out var y) &&
                        probe.TryGetProperty("sampleCount", out var count) &&
                        probe.TryGetProperty("minimumElevationM", out var minimum) &&
                        probe.TryGetProperty("maximumElevationM", out var maximum))
                    {
                        summary = $"DEM z{level.GetInt32()}/{x.GetInt32()}/{y.GetInt32()}, " +
                                  $"{count.GetInt32()} heights, range {minimum.GetDouble():0.0} to {maximum.GetDouble():0.0} m";
                    }
                }
                catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
                {
                    summary = "Terrain activated; diagnostic sample could not be parsed.";
                }
                _latestTerrainProbe = summary;
                LoggerService.Instance.LogInfo("Terrain numerical verification: " + summary);
            };
            _globeBridge.TerrainFallbackOccurred += (s, info) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    IsTerrainEnabled = false;
                    SyncTerrainSettingsDisplay(updateEnabledState: false);
                    UpdateTerrainStatusDisplay();
                    string message = "Provider unavailable: network, credentials or terrain tiles failed.";
                    try
                    {
                        using var doc = JsonDocument.Parse(info);
                        if (doc.RootElement.TryGetProperty("reason", out var reason) &&
                            reason.ValueKind == JsonValueKind.String)
                            message = reason.GetString() ?? message;
                    }
                    catch (JsonException) { }
                    StatusMessage = $"Terrain OFF — {message}";
                    _latestTerrainProbe = "No active numerical DEM: " + message;
                    LoggerService.Instance.LogWarning($"Terrain reverted to ellipsoid: {message}");
                });
            };
            _globeBridge.TimeOfDayChanged += (s, time) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _timeOfDayHours = Math.Round(Math.Max(0.0, Math.Min(24.0, time)), 2);
                    OnPropertyChanged(nameof(TimeOfDayHours));
                    UpdateTimeOfDayDisplay();
                });
            };
            _globeBridge.SunlightChanged += (s, enabled) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _isSunlightEnabled = enabled;
                    OnPropertyChanged(nameof(IsSunlightEnabled));
                    OnPropertyChanged(nameof(SunlightIconBrush));
                });
            };
            _globeBridge.AtmosphereChanged += (s, enabled) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _isAtmosphereEnabled = enabled;
                    OnPropertyChanged(nameof(IsAtmosphereEnabled));
                    OnPropertyChanged(nameof(AtmosphereIconBrush));
                });
            };

            // ActiveLayers strictly represents custom user overlay layers (WMS, WMTS, XYZ, GeoTIFF, etc.)
            // Built-in basemaps are mutually exclusive base layers managed by SelectedBasemapId / AvailableBasemaps.
            foreach (var l in _imageryRegistry.CustomOverlays)
            {
                ActiveLayers.Add(new ImageryLayerViewModel(l, OnLayerVisibilityChanged, OnLayerOpacityChanged));
            }

            // Theme service subscription
            ThemeService.Instance.ThemeChanged += (s, theme) =>
            {
                OnPropertyChanged(nameof(CurrentTheme));
                OnPropertyChanged(nameof(IsDarkTheme));
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(ThemeToggleLabel));
                OnPropertyChanged(nameof(ThemeToggleIcon));
            };

            ToggleThemeCommand = new RelayCommand(ExecuteToggleTheme);
            ToggleSidebarDockCommand = new RelayCommand(() => IsSidebarDockedLeft = !IsSidebarDockedLeft);
            ToggleSidebarCollapseCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);

            // Undo / Redo state synchronization (ZE-029)
            _undoRedo.StateChanged += (s, e) =>
            {
                void Update()
                {
                    try { CommandManager.InvalidateRequerySuggested(); } catch { }
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                }

                // Never block the raising thread on the UI thread (RC-4).
                RunOnUi(Update);
            };

            // Project modification listener (ZE-026)
            _projectService.ModifiedChanged += (s, e) =>
            {
                void Update()
                {
                    UpdateWindowTitle();
                    OnPropertyChanged(nameof(IsModified));
                }

                // Never block the raising thread on the UI thread (RC-4).
                RunOnUi(Update);
            };

            // Periodic background autosave timer (ZE-027, AC-012)
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoSaveTimer.Tick += async (s, e) =>
            {
                if (_projectService.IsModified)
                {
                    await _projectService.AutoSaveAsync();
                }
            };
            _autoSaveTimer.Start();

            // Coalesced telemetry pump (RC-2)
            EnsureTelemetryTimer();

            // Handle host commands from bridge/scripts (decoupled via BeginInvoke to prevent WebView2 pump deadlocks)
            _globeBridge.CommandRequested += (s, cmd) =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (cmd == "theme.toggle") ExecuteToggleTheme();
                    else if (cmd == "sidebar.toggleDock") IsSidebarDockedLeft = !IsSidebarDockedLeft;
                    else if (cmd == "sidebar.toggleCollapse") IsSidebarCollapsed = !IsSidebarCollapsed;
                    else if (cmd == "nav.prevExtent") ExecutePreviousExtent();
                    else if (cmd == "nav.nextExtent") ExecuteNextExtent();
                    else if (cmd == "nav.setHomePreset.world") SelectedHomePreset = HomePreset.World;
                    else if (cmd == "nav.setHomePreset.egypt") SelectedHomePreset = HomePreset.EgyptAfrica;
                    else if (cmd == "places.addPlacemark") ExecuteAddPlacemark();
                    else if (cmd == "places.addFolder") ExecuteAddFolder();
                    else if (cmd == "places.deleteSelected") ExecuteDeletePlace();
                    else if (cmd == "places.saveToMyPlaces") ExecuteSaveToMyPlaces();
                    else if (cmd == "places.addScene") ExecuteAddSavedScene();
                    else if (cmd == "project.save") _ = ExecuteSaveProjectAsync();
                    else if (cmd == "project.undo") { if (_undoRedo.Undo()) { _projectService.IsModified = true; UpdatePlacesOnGlobe(); } }
                    else if (cmd == "project.redo") { if (_undoRedo.Redo()) { _projectService.IsModified = true; UpdatePlacesOnGlobe(); } }
                    else if (cmd == "crs.format.toggle") ExecuteToggleCoordinateFormat();
                    else if (cmd == "crs.select") ExecuteSelectCrs();
                    else if (cmd.StartsWith("kml.import"))
                    {
                        var path = cmd.Contains(':') ? cmd.Substring(cmd.IndexOf(':') + 1) : null;
                        ExecuteImportKml(path);
                    }
                    else if (cmd == "kml.export") ExecuteExportKml();
                    else if (cmd == "draw.start.point") _ = _globeBridge.StartDrawingAsync("point");
                    else if (cmd == "draw.start.path") _ = _globeBridge.StartDrawingAsync("path");
                    else if (cmd == "draw.start.polygon") _ = _globeBridge.StartDrawingAsync("polygon");
                    else if (cmd == "draw.start.circle") _ = _globeBridge.StartDrawingAsync("circle", "#22A8DE");
                    else if (cmd == "swipe.stop") ExecuteStopSwipe();
                    else if (cmd == "options.show") ExecuteShowOptions();
                    else if (cmd.StartsWith("layers.setBasemap."))
                    {
                        var bmapId = cmd.Substring("layers.setBasemap.".Length);
                        ExecuteSelectBasemap(bmapId);
                    }
                    else if (cmd.StartsWith("terrain.setExaggeration."))
                    {
                        var valStr = cmd.Substring("terrain.setExaggeration.".Length);
                        if (double.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ex))
                        {
                            ExecuteSetVerticalExaggeration(ex);
                        }
                    }
                    else if (cmd == "terrain.toggle")
                    {
                        _ = ExecuteToggleTerrainAsync();
                    }
                    else if (cmd.StartsWith("vector.import"))
                    {
                        var path = cmd.Contains(':') ? cmd.Substring(cmd.IndexOf(':') + 1) : null;
                        ExecuteImportVector(path);
                    }
                    else if (cmd == "locale.toggle")
                    {
                        LocalizationService.Instance.ToggleLanguage(Application.Current?.MainWindow);
                        OnPropertyChanged(nameof(CurrentLanguageCode));
                        OnPropertyChanged(nameof(CurrentLanguageText));
                        UpdateWindowTitle();
                    }
                    else if (cmd == "app.about")
                    {
                        ShowAbout();
                    }
                }));
            };

            // Navigation Commands (Sprint 2)
            HomeCommand = AsyncCommand(() => _globeBridge.HomePresetAsync(SelectedHomePreset), "Home");
            FullExtentCommand = AsyncCommand(() => _globeBridge.FullExtentAsync(), "Full extent");
            FitSelectionCommand = AsyncCommand(ExecuteFitSelectionAsync, "Fit selection");
            SetHomePresetCommand = new RelayCommand(p =>
            {
                if (p is HomePreset preset) SelectedHomePreset = preset;
                else if (p is string s && Enum.TryParse<HomePreset>(s, out var parsed)) SelectedHomePreset = parsed;
            });
            ZoomInCommand = AsyncCommand(() => _globeBridge.ZoomInAsync(), "Zoom in");
            ZoomOutCommand = AsyncCommand(() => _globeBridge.ZoomOutAsync(), "Zoom out");
            ResetNorthCommand = AsyncCommand(() => _globeBridge.ResetNorthAsync(), "Reset north");
            ResetPitchCommand = AsyncCommand(() => _globeBridge.ResetPitchAsync(), "Reset pitch");

            PreviousExtentCommand = new RelayCommand(ExecutePreviousExtent, () => CanGoPreviousExtent);
            NextExtentCommand = new RelayCommand(ExecuteNextExtent, () => CanGoNextExtent);

            Set3DModeCommand = AsyncCommand(async () =>
            {
                CurrentSceneMode = SceneMode.Scene3D;
                await _globeBridge.SetSceneModeAsync(SceneMode.Scene3D);
            }, "Switch to 3D");
            Set2DModeCommand = AsyncCommand(async () =>
            {
                CurrentSceneMode = SceneMode.Scene2D;
                await _globeBridge.SetSceneModeAsync(SceneMode.Scene2D);
            }, "Switch to 2D");

            SetCompassModeCommand = new RelayCommand(p =>
            {
                if (p is string m && !string.IsNullOrEmpty(m)) CompassMode = m;
            });

            ToggleSunlightCommand = AsyncCommand(ExecuteToggleSunlightAsync, "Toggle sunlight");
            ToggleAtmosphereCommand = AsyncCommand(ExecuteToggleAtmosphereAsync, "Toggle atmosphere");

            // Project Commands (Sprint 3 & Sprint 4)
            NewProjectCommand = new RelayCommand(ExecuteNewProject);
            OpenProjectCommand = AsyncCommand(() => RunBusyAsync("Opening project…", ExecuteOpenProjectAsync), "Open project");
            SaveProjectCommand = AsyncCommand(() => RunBusyAsync("Saving project…", ExecuteSaveProjectAsync), "Save project");
            SaveAsProjectCommand = AsyncCommand(() => RunBusyAsync("Saving project…", ExecuteSaveAsProjectAsync), "Save project as");
            ExportKmlCommand = new RelayCommand(ExecuteExportKml);
            ExportKmzCommand = new RelayCommand(ExecuteExportKmz);
            ImportKmlCommand = new RelayCommand(p => ExecuteImportKml(p as string));
            ToggleCoordinateFormatCommand = new RelayCommand(ExecuteToggleCoordinateFormat);
            SelectCrsCommand = new RelayCommand(ExecuteSelectCrs);
            UndoCommand = new RelayCommand(() => { if (_undoRedo.Undo()) { _projectService.IsModified = true; UpdatePlacesOnGlobe(); } }, () => _undoRedo.CanUndo);
            RedoCommand = new RelayCommand(() => { if (_undoRedo.Redo()) { _projectService.IsModified = true; UpdatePlacesOnGlobe(); } }, () => _undoRedo.CanRedo);

            // Places Commands (Sprint 3 & Sidebar Phase 01)
            SearchCommand = AsyncCommand(() => RunBusyAsync("Searching…", ExecuteSearchByNameAsync), "Search");
            SearchByNameCommand = AsyncCommand(() => RunBusyAsync("Searching…", ExecuteSearchByNameAsync), "Search by name");
            LocateCoordinatesCommand = AsyncCommand(ExecuteLocateCoordinatesAsync, "Locate coordinates");
            FlyToSearchResultCommand = AsyncCommand(p => ExecuteFlyToSearchResultAsync(p as SearchResultItem), "Fly to search result");
            SaveSearchResultToMyPlacesCommand = new RelayCommand(p => ExecuteSaveSearchResultToMyPlaces(p as SearchResultItem));
            SaveSearchResultToTemporaryPlacesCommand = new RelayCommand(p => ExecuteSaveSearchResultToTemporaryPlaces(p as SearchResultItem));

            ExpandAllSectionsCommand = new RelayCommand(() =>
            {
                IsSearchExpanded = true;
                IsMyPlacesExpanded = true;
            });

            CollapseAllSectionsCommand = new RelayCommand(() =>
            {
                IsSearchExpanded = false;
                IsMyPlacesExpanded = false;
            });

            FlyToPlaceCommand = AsyncCommand(p => ExecuteFlyToPlaceAsync(p as PlaceItem), "Fly to place");
            AddPlacemarkCommand = new RelayCommand(ExecuteAddPlacemark);
            AddFolderCommand = new RelayCommand(ExecuteAddFolder);
            DeletePlaceCommand = new RelayCommand(ExecuteDeletePlace, () => SelectedPlace != null);
            SaveToMyPlacesCommand = new RelayCommand(ExecuteSaveToMyPlaces, () => SelectedPlace != null && TemporaryPlaces.Contains(SelectedPlace));
            AddSavedSceneCommand = new RelayCommand(ExecuteAddSavedScene);
            RenamePlaceCommand = new RelayCommand(ExecuteRenamePlace, () => SelectedPlace != null);
            SetAsStartupSceneCommand = new RelayCommand(p => ExecuteSetAsStartupScene(p as PlaceItem));
            MoveSavedSceneUpCommand = new RelayCommand(() => ExecuteMoveSavedScene(-1));
            MoveSavedSceneDownCommand = new RelayCommand(() => ExecuteMoveSavedScene(1));
            OpenPropertiesCommand = new RelayCommand(p => ExecuteOpenProperties(p as PlaceItem));

            KeepInTemporaryCommand = new RelayCommand(ExecuteKeepInTemporary);
            SaveTemporaryToProjectCommand = new RelayCommand(ExecuteSaveTemporaryToProject);
            MoveToMyPlacesCommand = new RelayCommand(p => ExecuteMoveToMyPlaces(p as PlaceItem));
            MoveToTemporaryPlacesCommand = new RelayCommand(p => ExecuteMoveToTemporaryPlaces(p as PlaceItem));
            ExportSelectedItemCommand = new RelayCommand(p => ExecuteExportSelectedItem(p as PlaceItem));
            ExportAllMyPlacesCommand = new RelayCommand(ExecuteExportAllMyPlaces);
            ExportAllTemporaryPlacesCommand = new RelayCommand(ExecuteExportTemporaryKml);
            RemoveFromSessionCommand = new RelayCommand(p => ExecuteRemoveFromSession(p as PlaceItem));
            ExportTemporaryKmlCommand = new RelayCommand(ExecuteExportTemporaryKml);
            AddPlacemarkToTemporaryCommand = new RelayCommand(ExecuteAddPlacemarkToTemporary);
            AddFolderToTemporaryCommand = new RelayCommand(ExecuteAddFolderToTemporary);
            DrawLineToTemporaryCommand = AsyncCommand(ExecuteDrawLineToTemporaryAsync, "Draw line");
            DrawPolygonToTemporaryCommand = AsyncCommand(ExecuteDrawPolygonToTemporaryAsync, "Draw polygon");
            ImportKmlToTemporaryCommand = new RelayCommand(ExecuteImportKmlToTemporary);

            // Restore Temporary Places from durable session store if available
            try
            {
                if (TemporaryPlaces.Count == 0)
                {
                    var cachedSession = _temporarySessionService.LoadSession();
                    foreach (var item in cachedSession)
                    {
                        TemporaryPlaces.Add(item);
                    }
                }
                CurrentProject.EnsureSavedScenesFolder();
                CurrentProject.EnsureTemporaryPlacesFolder();
            }
            catch { }

            // Drawing Commands (Sprint 5)
            DrawPlacemarkCommand = AsyncCommand(() => _globeBridge.StartDrawingAsync("point"), "Draw placemark");
            DrawLineCommand = AsyncCommand(() => _globeBridge.StartDrawingAsync("path"), "Draw line");
            DrawPolygonCommand = AsyncCommand(() => _globeBridge.StartDrawingAsync("polygon"), "Draw polygon");
            DrawRectangleCommand = AsyncCommand(() => _globeBridge.StartDrawingAsync("rectangle"), "Draw rectangle");
            DrawCircleCommand = AsyncCommand(async () =>
            {
                ActiveMeasurementInfo = "Draw Circle: Click center point, then click outer edge to complete circle.";
                await _globeBridge.StartDrawingAsync("circle", "#22A8DE");
            }, "Draw circle");
            OverlayImageCommand = new RelayCommand(ExecuteOverlayImage);
            Model3DCommand = new RelayCommand(ExecuteModel3D);

            // Measurement Commands (Sprint 6)
            MeasureDistanceCommand = AsyncCommand(async () =>
            {
                IsMeasuring = true;
                ActiveMeasurementInfo = "Distance measurement active: click on map/features to place vertices";
                await _globeBridge.StartMeasurementAsync("distance");
            }, "Measure distance");

            MeasureAreaCommand = AsyncCommand(async () =>
            {
                IsMeasuring = true;
                ActiveMeasurementInfo = "Area measurement active: click 3+ points to form polygon; double-click/right-click to finish";
                await _globeBridge.StartMeasurementAsync("area");
            }, "Measure area");

            ClearMeasurementCommand = AsyncCommand(async () =>
            {
                IsMeasuring = false;
                ActiveMeasurementInfo = string.Empty;
                await _globeBridge.ClearMeasurementAsync();
            }, "Clear measurement");

            ToggleSnapCommand = new RelayCommand(() =>
            {
                IsSnapEnabled = !IsSnapEnabled;
            });

            ElevationProfileCommand = new RelayCommand(ExecuteElevationProfile);

            // Layer & Terrain Commands (Sprint 7)
            ToggleBordersCommand = new RelayCommand(() => IsBordersVisible = !IsBordersVisible);
            ToggleGraticulesCommand = AsyncCommand(async () =>
            {
                var target = !IsGraticulesVisible;
                if (await _globeBridge.SetGraticulesVisibleAsync(target))
                {
                    IsGraticulesVisible = target;
                    StatusMessage = target ? "Coordinate grid enabled." : "Coordinate grid hidden.";
                }
                else
                {
                    StatusMessage = "The 3D engine did not acknowledge the coordinate grid toggle.";
                }
            }, "Toggle coordinate grid");
            GridOptionsCommand = new RelayCommand(() => ExecuteShowOptions(true));
            ToggleTerrainCommand = AsyncCommand(ExecuteToggleTerrainAsync, "Toggle terrain");
            SelectBasemapCommand = new RelayCommand(p =>
            {
                if (p is string id) ExecuteSelectBasemap(id);
            });
            HistoricalImageryCommand = AsyncCommand(ExecuteShowHistoricalImageryAsync, "Open historical timeline");
            AddImageryLayerCommand = new RelayCommand(ExecuteAddImageryLayer);
            RemoveImageryLayerCommand = new RelayCommand(p =>
            {
                if (p is ImageryLayerViewModel vm) ExecuteRemoveImageryLayer(vm);
            });
            MoveLayerUpCommand = new RelayCommand(p =>
            {
                if (p is ImageryLayerViewModel vm) ExecuteMoveLayerUp(vm);
            });
            MoveLayerDownCommand = new RelayCommand(p =>
            {
                if (p is ImageryLayerViewModel vm) ExecuteMoveLayerDown(vm);
            });
            SetVerticalExaggerationCommand = new RelayCommand(p =>
            {
                if (p is double d) ExecuteSetVerticalExaggeration(d);
                else if (p is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    ExecuteSetVerticalExaggeration(parsed);
            });
            ResetVerticalExaggerationCommand = new RelayCommand(ExecuteResetVerticalExaggeration);

            // Tool Commands (Sprint 4 & Sprint 8)
            ImportVectorCommand = new RelayCommand(p => ExecuteImportVector(p as string));
            ToggleLanguageCommand = new RelayCommand(() =>
            {
                LocalizationService.Instance.ToggleLanguage(Application.Current?.MainWindow);
                OnPropertyChanged(nameof(CurrentLanguageCode));
                OnPropertyChanged(nameof(CurrentLanguageText));
                UpdateWindowTitle();
            });
            CoordinateConverterCommand = new RelayCommand(ExecuteShowCoordinateConverter);
            TakeScreenshotCommand = new RelayCommand(ExecuteTakeScreenshot);
            OptionsCommand = new RelayCommand(ExecuteShowOptions);
            FlyToCoordinatesCommand = AsyncCommand(ExecuteLocateCoordinatesAsync, "Fly to coordinates");

            // Help & About
            AboutCommand = new RelayCommand(ShowAbout);
            DiagnosticsCommand = new RelayCommand(ShowDiagnostics);

            UpdateWindowTitle();
            UpdateTelemetryStrings();

            // Seed initial camera history
            _cameraHistory.Add(CurrentCamera.Clone());
            _historyIndex = 0;
        }

        private async Task ExecuteToggleSunlightAsync()
        {
            var desired = !IsSunlightEnabled;
            if (await _globeBridge.SetSunlightAsync(desired, _timeOfDayHours,
                SunlightDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                IsSunlightEnabled = desired;
            else
                StatusMessage = "Sunlight not changed — globe request failed or timed out.";
        }

        private async Task ExecuteToggleAtmosphereAsync()
        {
            var desired = !IsAtmosphereEnabled;
            if (await _globeBridge.SetAtmosphereAsync(desired))
                IsAtmosphereEnabled = desired;
            else
                StatusMessage = "Atmosphere not changed — globe request failed or timed out.";
        }

        private void ExecuteToggleTheme()
        {
            var targetTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            ThemeService.Instance.SetTheme(targetTheme, Application.Current.MainWindow);
            var themeString = targetTheme == AppTheme.Light ? "light" : "dark";
            _ = _globeBridge.SetThemeAsync(themeString);
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                // BeginInvoke (never Invoke) so the WebView2 message pump is never blocked (RC-4).
                dispatcher.BeginInvoke(action);
            }
        }

        /// <summary>
        /// Creates an async command wired to the standard error reporter and guarded against
        /// re-entrancy, so a double click cannot launch the same operation twice (RC-3).
        /// </summary>
        private AsyncRelayCommand AsyncCommand(Func<Task> execute, string name, Func<bool>? canExecute = null)
            => new(execute, canExecute, ex => ReportCommandError(name, ex));

        private AsyncRelayCommand AsyncCommand(Func<object?, Task> execute, string name)
            => new(execute, null, ex => ReportCommandError(name, ex));

        /// <summary>Runs an operation while publishing a non-blocking busy/status indicator.</summary>
        private async Task RunBusyAsync(string message, Func<Task> action)
        {
            BeginBusy(message);
            try
            {
                await action();
            }
            finally
            {
                EndBusy();
            }
        }

        public void BeginBusy(string message)
        {
            _busyOperations++;
            StatusMessage = message;
            OnPropertyChanged(nameof(IsBusy));
        }

        public void EndBusy()
        {
            if (_busyOperations > 0) _busyOperations--;
            if (_busyOperations == 0) StatusMessage = "Ready";
            OnPropertyChanged(nameof(IsBusy));
        }

        /// <summary>Central async-command failure handler: logs and surfaces the error to the user.</summary>
        public void ReportCommandError(string action, Exception ex)
        {
            LoggerService.Instance.LogError($"Command '{action}' failed", ex);
            StatusMessage = $"Error: {action} failed — {ex.Message}";
        }

        /// <summary>Builds the cached PROJ transform once per CRS pair instead of on every sample (RC-2).</summary>
        private void EnsureCrsTransformWarm(int epsg)
        {
            if (epsg == 4326 || epsg == 4979 || epsg == _warmedCrsEpsg) return;
            _warmedCrsEpsg = epsg;
            CoordinateTransformService.Warmup(4326, epsg);
        }

        private void EnsureTelemetryTimer()
        {            if (_telemetryTimer != null) return;

            _telemetryTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _telemetryTimer.Tick += (s, e) => FlushPendingTelemetry();
            _telemetryTimer.Start();
        }

        private void FlushPendingTelemetry()
        {
            var camera = _pendingCamera;
            var center = _pendingCenter;
            var cursor = _pendingCursor;
            _pendingCamera = null;
            _pendingCenter = null;
            _pendingCursor = null;

            if (camera != null)
            {
                CurrentCamera = camera; // refreshes coordinate / altitude / heading strings
            }
            if (center != null)
            {
                UpdateCenterCoordinates(center);
            }
            if (cursor != null)
            {
                UpdateCursorCoordinates(cursor);
            }
        }

        private void OnGlobeCameraChanged(object? sender, CameraState cam)
        {
            // When there is no WPF dispatcher (headless / unit tests) apply immediately so observers
            // see the update synchronously; otherwise coalesce and let the UI timer apply it.
            if (Application.Current?.Dispatcher == null)
            {
                CurrentCamera = cam;
                return;
            }

            _pendingCamera = cam;
        }

        private void OnGlobeCameraMoveEnded(object? sender, CameraState cam)
        {
            _pendingCamera = null;
            CurrentCamera = cam;
            if (_isNavigatingHistory) return;

            // Check if movement is significant compared to current extent
            var last = _historyIndex >= 0 && _historyIndex < _cameraHistory.Count ? _cameraHistory[_historyIndex] : null;
            if (last != null &&
                Math.Abs(last.Latitude - cam.Latitude) < 0.001 &&
                Math.Abs(last.Longitude - cam.Longitude) < 0.001 &&
                Math.Abs(last.Altitude - cam.Altitude) < 10.0 &&
                Math.Abs(last.Heading - cam.Heading) < 0.5 &&
                Math.Abs(last.Pitch - cam.Pitch) < 0.5)
            {
                return; // Ignore sub-pixel jitter
            }

            // AC-021: Forward extent history stack MUST be invalidated on new navigation branch
            if (_historyIndex >= 0 && _historyIndex < _cameraHistory.Count - 1)
            {
                _cameraHistory.RemoveRange(_historyIndex + 1, _cameraHistory.Count - (_historyIndex + 1));
            }

            _cameraHistory.Add(cam.Clone());
            if (_cameraHistory.Count > MaxHistorySize)
            {
                _cameraHistory.RemoveAt(0);
            }
            _historyIndex = _cameraHistory.Count - 1;

            UpdateExtentCommandStates();
        }

        private void UpdateExtentCommandStates()
        {
            RunOnUi(() =>
            {
                OnPropertyChanged(nameof(CanGoPreviousExtent));
                OnPropertyChanged(nameof(CanGoNextExtent));
                OnPropertyChanged(nameof(ExtentHistoryCount));
                OnPropertyChanged(nameof(ExtentHistoryIndex));

                // Targeted invalidation only — a global InvalidateRequerySuggested on every camera
                // move end caused a full command requery storm (RC-5).
                PreviousExtentCommand?.RaiseCanExecuteChanged();
                NextExtentCommand?.RaiseCanExecuteChanged();
            });
        }

        private void OnGlobeDrawCompleted(object? sender, string json)
        {
            void Handle()
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var type = root.GetProperty("geometryType").GetString() ?? "point";
                    var color = root.TryGetProperty("color", out var cProp) ? cProp.GetString() : "#22A8DE";
                    var continueDrawing = root.TryGetProperty("continueDrawing", out var continueProp) && continueProp.GetBoolean();

                    var coords = new List<CoordinatePoint>();
                    if (root.TryGetProperty("coordinates", out var coordsProp))
                    {
                        foreach (var pt in coordsProp.EnumerateArray())
                        {
                            double lon = pt.GetProperty("longitude").GetDouble();
                            double lat = pt.GetProperty("latitude").GetDouble();
                            double alt = pt.TryGetProperty("altitude", out var aProp) ? aProp.GetDouble() : 0.0;
                            coords.Add(new CoordinatePoint(lon, lat, alt));
                        }
                    }

                    bool isTemp = _drawTargetTemporary || (SelectedPlace != null && FindParentList(TemporaryPlaces, SelectedPlace) != null);
                    if (!continueDrawing)
                    {
                        _drawTargetTemporary = false;
                    }

                    PlaceItem newItem;
                    var targetList = (SelectedPlace != null && SelectedPlace.ItemType == PlaceItemType.Folder && !SelectedPlace.IsProtected)
                        ? SelectedPlace.Children
                        : (isTemp ? TemporaryPlaces : MyPlaces);

                    int count = isTemp ? TemporaryPlaces.Count + 1 : MyPlaces.Count + 1;

                    if (type == "polygon")
                    {
                        bool isRectangle = root.TryGetProperty("shapeKind", out var shapeKind) &&
                            string.Equals(shapeKind.GetString(), "rectangle", StringComparison.OrdinalIgnoreCase);
                        newItem = new PlaceItem($"{(isRectangle ? "Rectangle" : "Polygon")} {count}", PlaceItemType.Polygon)
                        {
                            Coordinates = coords,
                            ColorHex = color ?? "#22A8DE"
                        };
                        if (isRectangle) newItem.ExtendedData["shapeKind"] = "rectangle";
                        if (coords.Count > 0)
                        {
                            newItem.Longitude = coords[0].Longitude;
                            newItem.Latitude = coords[0].Latitude;
                            newItem.Altitude = coords[0].Altitude;
                        }
                    }
                    else if (type == "path")
                    {
                        newItem = new PlaceItem($"Path {count}", PlaceItemType.Path)
                        {
                            Coordinates = coords,
                            ColorHex = color ?? "#22A8DE"
                        };
                        if (coords.Count > 0)
                        {
                            newItem.Longitude = coords[0].Longitude;
                            newItem.Latitude = coords[0].Latitude;
                            newItem.Altitude = coords[0].Altitude;
                        }
                    }
                    else if (type == "circle")
                    {
                        double radius = 100.0;
                        if (root.TryGetProperty("radiusMeters", out var rProp)) radius = rProp.GetDouble();
                        double cLon = CurrentCamera.Longitude, cLat = CurrentCamera.Latitude, cAlt = 0.0;
                        if (root.TryGetProperty("center", out var centerProp))
                        {
                            cLon = centerProp.GetProperty("longitude").GetDouble();
                            cLat = centerProp.GetProperty("latitude").GetDouble();
                            if (centerProp.TryGetProperty("altitude", out var altP)) cAlt = altP.GetDouble();
                        }
                        newItem = new PlaceItem($"Circle {count}", PlaceItemType.Circle, cLat, cLon, cAlt)
                        {
                            Coordinates = coords,
                            ColorHex = color ?? "#22A8DE"
                        };
                        newItem.ExtendedData["RadiusMeters"] = radius.ToString(CultureInfo.InvariantCulture);
                        newItem.ExtendedData["AreaSquareMeters"] = (Math.PI * radius * radius).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        var first = coords.FirstOrDefault() ?? new CoordinatePoint(CurrentCamera.Longitude, CurrentCamera.Latitude, CurrentCamera.Altitude);
                        var pointNumber = root.TryGetProperty("pointNumber", out var pointNumberProp)
                            ? pointNumberProp.GetInt32()
                            : count;
                        newItem = new PlaceItem($"Point {pointNumber}", PlaceItemType.Placemark, first.Latitude, first.Longitude, first.Altitude)
                        {
                            ColorHex = color ?? "#22A8DE"
                        };
                        newItem.ExtendedData["PointNumber"] = pointNumber.ToString(CultureInfo.InvariantCulture);
                        newItem.ExtendedData["AltitudeMode"] = "ClampToGround";
                    }

                    _undoRedo.ExecuteAction(new AddPlaceAction(targetList, newItem));
                    SelectedPlace = newItem;
                    _projectService.IsModified = true;
                    if (isTemp)
                    {
                        _temporarySessionService.SaveSession(TemporaryPlaces);
                    }
                    UpdatePlacesOnGlobe();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] DrawCompleted error: {ex.Message}");
                }
            }

            // Never block the raising thread on the UI thread (RC-4).
            RunOnUi(Handle);
        }

        private bool _isSnapEnabled = true;
        public bool IsSnapEnabled
        {
            get => _isSnapEnabled;
            set
            {
                if (SetProperty(ref _isSnapEnabled, value))
                {
                    SyncSnapConfig();
                }
            }
        }

        private double _snapTolerancePixels = 12.0;
        public double SnapTolerancePixels
        {
            get => _snapTolerancePixels;
            set
            {
                if (SetProperty(ref _snapTolerancePixels, value))
                {
                    SyncSnapConfig();
                }
            }
        }

        private double _snapGroundToleranceMeters = 5.0;
        public double SnapGroundToleranceMeters
        {
            get => _snapGroundToleranceMeters;
            set
            {
                if (SetProperty(ref _snapGroundToleranceMeters, value))
                {
                    _snapToleranceTag = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(SnapToleranceTag));
                    SyncSnapConfig();
                }
            }
        }

        private string _snapToleranceTag = "5";
        public string SnapToleranceTag
        {
            get => _snapToleranceTag;
            set
            {
                if (SetProperty(ref _snapToleranceTag, value) && double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var d))
                {
                    SnapGroundToleranceMeters = d;
                }
            }
        }

        private bool _snapToVertices = true;
        public bool SnapToVertices
        {
            get => _snapToVertices;
            set
            {
                if (SetProperty(ref _snapToVertices, value))
                {
                    SyncSnapConfig();
                }
            }
        }

        private bool _snapToMidpoints = true;
        public bool SnapToMidpoints
        {
            get => _snapToMidpoints;
            set
            {
                if (SetProperty(ref _snapToMidpoints, value))
                {
                    SyncSnapConfig();
                }
            }
        }

        private bool _snapToEdges = true;
        public bool SnapToEdges
        {
            get => _snapToEdges;
            set
            {
                if (SetProperty(ref _snapToEdges, value))
                {
                    SyncSnapConfig();
                }
            }
        }

        private string _activeMeasurementInfo = string.Empty;
        public string ActiveMeasurementInfo
        {
            get => _activeMeasurementInfo;
            set => SetProperty(ref _activeMeasurementInfo, value);
        }

        private bool _isMeasuring = false;
        public bool IsMeasuring
        {
            get => _isMeasuring;
            set => SetProperty(ref _isMeasuring, value);
        }

        private void SyncSnapConfig()
        {
            _ = _globeBridge.SetSnappingConfigAsync(
                _isSnapEnabled,
                "GroundDistance",
                _snapTolerancePixels,
                _snapGroundToleranceMeters,
                _snapToVertices,
                _snapToMidpoints,
                _snapToEdges,
                SnappingSettings.Instance.SnapToIntersections);
        }

        private void OnGlobeMeasurementUpdated(object? sender, string json)
        {
            void Handle()
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var mode = root.TryGetProperty("mode", out var mProp) ? mProp.GetString() : "distance";
                    if (mode == "distance" && root.TryGetProperty("horizontalDistanceMeters", out var hProp))
                    {
                        double h = hProp.GetDouble();
                        double s = root.TryGetProperty("slopeDistanceMeters", out var sProp) ? sProp.GetDouble() : h;
                        double dz = root.TryGetProperty("elevationDeltaMeters", out var dzProp) ? dzProp.GetDouble() : 0.0;
                        int pts = root.TryGetProperty("pointCount", out var pProp) ? pProp.GetInt32() : 0;
                        ActiveMeasurementInfo = $"Distance: {GeodesicMeasurementService.FormatDistance(h)} | 3D: {GeodesicMeasurementService.FormatDistance(s)} | ΔZ: {dz:F1}m ({pts} pts)";
                    }
                    else if (mode == "area" && root.TryGetProperty("areaSquareMeters", out var aProp))
                    {
                        double a = aProp.GetDouble();
                        double perim = root.TryGetProperty("perimeterMeters", out var prProp) ? prProp.GetDouble() : 0.0;
                        int pts = root.TryGetProperty("pointCount", out var pProp) ? pProp.GetInt32() : 0;
                        ActiveMeasurementInfo = $"Area: {GeodesicMeasurementService.FormatArea(a)} | Perimeter: {GeodesicMeasurementService.FormatDistance(perim)} ({pts} pts)";
                    }
                    else if (mode == "circle" && root.TryGetProperty("radiusMeters", out var radProp))
                    {
                        double r = radProp.GetDouble();
                        double area = root.TryGetProperty("areaSquareMeters", out var arProp) ? arProp.GetDouble() : Math.PI * r * r;
                        ActiveMeasurementInfo = $"Radius: {GeodesicMeasurementService.FormatDistance(r)} | Area: {GeodesicMeasurementService.FormatArea(area)}";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] MeasurementUpdated error: {ex.Message}");
                }
            }

            // Never block the raising thread on the UI thread (RC-4).
            RunOnUi(Handle);
        }

        private void OnGlobeMeasurementCompleted(object? sender, string json)
        {
            void Handle()
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var mode = root.TryGetProperty("mode", out var mProp) ? mProp.GetString() : "distance";
                    if (mode == "distance" && root.TryGetProperty("horizontalDistanceMeters", out var hProp))
                    {
                        double h = hProp.GetDouble();
                        double s = root.TryGetProperty("slopeDistanceMeters", out var sProp) ? sProp.GetDouble() : h;
                        double dz = root.TryGetProperty("elevationDeltaMeters", out var dzProp) ? dzProp.GetDouble() : 0.0;
                        ActiveMeasurementInfo = $"Result: {GeodesicMeasurementService.FormatDistance(h)} (3D: {GeodesicMeasurementService.FormatDistance(s)}, ΔZ: {dz:F1}m)";
                    }
                    else if (mode == "area" && root.TryGetProperty("areaSquareMeters", out var aProp))
                    {
                        double a = aProp.GetDouble();
                        double perim = root.TryGetProperty("perimeterMeters", out var prProp) ? prProp.GetDouble() : 0.0;
                        ActiveMeasurementInfo = $"Result: {GeodesicMeasurementService.FormatArea(a)} (Perim: {GeodesicMeasurementService.FormatDistance(perim)})";
                    }
                    else if (mode == "circle" && root.TryGetProperty("radiusMeters", out var radProp))
                    {
                        double r = radProp.GetDouble();
                        double area = root.TryGetProperty("areaSquareMeters", out var arProp) ? arProp.GetDouble() : Math.PI * r * r;
                        ActiveMeasurementInfo = $"Result Radius: {GeodesicMeasurementService.FormatDistance(r)} (Area: {GeodesicMeasurementService.FormatArea(area)})";
                    }

                    if (root.TryGetProperty("coordinates", out var coordsProp))
                    {
                        _lastMeasurementPoints.Clear();
                        foreach (var pt in coordsProp.EnumerateArray())
                        {
                            double lon = pt.GetProperty("longitude").GetDouble();
                            double lat = pt.GetProperty("latitude").GetDouble();
                            double alt = pt.TryGetProperty("altitude", out var altProp) ? altProp.GetDouble() : 0.0;
                            _lastMeasurementPoints.Add(new CoordinatePoint(lon, lat, alt));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] MeasurementCompleted error: {ex.Message}");
                }
            }

            // Never block the raising thread on the UI thread (RC-4).
            RunOnUi(Handle);
        }

        private async void ExecutePreviousExtent()
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                _isNavigatingHistory = true;
                UpdateExtentCommandStates();
                await _globeBridge.FlyToAsync(_cameraHistory[_historyIndex], 0.8);
                _isNavigatingHistory = false;
            }
        }

        private async void ExecuteNextExtent()
        {
            if (_historyIndex < _cameraHistory.Count - 1)
            {
                _historyIndex++;
                _isNavigatingHistory = true;
                UpdateExtentCommandStates();
                await _globeBridge.FlyToAsync(_cameraHistory[_historyIndex], 0.8);
                _isNavigatingHistory = false;
            }
        }

        private async Task ExecuteFitSelectionAsync()
        {
            if (SelectedPlace != null)
            {
                await _globeBridge.FitSelectionAsync(new
                {
                    entityId = SelectedPlace.Id,
                    west = SelectedPlace.Longitude - 0.25,
                    south = SelectedPlace.Latitude - 0.25,
                    east = SelectedPlace.Longitude + 0.25,
                    north = SelectedPlace.Latitude + 0.25
                });
            }
            else
            {
                await _globeBridge.FullExtentAsync();
            }
        }

        private void UpdateTelemetryStrings()
        {
            StatusCoordinates = CoordinateFormatter.Format(CurrentCamera.Longitude, CurrentCamera.Latitude, SelectedCoordinateFormat);

            if (SelectedCoordinateFormat == CoordinateDisplayFormat.Utm)
            {
                var utm = CoordinateTransformService.Wgs84ToUtm(CurrentCamera.Longitude, CurrentCamera.Latitude);
                StatusCrs = $"WGS 84 / UTM {utm.Zone}{(utm.IsNorthernHemisphere ? "N" : "S")}";
            }
            else if (SelectedCoordinateFormat == CoordinateDisplayFormat.Mgrs)
            {
                StatusCrs = "WGS 84 / MGRS Grid";
            }
            else
            {
                StatusCrs = "EPSG:4326 (WGS 84)";
            }

            if (CurrentCamera.Altitude >= 1000)
            {
                StatusAltitude = $"{(CurrentCamera.Altitude / 1000.0):N0} km";
            }
            else
            {
                StatusAltitude = $"{CurrentCamera.Altitude:N0} m";
            }

            StatusHeading = $"{CurrentCamera.Heading:F1}°";
            UpdateCenterCoordinates(_pendingCenter ?? _lastKnownCenter ?? _globeBridge.CurrentCenter);
            OnPropertyChanged(nameof(StatusViewMode));
        }

        private void UpdateCenterCoordinates(MapCenterInfo? center)
        {
            if (center == null || !center.IsValid)
            {
                StatusCenterCoordinates = "Center: Outside globe";
                return;
            }

            int epsg = SettingsService.Instance.Settings.MapCrsEpsg;
            if (epsg != 4326 && epsg != 4979 && SelectedCoordinateFormat != CoordinateDisplayFormat.Utm && SelectedCoordinateFormat != CoordinateDisplayFormat.Mgrs)
            {
                EnsureCrsTransformWarm(epsg);
                try
                {
                    var (x, y) = CoordinateTransformService.TransformFast(center.Longitude, center.Latitude, 4326, epsg);
                    StatusCenterCoordinates = string.Format(CultureInfo.InvariantCulture, "Center X/E: {0:N1} m | Y/N: {1:N1} m", x, y);
                    return;
                }
                catch
                {
                    // Fall back to geographic
                }
            }

            StatusCenterCoordinates = $"Center: {CoordinateFormatter.Format(center.Longitude, center.Latitude, SelectedCoordinateFormat)}";
        }

        private void UpdateCursorCoordinates(CursorPositionInfo? pos)
        {
            if (pos == null || !pos.IsValid)
            {
                StatusCursorCoordinates = "Cursor: Outside globe";
                StatusCursorElevation = "Elev: —";
                return;
            }

            int epsg = SettingsService.Instance.Settings.MapCrsEpsg;
            if (epsg != 4326 && epsg != 4979 && SelectedCoordinateFormat != CoordinateDisplayFormat.Utm && SelectedCoordinateFormat != CoordinateDisplayFormat.Mgrs)
            {
                EnsureCrsTransformWarm(epsg);
                try
                {
                    var (x, y) = CoordinateTransformService.TransformFast(pos.Longitude, pos.Latitude, 4326, epsg);
                    StatusCursorCoordinates = string.Format(CultureInfo.InvariantCulture, "Cursor X/E: {0:N1} m | Y/N: {1:N1} m", x, y);
                }
                catch
                {
                    StatusCursorCoordinates = $"Cursor: {CoordinateFormatter.Format(pos.Longitude, pos.Latitude, SelectedCoordinateFormat)}";
                }
            }
            else
            {
                StatusCursorCoordinates = $"Cursor: {CoordinateFormatter.Format(pos.Longitude, pos.Latitude, SelectedCoordinateFormat)}";
            }

            StatusCursorElevation = pos.Altitude >= 1000
                ? $"Elev: {(pos.Altitude / 1000.0):N1} km"
                : $"Elev: {pos.Altitude:N0} m";
        }

        public void ExecuteToggleCoordinateFormat()
        {
            SelectedCoordinateFormat = SelectedCoordinateFormat switch
            {
                CoordinateDisplayFormat.DecimalDegrees => CoordinateDisplayFormat.DegreesMinutesSeconds,
                CoordinateDisplayFormat.DegreesMinutesSeconds => CoordinateDisplayFormat.Utm,
                CoordinateDisplayFormat.Utm => CoordinateDisplayFormat.Mgrs,
                _ => CoordinateDisplayFormat.DecimalDegrees
            };
        }

        public void ExecuteSelectCrs()
        {
            var currentEpsg = SettingsService.Instance.Settings.MapCrsEpsg;
            var dlg = new Views.CrsSelectionDialog(currentEpsg)
            {
                Owner = Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() == true && dlg.SelectedCrs != null)
            {
                var crs = dlg.SelectedCrs;
                StatusCrs = $"{crs.Name} ({crs.AuthorityCode})";
                SettingsService.Instance.Settings.MapCrsEpsg = crs.EpsgCode;
                SettingsService.Instance.Save();

                // Pre-build the projection transform so the first cursor sample is not slow (RC-2).
                _warmedCrsEpsg = crs.EpsgCode;
                if (crs.EpsgCode != 4326 && crs.EpsgCode != 4979)
                {
                    CoordinateTransformService.Warmup(4326, crs.EpsgCode);
                }

                _ = _globeBridge.SetMapCrsAsync(crs.EpsgCode, crs.Name, crs.IsProjected, crs.Unit);
                LoggerService.Instance.LogInfo($"Active Map CRS set to {crs.Name} ({crs.AuthorityCode})");
            }
        }

        private void UpdateWindowTitle()
        {
            var dirtyPrefix = _projectService.IsModified ? "* " : "";
            var fileName = !string.IsNullOrEmpty(_projectService.CurrentFilePath)
                ? Path.GetFileName(_projectService.CurrentFilePath)
                : _projectService.CurrentProject.Name;
            var appTitle = LocalizationService.Instance.GetString("App.Title", "Z EARTH Desktop GIS - Developed by Ahmad Ibrahim");
            WindowTitle = $"{dirtyPrefix}{appTitle} - [{fileName}]";
        }

        private void ExecuteNewProject()
        {
            _projectService.CreateNew();
            _undoRedo.Clear();
            _pendingSessionItems.Clear();
            OnPropertyChanged(nameof(CurrentProject));
            OnPropertyChanged(nameof(MyPlaces));
            OnPropertyChanged(nameof(TemporaryPlaces));
            OnPropertyChanged(nameof(SavedScenes));
            UpdateWindowTitle();
            UpdatePlacesOnGlobe();
            _ = RestoreStartupSceneAsync();
        }

        private async Task ExecuteOpenProjectAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Z EARTH Project (*.zearth;*.zproj)|*.zearth;*.zproj|All Files (*.*)|*.*",
                Title = "Open Z EARTH Project"
            };

            if (dlg.ShowDialog() == true)
            {
                var doc = await _projectService.LoadAsync(dlg.FileName);
                if (doc != null)
                {
                    _pendingSessionItems.Clear();
                    OnPropertyChanged(nameof(CurrentProject));
                    OnPropertyChanged(nameof(MyPlaces));
                    OnPropertyChanged(nameof(TemporaryPlaces));
                    OnPropertyChanged(nameof(SavedScenes));
                    UpdateWindowTitle();
                    await RestoreStartupSceneAsync();
                }
            }
        }

        private async Task ExecuteSaveProjectAsync()
        {
            if (string.IsNullOrEmpty(_projectService.CurrentFilePath))
            {
                await ExecuteSaveAsProjectAsync();
            }
            else
            {
                _projectService.CurrentProject.Camera = CurrentCamera.Clone();
                _projectService.CurrentProject.SceneMode = CurrentSceneMode;
                if (await _projectService.SaveAsync())
                {
                    // Project save retains Temporary Places as their own project folder.
                    // Promotion to permanent My Places remains an explicit user action.
                    _pendingSessionItems.Clear();
                }
                UpdateWindowTitle();
            }
        }

        private async Task ExecuteSaveAsProjectAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Z EARTH Project (*.zearth)|*.zearth|Z EARTH JSON (*.zproj)|*.zproj",
                DefaultExt = ".zearth",
                Title = "Save Z EARTH Project As"
            };

            if (dlg.ShowDialog() == true)
            {
                _projectService.CurrentProject.Camera = CurrentCamera.Clone();
                _projectService.CurrentProject.SceneMode = CurrentSceneMode;
                if (await _projectService.SaveAsync(dlg.FileName))
                {
                    _pendingSessionItems.Clear();
                }
                UpdateWindowTitle();
            }
        }

        private void ExecuteExportKml()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Keyhole Markup Language (*.kml)|*.kml",
                DefaultExt = ".kml",
                Title = "Export Places as KML"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var exportRoot = SelectedPlace ?? new PlaceItem(CurrentProject.Name, PlaceItemType.Folder);
                    if (SelectedPlace == null)
                    {
                        foreach (var item in MyPlaces) exportRoot.AddChild(item.Clone());
                    }

                    KmlSerializer.SerializeToFile(exportRoot, dlg.FileName);
                    MessageBox.Show($"KML exported successfully to:\n{dlg.FileName}", "Export KML", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteExportKmz()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Zipped Keyhole Markup Language (*.kmz)|*.kmz",
                DefaultExt = ".kmz",
                Title = "Export Places as KMZ Bundle"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var exportRoot = SelectedPlace ?? new PlaceItem(CurrentProject.Name, PlaceItemType.Folder);
                    if (SelectedPlace == null)
                    {
                        foreach (var item in MyPlaces) exportRoot.AddChild(item.Clone());
                    }

                    KmzService.CreatePackage(dlg.FileName, exportRoot);
                    MessageBox.Show($"KMZ archive exported successfully to:\n{dlg.FileName}", "Export KMZ", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"KMZ export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async Task ExecuteSearchByNameAsync()
        {
            var query = !string.IsNullOrWhiteSpace(SearchNameQuery) ? SearchNameQuery : SearchQuery;
            if (string.IsNullOrWhiteSpace(query)) return;

            IsSearching = true;
            try
            {
                SearchResults.Clear();
                var allPlaces = new List<PlaceItem>();
                allPlaces.AddRange(MyPlaces);

                var matches = await _searchService.SearchByNameAsync(query, allPlaces, Array.Empty<ReferenceLayerItem>());
                foreach (var m in matches)
                {
                    SearchResults.Add(m);
                }

                if (SearchResults.Count > 0)
                {
                    SelectedSearchResult = SearchResults[0];
                    CoordinateValidationMessage = _searchService.LastOnlineError == null
                        ? $"Found {SearchResults.Count} result(s) for '{query}'. Select a result to fly to it."
                        : $"Found {SearchResults.Count} local result(s). {_searchService.LastOnlineError}";
                    IsCoordinateValidationSuccess = true;
                }
                else
                {
                    CoordinateValidationMessage = _searchService.LastOnlineError == null
                        ? $"No locations found matching '{query}'."
                        : _searchService.LastOnlineError;
                    IsCoordinateValidationSuccess = false;
                }
            }
            catch (Exception ex)
            {
                CoordinateValidationMessage = $"Search error: {ex.Message}";
                IsCoordinateValidationSuccess = false;
            }
            finally
            {
                IsSearching = false;
            }
        }

        public async Task ExecuteLocateCoordinatesAsync()
        {
            try
            {
                CoordinateValidationMessage = string.Empty;
                var result = _searchService.SearchByCoordinates(SearchCoordinateX, SearchCoordinateY, SelectedSearchCrsEpsg);
                IsCoordinateValidationSuccess = true;
                CoordinateValidationMessage = $"Target: {result.CoordinateDisplay} ({result.Source})";

                await _globeBridge.SetSearchPinAsync(result.Longitude, result.Latitude, result.Title, true);
                await _globeBridge.FlyToAsync(new CameraState(result.Latitude, result.Longitude, result.Altitude, 0, -90), 1.5);

                if (!SearchResults.Any(r => Math.Abs(r.Longitude - result.Longitude) < 1e-5 && Math.Abs(r.Latitude - result.Latitude) < 1e-5))
                {
                    SearchResults.Insert(0, result);
                }
                SelectedSearchResult = result;
            }
            catch (CoordinateValidationException ex)
            {
                IsCoordinateValidationSuccess = false;
                CoordinateValidationMessage = ex.Message;
            }
            catch (Exception ex)
            {
                IsCoordinateValidationSuccess = false;
                CoordinateValidationMessage = $"Coordinate Error: {ex.Message}";
            }
        }

        public async Task ExecuteFlyToSearchResultAsync(SearchResultItem? item)
        {
            if (item == null) return;
            await _globeBridge.SetSearchPinAsync(item.Longitude, item.Latitude, item.Title, true);
            await _globeBridge.FlyToAsync(new CameraState(item.Latitude, item.Longitude, item.Altitude, 0, -90), 1.5);
        }

        private void ExecuteSaveSearchResultToMyPlaces(SearchResultItem? item)
        {
            var target = item ?? SelectedSearchResult;
            if (target == null) return;

            // Search altitude is the preferred camera height, not the feature height.
            // Store the result on the terrain so it can never appear to float in space.
            var p = new PlaceItem(target.Title, PlaceItemType.Placemark, target.Latitude, target.Longitude, 0);
            p.Description = target.Subtitle;
            p.ExtendedData["AltitudeMode"] = "ClampToGround";
            MyPlaces.Add(p);
            _projectService.IsModified = true;
            UpdatePlacesOnGlobe();
            OnPropertyChanged(nameof(MyPlaces));
            MessageBox.Show($"Saved '{target.Title}' to My Places.", "Saved to My Places", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteSaveSearchResultToTemporaryPlaces(SearchResultItem? item)
        {
            var target = item ?? SelectedSearchResult;
            if (target == null) return;

            var p = new PlaceItem(target.Title, PlaceItemType.Placemark, target.Latitude, target.Longitude, 0);
            p.Description = target.Subtitle;
            p.ExtendedData["AltitudeMode"] = "ClampToGround";
            TemporaryPlaces.Add(p);
            _temporarySessionService.SaveSession(TemporaryPlaces);
            UpdatePlacesOnGlobe();
            OnPropertyChanged(nameof(TemporaryPlaces));
            MessageBox.Show($"Saved '{target.Title}' to Temporary Places.", "Saved to Temporary Places", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public async Task ExecuteFlyToPlaceAsync(PlaceItem? item)
        {
            var target = item ?? SelectedPlace;
            if (target == null) return;

            if (target.ItemType == PlaceItemType.SavedScene && target.Camera != null)
            {
                if (target.ExtendedData.TryGetValue("sceneMode", out var smStr) && Enum.TryParse<SceneMode>(smStr, out var sm))
                {
                    _ = _globeBridge.SetSceneModeAsync(sm);
                }
                if (target.ExtendedData.TryGetValue("basemapId", out var bmapId) && !string.IsNullOrEmpty(bmapId))
                {
                    _ = _globeBridge.SetBasemapAsync(bmapId);
                }
                await _globeBridge.FlyToAsync(target.Camera, 1.5);
                return;
            }

            if (target.Latitude.HasValue && target.Longitude.HasValue)
            {
                var targetAlt = target.Altitude.HasValue && target.Altitude.Value > 1000 ? target.Altitude.Value : (target.Camera?.Altitude ?? 50000.0);
                await _globeBridge.FlyToAsync(new CameraState(target.Latitude.Value, target.Longitude.Value, targetAlt, target.Camera?.Heading ?? 0, target.Camera?.Pitch ?? -90), 1.5);
            }
            else if (target.Coordinates.Count > 0)
            {
                double minLon = target.Coordinates.Min(c => c.Longitude);
                double maxLon = target.Coordinates.Max(c => c.Longitude);
                double minLat = target.Coordinates.Min(c => c.Latitude);
                double maxLat = target.Coordinates.Max(c => c.Latitude);
                await _globeBridge.FitSelectionAsync(new
                {
                    west = minLon - 0.05,
                    south = minLat - 0.05,
                    east = maxLon + 0.05,
                    north = maxLat + 0.05,
                    durationSeconds = 1.5
                });
            }
            else if (target.Children.Count > 0)
            {
                var coords = new List<CoordinatePoint>();
                void CollectCoords(PlaceItem pi)
                {
                    if (pi.Longitude.HasValue && pi.Latitude.HasValue)
                        coords.Add(new CoordinatePoint(pi.Longitude.Value, pi.Latitude.Value, pi.Altitude.GetValueOrDefault(0)));
                    coords.AddRange(pi.Coordinates);
                    foreach (var ch in pi.Children) CollectCoords(ch);
                }
                CollectCoords(target);
                if (coords.Count > 0)
                {
                    double minLon = coords.Min(c => c.Longitude);
                    double maxLon = coords.Max(c => c.Longitude);
                    double minLat = coords.Min(c => c.Latitude);
                    double maxLat = coords.Max(c => c.Latitude);
                    await _globeBridge.FitSelectionAsync(new
                    {
                        west = minLon - 0.05,
                        south = minLat - 0.05,
                        east = maxLon + 0.05,
                        north = maxLat + 0.05,
                        durationSeconds = 1.5
                    });
                }
            }
        }

        private void ExecuteAddPlacemark()
        {
            var targetList = (SelectedPlace != null && SelectedPlace.ItemType == PlaceItemType.Folder && !SelectedPlace.IsProtected)
                ? SelectedPlace.Children
                : MyPlaces;

            var center = _globeBridge.CurrentCenter;
            var latitude = center.IsValid ? center.Latitude : CurrentCamera.Latitude;
            var longitude = center.IsValid ? center.Longitude : CurrentCamera.Longitude;
            var altitude = center.IsValid ? Math.Max(0.0, center.Altitude) : 0.0;
            var p = new PlaceItem($"Placemark {MyPlaces.Count + 1}", PlaceItemType.Placemark, latitude, longitude, altitude);
            p.ExtendedData["AltitudeMode"] = "ClampToGround";
            _undoRedo.ExecuteAction(new AddPlaceAction(targetList, p));
            SelectedPlace = p;
            _projectService.IsModified = true;
            UpdatePlacesOnGlobe();
        }

        private void ExecuteAddFolder()
        {
            var targetList = (SelectedPlace != null && SelectedPlace.ItemType == PlaceItemType.Folder && !SelectedPlace.IsProtected)
                ? SelectedPlace.Children
                : MyPlaces;

            var f = new PlaceItem($"Folder {MyPlaces.Count + 1}", PlaceItemType.Folder);
            _undoRedo.ExecuteAction(new AddPlaceAction(targetList, f));
            SelectedPlace = f;
            _projectService.IsModified = true;
        }

        private static IList<PlaceItem>? FindParentList(IList<PlaceItem> list, PlaceItem target)
        {
            if (list.Contains(target)) return list;
            foreach (var item in list)
            {
                var found = FindParentList(item.Children, target);
                if (found != null) return found;
            }
            return null;
        }

        private void ExecuteDeletePlace()
        {
            if (SelectedPlace == null) return;
            if (SelectedPlace.IsProtected)
            {
                MessageBox.Show("Built-in protected system folders cannot be deleted.", "Protected Folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var scenesFolder = CurrentProject.EnsureSavedScenesFolder();
            var parent = FindParentList(MyPlaces, SelectedPlace)
                      ?? FindParentList(TemporaryPlaces, SelectedPlace)
                      ?? (scenesFolder.Children.Contains(SelectedPlace) ? scenesFolder.Children : null)
                      ?? (SavedScenes.Contains(SelectedPlace) ? SavedScenes : null);

            if (parent != null)
            {
                _undoRedo.ExecuteAction(new DeletePlaceAction(parent, SelectedPlace));
                if (TemporaryPlaces.Contains(SelectedPlace) || parent == TemporaryPlaces)
                {
                    _temporarySessionService.SaveSession(TemporaryPlaces);
                }
                SelectedPlace = null;
                _projectService.IsModified = true;
                CurrentProject.SyncSavedScenes();
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(TemporaryPlaces));
                OnPropertyChanged(nameof(SavedScenes));
            }
        }

        private void ExecuteSaveToMyPlaces()
        {
            if (SelectedPlace == null) return;
            if (TemporaryPlaces.Contains(SelectedPlace))
            {
                _undoRedo.ExecuteAction(new MoveToMyPlacesAction(TemporaryPlaces, MyPlaces, SelectedPlace));
                _temporarySessionService.SaveSession(TemporaryPlaces);
                _projectService.IsModified = true;
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(TemporaryPlaces));
            }
        }

        private void ExecuteAddSavedScene()
        {
            var scenesFolder = CurrentProject.EnsureSavedScenesFolder();
            var center = _globeBridge.CurrentCenter;

            // A saved scene represents a location on Earth. Refuse a sky/space view
            // instead of creating the old floating "Placemark 4" style artifact.
            if (!center.IsValid)
            {
                MessageBox.Show(
                    "Point the center of the view at the Earth, then save the scene again.",
                    "Cannot Save a Space View",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            double? groundLat = center.Latitude;
            double? groundLon = center.Longitude;
            double? groundAlt = Math.Max(0.0, center.Altitude);

            var sceneName = $"Scene {scenesFolder.Children.Count + 1}";
            sceneName += $" ({Math.Abs(center.Latitude):F2}°, {Math.Abs(center.Longitude):F2}°)";

            var scene = new PlaceItem(sceneName, CurrentCamera, groundLat, groundLon, groundAlt);
            scene.ExtendedData["sceneMode"] = CurrentSceneMode.ToString();
            scene.ExtendedData["basemapId"] = SelectedBasemapId;
            scenesFolder.AddChild(scene);
            CurrentProject.SyncSavedScenes();
            _projectService.IsModified = true;
            OnPropertyChanged(nameof(MyPlaces));
            OnPropertyChanged(nameof(SavedScenes));
        }

        private void ExecuteMoveSavedScene(int direction)
        {
            if (!CurrentProject.MoveSavedScene(SelectedPlace, direction))
            {
                StatusMessage = "Select a saved scene that can move within Saved Scenes.";
                return;
            }

            _projectService.IsModified = true;
            OnPropertyChanged(nameof(MyPlaces));
            OnPropertyChanged(nameof(SavedScenes));
            StatusMessage = "Saved scene order updated. The first scene is the startup view.";
        }

        private void ExecuteSetAsStartupScene(PlaceItem? scene)
        {
            var targetScene = scene ?? SelectedPlace;
            if (targetScene == null) return;

            var scenesFolder = CurrentProject.EnsureSavedScenesFolder();
            if (scenesFolder.Children.Contains(targetScene))
            {
                int currentIndex = scenesFolder.Children.IndexOf(targetScene);
                if (currentIndex > 0)
                {
                    scenesFolder.Children.Move(currentIndex, 0);
                }
                CurrentProject.SyncSavedScenes();
                _projectService.IsModified = true;
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(SavedScenes));
                MessageBox.Show($"'{targetScene.Name}' is now set as the project startup scene (ordered first).", "Startup Scene Configured", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteOpenProperties(PlaceItem? item)
        {
            var target = item ?? SelectedPlace;
            if (target == null) return;

            var dlg = new PropertiesDialog(target)
            {
                Owner = Application.Current?.MainWindow
            };

            if (dlg.ShowDialog() == true)
            {
                _projectService.IsModified = true;
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(TemporaryPlaces));
                OnPropertyChanged(nameof(SavedScenes));
            }
        }

        public async Task RestoreStartupSceneAsync()
        {
            var scenesFolder = CurrentProject.EnsureSavedScenesFolder();
            if (scenesFolder.Children.Count > 0)
            {
                var startScene = scenesFolder.Children[0];
                if (startScene.Camera != null)
                {
                    await _globeBridge.FlyToAsync(startScene.Camera, 1.2);
                }
                if (startScene.ExtendedData.TryGetValue("sceneMode", out var modeStr) &&
                    Enum.TryParse<SceneMode>(modeStr, out var sm))
                {
                    await _globeBridge.SetSceneModeAsync(sm);
                }
                if (startScene.ExtendedData.TryGetValue("basemapId", out var bId) && !string.IsNullOrEmpty(bId))
                {
                    SelectedBasemapId = bId;
                }
            }
            else
            {
                await _globeBridge.HomePresetAsync(SelectedHomePreset);
            }
            UpdatePlacesOnGlobe();
        }

        private bool _drawTargetTemporary = false;

        private void ExecuteAddPlacemarkToTemporary()
        {
            var targetList = (SelectedPlace != null && SelectedPlace.ItemType == PlaceItemType.Folder && !SelectedPlace.IsProtected && FindParentList(TemporaryPlaces, SelectedPlace) != null)
                ? SelectedPlace.Children
                : TemporaryPlaces;

            var center = _globeBridge.CurrentCenter;
            var latitude = center.IsValid ? center.Latitude : CurrentCamera.Latitude;
            var longitude = center.IsValid ? center.Longitude : CurrentCamera.Longitude;
            var altitude = center.IsValid ? Math.Max(0.0, center.Altitude) : 0.0;
            var newItem = new PlaceItem($"Placemark {TemporaryPlaces.Count + 1}", PlaceItemType.Placemark, latitude, longitude, altitude)
            {
                ColorHex = "#F59E0B"
            };
            newItem.ExtendedData["AltitudeMode"] = "ClampToGround";

            targetList.Add(newItem);
            SelectedPlace = newItem;
            _projectService.IsModified = true;
            _temporarySessionService.SaveSession(TemporaryPlaces);
            UpdatePlacesOnGlobe();

            var dlg = new PropertiesDialog(newItem)
            {
                Owner = Application.Current?.MainWindow
            };
            dlg.ShowDialog();
            UpdatePlacesOnGlobe();
        }

        private void ExecuteAddFolderToTemporary()
        {
            var targetList = (SelectedPlace != null && SelectedPlace.ItemType == PlaceItemType.Folder && !SelectedPlace.IsProtected && FindParentList(TemporaryPlaces, SelectedPlace) != null)
                ? SelectedPlace.Children
                : TemporaryPlaces;

            var newFolder = new PlaceItem($"Folder {TemporaryPlaces.Count + 1}", PlaceItemType.Folder)
            {
                ColorHex = "#EAB308"
            };

            targetList.Add(newFolder);
            SelectedPlace = newFolder;
            _projectService.IsModified = true;
            _temporarySessionService.SaveSession(TemporaryPlaces);
            UpdatePlacesOnGlobe();
        }

        private async Task ExecuteDrawLineToTemporaryAsync()
        {
            _drawTargetTemporary = true;
            await _globeBridge.StartDrawingAsync("path");
        }

        private async Task ExecuteDrawPolygonToTemporaryAsync()
        {
            _drawTargetTemporary = true;
            await _globeBridge.StartDrawingAsync("polygon");
        }

        private void ExecuteImportKmlToTemporary()
        {
            ExecuteImportKml();
        }

        private void ExecuteKeepInTemporary()
        {
            _temporarySessionService.SaveSession(TemporaryPlaces);
            if (Application.Current?.MainWindow != null)
            {
                MessageBox.Show($"Retained {TemporaryPlaces.Count} root item(s) in Temporary Places session store.", "Temporary Places", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteSaveTemporaryToProject()
        {
            _projectService.IsModified = true;
            if (Application.Current?.MainWindow != null)
            {
                MessageBox.Show("Temporary Places committed to active project document.", "Project Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteMoveToMyPlaces(PlaceItem? item)
        {
            var target = item ?? SelectedPlace;
            if (target == null || target.IsProtected) return;

            var parent = FindParentList(TemporaryPlaces, target);
            if (parent != null)
            {
                _undoRedo.ExecuteAction(new MoveToMyPlacesAction(parent, MyPlaces, target));
                _temporarySessionService.SaveSession(TemporaryPlaces);
                _projectService.IsModified = true;
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(TemporaryPlaces));
            }
        }

        private void ExecuteMoveToTemporaryPlaces(PlaceItem? item)
        {
            var target = item ?? SelectedPlace;
            if (target == null || target.IsProtected) return;

            var parent = FindParentList(MyPlaces, target);
            if (parent != null)
            {
                _undoRedo.ExecuteAction(new MoveToMyPlacesAction(parent, TemporaryPlaces, target));
                _temporarySessionService.SaveSession(TemporaryPlaces);
                _projectService.IsModified = true;
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(MyPlaces));
                OnPropertyChanged(nameof(TemporaryPlaces));
            }
        }

        public void ExecuteExportSelectedItem(PlaceItem? item = null)
        {
            var target = item ?? SelectedPlace;
            if (target == null)
            {
                MessageBox.Show("Please select an item to export.", "Export Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Keyhole Markup Language (*.kml)|*.kml|Zipped KMZ (*.kmz)|*.kmz",
                FileName = $"{target.Name.Replace(' ', '_')}.kml",
                Title = $"Export Selected: {target.Name}"
            };

            if (dlg.ShowDialog() == true)
            {
                var exportRoot = target.Clone();
                if (dlg.FileName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    KmzService.CreatePackage(dlg.FileName, exportRoot);
                }
                else
                {
                    KmlSerializer.SerializeToFile(exportRoot, dlg.FileName);
                }
                MessageBox.Show($"Exported '{target.Name}' to '{dlg.FileName}'.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void ExecuteExportAllMyPlaces()
        {
            if (MyPlaces.Count == 0)
            {
                MessageBox.Show("No places in My Places to export.", "Export All My Places", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Keyhole Markup Language (*.kml)|*.kml|Zipped KMZ (*.kmz)|*.kmz",
                FileName = "My_Places.kml",
                Title = "Export All My Places"
            };

            if (dlg.ShowDialog() == true)
            {
                var exportRoot = new PlaceItem("My Places", PlaceItemType.Folder);
                foreach (var p in MyPlaces) exportRoot.AddChild(p.Clone());

                if (dlg.FileName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    KmzService.CreatePackage(dlg.FileName, exportRoot);
                }
                else
                {
                    KmlSerializer.SerializeToFile(exportRoot, dlg.FileName);
                }
                MessageBox.Show($"Exported {MyPlaces.Count} item(s) to '{dlg.FileName}'.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteRemoveFromSession(PlaceItem? item)
        {
            var target = item ?? SelectedPlace;
            if (target == null) return;

            if (TemporaryPlaces.Contains(target))
            {
                TemporaryPlaces.Remove(target);
                _temporarySessionService.SaveSession(TemporaryPlaces);
                UpdatePlacesOnGlobe();
                OnPropertyChanged(nameof(TemporaryPlaces));
            }
        }

        private void ExecuteExportTemporaryKml()
        {
            if (TemporaryPlaces.Count == 0)
            {
                MessageBox.Show("No temporary places to export.", "Export KML", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Keyhole Markup Language (*.kml)|*.kml|Zipped KMZ (*.kmz)|*.kmz",
                FileName = "Temporary_Places.kml",
                Title = "Export Temporary Places"
            };

            if (dlg.ShowDialog() == true)
            {
                var exportRoot = new PlaceItem("Temporary Places Export", PlaceItemType.Folder);
                foreach (var p in TemporaryPlaces) exportRoot.AddChild(p.Clone());

                if (dlg.FileName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    KmzService.CreatePackage(dlg.FileName, exportRoot);
                }
                else
                {
                    KmlSerializer.SerializeToFile(exportRoot, dlg.FileName);
                }
                MessageBox.Show($"Exported {TemporaryPlaces.Count} item(s) to '{dlg.FileName}'.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteRenamePlace()
        {
            if (SelectedPlace == null) return;
            _projectService.IsModified = true;
            UpdateWindowTitle();
        }

        public void UpdatePlacesOnGlobe()
        {
            var flat = new List<object>();
            void Gather(IEnumerable<PlaceItem> items)
            {
                foreach (var item in items)
                {
                    if (item.IsVisible)
                    {
                        string altitudeMode = item.ExtendedData.TryGetValue("AltitudeMode", out var altitudeModeValue)
                            ? altitudeModeValue
                            : "ClampToGround";
                        double lineWidth = item.ExtendedData.TryGetValue("LineWidth", out var lineWidthValue) &&
                                           double.TryParse(lineWidthValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLineWidth)
                            ? parsedLineWidth : 3.0;
                        double fillOpacity = item.ExtendedData.TryGetValue("FillOpacity", out var fillOpacityValue) &&
                                             double.TryParse(fillOpacityValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFillOpacity)
                            ? parsedFillOpacity : 0.4;
                        double labelSize = item.ExtendedData.TryGetValue("LabelSize", out var labelSizeValue) &&
                                           double.TryParse(labelSizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLabelSize)
                            ? parsedLabelSize : 12.0;
                        bool showLabel = !item.ExtendedData.TryGetValue("ShowLabel", out var showLabelValue) ||
                                         !string.Equals(showLabelValue, "false", StringComparison.OrdinalIgnoreCase);
                        string symbolShape = item.ExtendedData.TryGetValue("SymbolShape", out var symbolShapeValue)
                            ? symbolShapeValue : "Circle";

                        if (item.ItemType == PlaceItemType.Polygon && item.Coordinates.Count >= 3)
                        {
                            flat.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                type = "polygon",
                                coordinates = item.Coordinates.Select(c => new { lon = c.Longitude, lat = c.Latitude, alt = c.Altitude }).ToList(),
                                innerRings = item.InnerRings.Select(r => r.Select(c => new { lon = c.Longitude, lat = c.Latitude, alt = c.Altitude }).ToList()).ToList(),
                                color = item.ColorHex,
                                lineWidth,
                                fillOpacity,
                                altitudeMode
                            });
                        }
                        else if (item.ItemType == PlaceItemType.Path && item.Coordinates.Count >= 2)
                        {
                            flat.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                type = "path",
                                coordinates = item.Coordinates.Select(c => new { lon = c.Longitude, lat = c.Latitude, alt = c.Altitude }).ToList(),
                                color = item.ColorHex,
                                lineWidth,
                                altitudeMode
                            });
                        }
                        else if (item.ItemType == PlaceItemType.Circle || item.ExtendedData.ContainsKey("RadiusMeters"))
                        {
                            double radius = 500;
                            if (item.ExtendedData.TryGetValue("RadiusMeters", out var rStr) && double.TryParse(rStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rVal))
                            {
                                radius = rVal;
                            }
                            flat.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                type = "circle",
                                lon = item.Longitude.GetValueOrDefault(0),
                                lat = item.Latitude.GetValueOrDefault(0),
                                alt = item.Altitude.GetValueOrDefault(0),
                                radiusMeters = radius,
                                color = item.ColorHex,
                                fillOpacity,
                                altitudeMode
                            });
                        }
                        else if (item.ItemType == PlaceItemType.GroundOverlay)
                        {
                            double west = -180, south = -90, east = 180, north = 90;
                            if (item.ExtendedData.TryGetValue("WestLon", out var wStr)) double.TryParse(wStr, NumberStyles.Float, CultureInfo.InvariantCulture, out west);
                            if (item.ExtendedData.TryGetValue("SouthLat", out var sStr)) double.TryParse(sStr, NumberStyles.Float, CultureInfo.InvariantCulture, out south);
                            if (item.ExtendedData.TryGetValue("EastLon", out var eStr)) double.TryParse(eStr, NumberStyles.Float, CultureInfo.InvariantCulture, out east);
                            if (item.ExtendedData.TryGetValue("NorthLat", out var nStr)) double.TryParse(nStr, NumberStyles.Float, CultureInfo.InvariantCulture, out north);
                            string imgUrl = item.ExtendedData.TryGetValue("OverlayImagePath", out var pStr) ? pStr : "";

                            flat.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                type = "groundOverlay",
                                west = west,
                                south = south,
                                east = east,
                                north = north,
                                imageUrl = imgUrl,
                                color = item.ColorHex,
                                fillOpacity
                            });
                        }
                        else if (item.ItemType == PlaceItemType.SavedScene)
                        {
                            if (SettingsService.Instance.Settings.ShowSceneLocationsOnMap && item.Latitude.HasValue && item.Longitude.HasValue)
                            {
                                flat.Add(new
                                {
                                    id = item.Id,
                                    name = item.Name,
                                    type = "point",
                                    lon = item.Longitude.Value,
                                    lat = item.Latitude.Value,
                                    alt = item.Altitude.GetValueOrDefault(0),
                                    color = item.ColorHex,
                                    labelSize,
                                    showLabel,
                                    symbolShape,
                                    altitudeMode
                                });
                            }
                        }
                        else if (item.Latitude.HasValue && item.Longitude.HasValue)
                        {
                            flat.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                type = "point",
                                lon = item.Longitude.Value,
                                lat = item.Latitude.Value,
                                alt = item.Altitude.GetValueOrDefault(0),
                                color = item.ColorHex,
                                labelSize,
                                showLabel,
                                symbolShape,
                                altitudeMode
                            });
                        }
                    }
                    Gather(item.Children);
                }
            }
            Gather(MyPlaces);
            _ = _globeBridge.SetPlacemarksAsync(flat);
        }

        public void ClearActiveSession()
        {
            _projectService.ClearActiveSession();
        }

        /// <summary>
        /// Resolves root items imported during this run. Imported data is intentionally shown
        /// beside the system folders while the application is open. Only an explicit Yes at
        /// shutdown moves it under Temporary Places and writes the durable session cache.
        /// </summary>
        public bool ResolvePendingPlacesOnClose()
        {
            var pending = _pendingSessionItems
                .Where(item => FindParentList(MyPlaces, item) != null && FindParentList(TemporaryPlaces, item) == null)
                .ToList();

            if (pending.Count == 0)
            {
                _pendingSessionItems.Clear();
                return true;
            }

            var result = MessageBox.Show(
                $"You imported {pending.Count} item(s) during this session.\n\n" +
                "Save them inside the Temporary Places folder for the next launch?\n\n" +
                "Yes: move them to Temporary Places and keep them.\n" +
                "No: close without keeping these imported items.\n" +
                "Cancel: return to Z EARTH.",
                "Save imported places?",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }

            foreach (var item in pending)
            {
                var parent = FindParentList(MyPlaces, item);
                if (parent == null) continue;

                parent.Remove(item);
                if (result == MessageBoxResult.Yes)
                {
                    TemporaryPlaces.Add(item);
                }
            }

            if (result == MessageBoxResult.Yes)
            {
                _temporarySessionService.SaveSession(TemporaryPlaces);
            }

            _pendingSessionItems.Clear();
            SelectedPlace = null;
            OnPropertyChanged(nameof(MyPlaces));
            OnPropertyChanged(nameof(TemporaryPlaces));
            UpdatePlacesOnGlobe();
            return true;
        }

        public Task<bool> SaveProjectCommandDirectAsync() => _projectService.SaveAsync();

        private void NotifyPlannedFeature(string featureName)
        {
            MessageBox.Show($"{featureName}\n\nThis feature is tracked in the Z EARTH development roadmap.", "Z EARTH GIS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ExecuteImportKml(string? directFilePath = null)
        {
            string? filePath = directFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "KML / KMZ Geographic Files (*.kml;*.kmz)|*.kml;*.kmz|Keyhole Markup Language (*.kml)|*.kml|Zipped KML (*.kmz)|*.kmz|All Files (*.*)|*.*",
                    Title = "Import KML / KMZ into Temporary Places"
                };

                if (dlg.ShowDialog() == true)
                {
                    filePath = dlg.FileName;
                }
            }

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    PlaceItem importedTree;
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext == ".kmz")
                    {
                        var extractTemp = Path.Combine(Path.GetTempPath(), "ZEarth", "KmzImports", Guid.NewGuid().ToString("N"));
                        importedTree = KmzService.ExtractAndParse(filePath, extractTemp);
                    }
                    else
                    {
                        importedTree = KmlParser.ParseFile(filePath);
                    }

                    importedTree.Name = Path.GetFileName(filePath);

                    // Like Google Earth Pro: imports first appear in Temporary Places.
                    // Explicit "Save to My Places" promotes them; the project/session can
                    // retain Temporary Places without silently committing them permanently.
                    _undoRedo.ExecuteAction(new AddPlaceAction(TemporaryPlaces, importedTree));
                    _temporarySessionService.SaveSession(TemporaryPlaces);
                    _projectService.IsModified = true;
                    SelectedPlace = importedTree;
                    UpdatePlacesOnGlobe();
                    OnPropertyChanged(nameof(MyPlaces));
                    OnPropertyChanged(nameof(TemporaryPlaces));

                    // Collect bounding coordinates to fit view
                    var coords = new List<CoordinatePoint>();
                    void CollectCoords(PlaceItem item)
                    {
                        if (item.Longitude.HasValue && item.Latitude.HasValue)
                        {
                            coords.Add(new CoordinatePoint(item.Longitude.Value, item.Latitude.Value, item.Altitude.GetValueOrDefault(0)));
                        }
                        coords.AddRange(item.Coordinates);
                        foreach (var c in item.Children) CollectCoords(c);
                    }
                    CollectCoords(importedTree);

                    if (coords.Count > 0)
                    {
                        double minLon = coords.Min(c => c.Longitude);
                        double maxLon = coords.Max(c => c.Longitude);
                        double minLat = coords.Min(c => c.Latitude);
                        double maxLat = coords.Max(c => c.Latitude);

                        _ = _globeBridge.FitSelectionAsync(new
                        {
                            west = minLon - 0.05,
                            south = minLat - 0.05,
                            east = maxLon + 0.05,
                            north = maxLat + 0.05,
                            durationSeconds = 1.5
                        });
                    }

                    if (string.IsNullOrEmpty(directFilePath))
                    {
                        MessageBox.Show($"File '{Path.GetFileName(filePath)}' was imported into Temporary Places.\nRoot: {importedTree.Name} ({importedTree.Children.Count} children)\n\nChoose Save to My Places to make it permanent.", "KML Import", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(directFilePath))
                    {
                        MessageBox.Show($"Failed to import '{Path.GetFileName(filePath)}':\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        public void ExecuteImportVector(string? directFilePath = null)
        {
            string? filePath = directFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "GIS Vector Files (*.geojson;*.json;*.shp;*.gpkg)|*.geojson;*.json;*.shp;*.gpkg|GeoJSON (*.geojson;*.json)|*.geojson;*.json|ESRI Shapefile (*.shp)|*.shp|OGC GeoPackage (*.gpkg)|*.gpkg|All Files (*.*)|*.*",
                    Title = "Import GIS Vector Layer into Temporary Places"
                };

                if (dlg.ShowDialog() == true)
                {
                    filePath = dlg.FileName;
                }
            }

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var result = VectorDatasetReader.ReadFile(filePath);
                    if (result.NeedsCrsResolution)
                    {
                        var crsDlg = new CrsSelectionDialog(new CrsRegistry())
                        {
                            Owner = Application.Current?.MainWindow
                        };
                        if (crsDlg.ShowDialog() == true && crsDlg.SelectedCrs != null)
                        {
                            result = VectorDatasetReader.ReadFile(filePath, $"EPSG:{crsDlg.SelectedCrs.EpsgCode}");
                        }
                    }

                    if (result.Success && result.Features.Count > 0)
                    {
                        var importFolder = new PlaceItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Name = Path.GetFileName(filePath),
                            ItemType = PlaceItemType.Folder,
                            IsExpanded = true
                        };
                        importFolder.ExtendedData["SourceCrs"] = result.SourceCrs;

                        foreach (var feat in result.Features)
                        {
                            importFolder.Children.Add(feat);
                        }

                        // Import all external datasets into the same Temporary Places
                        // folder used for KML/KMZ; retain source CRS metadata and
                        // allow deliberate promotion to My Places.
                        TemporaryPlaces.Add(importFolder);
                        _temporarySessionService.SaveSession(TemporaryPlaces);
                        _projectService.IsModified = true;
                        SelectedPlace = importFolder;
                        UpdatePlacesOnGlobe();
                        OnPropertyChanged(nameof(MyPlaces));
                        OnPropertyChanged(nameof(TemporaryPlaces));

                        if (result.MinLon <= result.MaxLon && result.MinLat <= result.MaxLat)
                        {
                            double centerLon = (result.MinLon + result.MaxLon) / 2.0;
                            double centerLat = (result.MinLat + result.MaxLat) / 2.0;
                            double span = Math.Max(result.MaxLon - result.MinLon, result.MaxLat - result.MinLat);
                            double alt = Math.Max(2000.0, span * 111000.0 * 1.5);

                            _ = _globeBridge.FlyToAsync(new CameraState
                            {
                                Latitude = centerLat,
                                Longitude = centerLon,
                                Altitude = alt
                            });
                        }

                        if (string.IsNullOrEmpty(directFilePath))
                        {
                            MessageBox.Show($"Vector dataset '{Path.GetFileName(filePath)}' ({result.Features.Count} features) was imported into My Places.\nSource CRS: {result.SourceCrs}\n\nYou can keep it in Temporary Places when you close Z EARTH.", "Vector Import", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else if (!result.Success && string.IsNullOrEmpty(directFilePath))
                    {
                        MessageBox.Show($"Failed to read vector dataset:\n{result.ErrorMessage}", "Vector Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(directFilePath))
                    {
                        MessageBox.Show($"Error importing vector dataset:\n{ex.Message}", "Vector Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ExecuteTakeScreenshot()
        {
            try
            {
                var screenshotsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ZEarth");
                Directory.CreateDirectory(screenshotsDir);
                var filePath = Path.Combine(screenshotsDir, $"ZEarth_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                MessageBox.Show($"Map view snapshot saved to:\n{filePath}", "Capture Viewport", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Screenshot notice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ExecuteOverlayImage()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Raster Imagery (*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp)|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp|GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|PNG/JPEG (*.png;*.jpg)|*.png;*.jpg|All Files (*.*)|*.*",
                Title = "Select Image to Overlay on 3D Globe"
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                double west, south, east, north;
                if (ext == ".tif" || ext == ".tiff")
                {
                    var res = VectorDatasetReader.ReadGeoTiff(filePath);
                    if (res.Success && res.Features.Count > 0)
                    {
                        var feat = res.Features[0];
                        west = res.MinLon;
                        south = res.MinLat;
                        east = res.MaxLon;
                        north = res.MaxLat;

                        MyPlaces.Add(feat);
                        _projectService.IsModified = true;
                        string imgPath = feat.ExtendedData.TryGetValue("OverlayImagePath", out var p) ? p : filePath;
                        _ = _globeBridge.AddRasterOverlayAsync(feat.Id, feat.Name, imgPath, west, south, east, north);
                        _ = _globeBridge.FlyToAsync(new CameraState((south + north) / 2.0, (west + east) / 2.0, Math.Max(2000.0, Math.Max(east - west, north - south) * 111000.0 * 1.5), 0, -90), 1.2);
                        UpdatePlacesOnGlobe();
                        return;
                    }
                }

                west = CurrentCamera.Longitude - 0.05;
                east = CurrentCamera.Longitude + 0.05;
                south = CurrentCamera.Latitude - 0.05;
                north = CurrentCamera.Latitude + 0.05;

                var item = new PlaceItem(Path.GetFileNameWithoutExtension(filePath), PlaceItemType.GroundOverlay, CurrentCamera.Latitude, CurrentCamera.Longitude)
                {
                    Coordinates = new List<CoordinatePoint>
                    {
                        new(west, north, 0),
                        new(east, north, 0),
                        new(east, south, 0),
                        new(west, south, 0),
                        new(west, north, 0)
                    }
                };
                item.ExtendedData["WestLon"] = west.ToString("F6", CultureInfo.InvariantCulture);
                item.ExtendedData["EastLon"] = east.ToString("F6", CultureInfo.InvariantCulture);
                item.ExtendedData["SouthLat"] = south.ToString("F6", CultureInfo.InvariantCulture);
                item.ExtendedData["NorthLat"] = north.ToString("F6", CultureInfo.InvariantCulture);
                item.ExtendedData["OverlayImagePath"] = filePath;

                MyPlaces.Add(item);
                _projectService.IsModified = true;
                _ = _globeBridge.AddRasterOverlayAsync(item.Id, item.Name, filePath, west, south, east, north);
                _ = _globeBridge.FlyToAsync(new CameraState(CurrentCamera.Latitude, CurrentCamera.Longitude, 15000, 0, -90), 1.2);
                UpdatePlacesOnGlobe();
            }
        }

        public void ExecuteModel3D()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "3D Model Files (*.gltf;*.glb;*.dae;*.obj)|*.gltf;*.glb;*.dae;*.obj|glTF Binary (*.glb)|*.glb|glTF (*.gltf)|*.gltf|Collada (*.dae)|*.dae|Wavefront OBJ (*.obj)|*.obj|All Files (*.*)|*.*",
                Title = "Import 3D Model into Scene"
            };
            if (dlg.ShowDialog() == true)
            {
                var item = new PlaceItem(Path.GetFileNameWithoutExtension(dlg.FileName), PlaceItemType.Placemark, CurrentCamera.Latitude, CurrentCamera.Longitude, CurrentCamera.Altitude);
                item.ExtendedData["ModelPath"] = dlg.FileName;
                MyPlaces.Add(item);
                _projectService.IsModified = true;
                UpdatePlacesOnGlobe();
                MessageBox.Show($"3D Model '{Path.GetFileName(dlg.FileName)}' anchored at current coordinate position ({CurrentCamera.Latitude:F4}°, {CurrentCamera.Longitude:F4}°).", "3D Model Imported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void ExecuteElevationProfile()
        {
            IEnumerable<CoordinatePoint>? points = null;

            if (SelectedPlace != null && SelectedPlace.Coordinates.Count >= 2)
            {
                points = SelectedPlace.Coordinates;
            }
            else if (_lastMeasurementPoints.Count >= 2)
            {
                points = _lastMeasurementPoints;
            }
            else
            {
                var pathFeat = MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Path && p.Coordinates.Count >= 2)
                            ?? TemporaryPlaces.SelectMany(f => f.Children).FirstOrDefault(p => p.ItemType == PlaceItemType.Path && p.Coordinates.Count >= 2);
                if (pathFeat != null)
                {
                    points = pathFeat.Coordinates;
                }
            }

            if (points == null || points.Count() < 2)
            {
                MessageBox.Show("Draw or select a path with at least two coordinates first. " +
                    "A terrain elevation profile requires actual DEM samples; Z EARTH will not invent heights.",
                    "Elevation Profile — No Valid Path", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Stored vertex altitudes are not equivalent to elevations sampled from
            // the active DEM; the dialog labels their provenance explicitly.
            var dlg = new Views.ElevationProfileDialog(points)
            {
                Owner = Application.Current?.MainWindow
            };
            dlg.ShowDialog();
        }

        public void ExecuteShowOptions() => ExecuteShowOptions(false);

        private void ExecuteShowOptions(bool openGridTab)
        {
            var wasTerrainEnabled = IsTerrainEnabled;
            var dlg = new Views.OptionsDialog(_globeBridge, openGridTab)
            {
                Owner = Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() == true)
            {
                var s = SettingsService.Instance.Settings;
                IsSnapEnabled = s.SnappingEnabled;
                SnapTolerancePixels = s.SnappingTolerancePixels;
                SnapGroundToleranceMeters = s.GroundToleranceMeters;
                SnapToVertices = s.SnapVertices;
                SnapToMidpoints = s.SnapMidpoints;
                SnapToEdges = s.SnapEdges;
                IsGraticulesVisible = s.ShowGraticules;
                RefreshBasemapRibbonGroups();
                SyncTerrainSettingsDisplay(updateEnabledState: false);
                if (wasTerrainEnabled)
                {
                    _ = ActivateConfiguredTerrainAsync();
                }
                else
                {
                    IsTerrainEnabled = false;
                    UpdateTerrainStatusDisplay();
                }
                OnPropertyChanged(nameof(DefaultBasemapDisplay));
                UpdateTelemetryStrings();
            }
        }

        private void RefreshBasemapRibbonGroups()
        {
            var defaultId = SettingsService.Instance.Settings.DefaultBasemap;
            var providerOrder = new[] { "Natural Earth", "OpenStreetMap", "CARTO", "Esri" };
            var groups = AvailableBasemaps
                .GroupBy(layer => string.IsNullOrWhiteSpace(layer.ProviderGroup) ? "Other" : layer.ProviderGroup)
                .OrderBy(group =>
                {
                    var index = Array.IndexOf(providerOrder, group.Key);
                    return index < 0 ? int.MaxValue : index;
                })
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            BasemapRibbonGroups.Clear();
            foreach (var group in groups)
            {
                BasemapRibbonGroups.Add(new BasemapRibbonGroup
                {
                    ProviderName = group.Key,
                    Items = group
                        .OrderBy(layer => layer.Order)
                        .ThenBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(layer => new BasemapRibbonItem
                        {
                            Id = layer.Id,
                            Name = layer.Name,
                            ToolTip = $"{layer.Name}\n{layer.ExtentDescription}\n{layer.StatusMessage}\n{layer.Attribution}",
                            IsDefault = string.Equals(layer.Id, defaultId, StringComparison.OrdinalIgnoreCase)
                        })
                        .ToList()
                });
            }
        }

        private void SyncTerrainSettingsDisplay(bool updateEnabledState = false)
        {
            var providerId = SettingsService.Instance.Settings.TerrainProviderId;
            switch (providerId)
            {
                case "demTerrain":
                    _terrainConfig.Type = TerrainType.LocalDem;
                    _terrainConfig.Name = "AWS Open Data Terrarium DEM";
                    break;
                case "worldTerrain":
                    _terrainConfig.Type = TerrainType.CesiumWorldTerrain;
                    _terrainConfig.Name = "Cesium World Terrain / Copernicus GLO-30";
                    break;
                case "worldBathymetry":
                    _terrainConfig.Type = TerrainType.CesiumWorldTerrain;
                    _terrainConfig.Name = "Cesium World Bathymetry / GEBCO 2023";
                    break;
                case "custom":
                    _terrainConfig.Type = TerrainType.Custom;
                    _terrainConfig.Name = "Custom Quantized-Mesh Terrain";
                    _terrainConfig.Url = SettingsService.Instance.Settings.CustomTerrainUrl ?? string.Empty;
                    break;
                default:
                    _terrainConfig.Type = TerrainType.Ellipsoid;
                    _terrainConfig.Name = "WGS84 Smooth Ellipsoid (Offline)";
                    break;
            }

            // Configuration is only a selected preference. Never display green/ON
            // until Cesium has acknowledged a real provider activation.
            UpdateTerrainStatusDisplay();
        }

        private void UpdateTerrainStatusDisplay()
        {
            TerrainProviderType = IsTerrainEnabled
                ? _terrainConfig.Name
                : $"Off — Default: {_terrainConfig.Name}";
        }

        public Task InitializeConfiguredTerrainAsync() => ActivateConfiguredTerrainAsync();

        private async Task ActivateConfiguredTerrainAsync()
        {
            var settings = SettingsService.Instance.Settings;
            var providerId = settings.TerrainProviderId;

            if (string.Equals(providerId, "ellipsoid", StringComparison.OrdinalIgnoreCase))
            {
                await _globeBridge.SetTerrainProviderAsync("ellipsoid");
                IsTerrainEnabled = false;
            }
            else
            {
                IsTerrainEnabled = await _globeBridge.SetTerrainProviderAsync(
                    providerId,
                    settings.CustomTerrainUrl,
                    settings.CesiumIonToken);
            }

            SyncTerrainSettingsDisplay(updateEnabledState: false);
            UpdateTerrainStatusDisplay();
            if (IsTerrainEnabled && !await _globeBridge.SetTerrainExaggerationAsync(_verticalExaggeration))
                StatusMessage = "Terrain loaded, but vertical exaggeration was not acknowledged.";
        }

        private bool _terrainRequestInProgress;

        public async Task ExecuteToggleTerrainAsync()
        {
            if (_terrainRequestInProgress) return;
            _terrainRequestInProgress = true;
            try
            {
            StatusMessage = IsTerrainEnabled ? "Disabling terrain…" : "Loading selected elevation model…";
            if (IsTerrainEnabled)
            {
                if (await _globeBridge.SetTerrainProviderAsync("ellipsoid"))
                {
                    IsTerrainEnabled = false;
                    UpdateTerrainStatusDisplay();
                    StatusMessage = "Terrain disabled; smooth ellipsoid restored.";
                }
                else StatusMessage = "Unable to disable terrain: globe did not acknowledge.";
                return;
            }

            var settings = SettingsService.Instance.Settings;
            if (string.Equals(settings.TerrainProviderId, "ellipsoid", StringComparison.OrdinalIgnoreCase))
            {
                IsTerrainEnabled = false;
                UpdateTerrainStatusDisplay();
                StatusMessage = "WGS84 ellipsoid has no elevation relief. Choose an actual DEM under Options → Terrain Models.";
                return;
            }

            await ActivateConfiguredTerrainAsync();
            StatusMessage = IsTerrainEnabled
                ? $"Terrain active: {_terrainConfig.Name}"
                : $"Terrain unavailable: {_terrainConfig.Name}. Check the provider connection, URL or Cesium ion token.";
            }
            finally { _terrainRequestInProgress = false; }
        }

        private async Task ExecuteShowHistoricalImageryAsync()
        {
            // An in-globe overlay avoids WPF WebView2 airspace and modal ShowDialog.
            // The release catalog is fetched in .NET; the JS viewer draws an interactive timeline.
            await _globeBridge.SendRequestAsync("historical.loading");
            try
            {
                var releases = await new WaybackService().GetReleasesAsync();
                if (releases.Count == 0) throw new InvalidOperationException("No Wayback releases.");
                var items = releases.Select(r => new
                {
                    releaseId = r.ReleaseId,
                    date = r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    tileUrl = r.TileUrl
                }).ToArray();
                if (!await _globeBridge.SendRequestAsync("historical.show", new { releases = items }, 18000))
                {
                    StatusMessage = "Historical imagery timeline unavailable in the globe engine.";
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning("Historical imagery release catalog error: " + ex.Message);
                StatusMessage = "Historical imagery catalog is unavailable; retry when online.";
                await _globeBridge.SendRequestAsync("historical.error", new
                {
                    message = "Wayback catalog unavailable. Check internet access and retry."
                });
            }
        }

        public void ExecuteStartSwipe(string leftId, string rightId)
        {
            _ = _globeBridge.StartSwipeAsync(leftId, rightId);
            var leftLayer = _imageryRegistry.GetLayer(leftId);
            var rightLayer = _imageryRegistry.GetLayer(rightId);
            ActiveBasemap = $"Swipe: {leftLayer?.Name ?? leftId} ⇄ {rightLayer?.Name ?? rightId}";
            ActiveAttribution = $"{leftLayer?.Attribution} | {rightLayer?.Attribution}";
        }

        public void ExecuteStopSwipe()
        {
            _ = _globeBridge.StopSwipeAsync();
            ExecuteSelectBasemap(SelectedBasemapId);
        }

        public void ExecuteShowCoordinateConverter()
        {
            var dlg = new Views.CoordinateConverterDialog(CurrentCamera.Latitude, CurrentCamera.Longitude)
            {
                Owner = Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() == true && dlg.RequestedFlyTo)
            {
                _ = _globeBridge.FlyToAsync(new CameraState(dlg.ConvertedLat, dlg.ConvertedLon, 50000, 0, -90), 1.2);
            }
        }

        private void OnLayerVisibilityChanged(ImageryLayerViewModel vm)
        {
            _ = _globeBridge.SetLayerVisibilityAsync(vm.Id, vm.IsVisible);
            ActiveAttribution = _imageryRegistry.GetCombinedAttribution();
        }

        private void OnLayerOpacityChanged(ImageryLayerViewModel vm)
        {
            _ = _globeBridge.SetLayerOpacityAsync(vm.Id, vm.Opacity);
        }

        public void ExecuteSelectBasemap(string basemapId)
        {
            if (string.IsNullOrEmpty(basemapId)) return;
            _selectedBasemapId = basemapId;
            OnPropertyChanged(nameof(SelectedBasemapId));
            var layer = _imageryRegistry.GetLayer(basemapId);
            if (layer != null)
            {
                _imageryRegistry.SetBasemap(basemapId);
                ActiveBasemap = layer.Name;
            }
            _ = _globeBridge.SetBasemapAsync(basemapId);
            ActiveAttribution = _imageryRegistry.GetCombinedAttribution();
            OnPropertyChanged(nameof(StatusEngineInfo));
        }

        public void ExecuteAddImageryLayer()
        {
            var dlg = new AddImageryLayerDialog
            {
                Owner = Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() == true && dlg.CreatedLayer != null)
            {
                var layer = dlg.CreatedLayer;
                if (_imageryRegistry.AddCustomLayer(layer))
                {
                    var vm = new ImageryLayerViewModel(layer, OnLayerVisibilityChanged, OnLayerOpacityChanged);
                    ActiveLayers.Add(vm);
                    _ = _globeBridge.AddImageryLayerAsync(layer.Id, layer.Name, layer.ProviderType.ToString(), layer.UrlTemplate, layer.Attribution, layer.Opacity, layer.IsVisible);
                    ActiveAttribution = _imageryRegistry.GetCombinedAttribution();
                }
            }
        }

        public void ExecuteRemoveImageryLayer(ImageryLayerViewModel vm)
        {
            if (vm == null || vm.IsBaseLayer) return;
            if (_imageryRegistry.RemoveLayer(vm.Id))
            {
                ActiveLayers.Remove(vm);
                _ = _globeBridge.RemoveImageryLayerAsync(vm.Id);
                ActiveAttribution = _imageryRegistry.GetCombinedAttribution();
            }
        }

        public void ExecuteMoveLayerUp(ImageryLayerViewModel vm)
        {
            if (vm == null) return;
            int idx = ActiveLayers.IndexOf(vm);
            if (idx > 0 && _imageryRegistry.MoveUp(vm.Id))
            {
                ActiveLayers.Move(idx, idx - 1);
                _ = _globeBridge.ReorderLayerAsync(vm.Id, "up");
            }
        }

        public void ExecuteMoveLayerDown(ImageryLayerViewModel vm)
        {
            if (vm == null) return;
            int idx = ActiveLayers.IndexOf(vm);
            if (idx >= 0 && idx < ActiveLayers.Count - 1 && _imageryRegistry.MoveDown(vm.Id))
            {
                ActiveLayers.Move(idx, idx + 1);
                _ = _globeBridge.ReorderLayerAsync(vm.Id, "down");
            }
        }

        public void ExecuteSetVerticalExaggeration(double factor)
        {
            _terrainConfig.SetExaggeration(factor);
            VerticalExaggeration = _terrainConfig.Exaggeration;
        }

        public void ExecuteResetVerticalExaggeration()
        {
            _terrainConfig.ResetExaggeration();
            VerticalExaggeration = _terrainConfig.Exaggeration;
        }

        private void ShowAbout()
        {
            var aboutWindow = new Views.AboutDialog
            {
                Owner = Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        }

        private void ShowDiagnostics()
        {
            var projectPath = _projectService.CurrentFilePath ?? "[Unsaved In-Memory]";
            var diagInfo = "Z EARTH Desktop GIS - System & Architecture Diagnostics\n" +
                           "------------------------------------------------------\n" +
                           "Developer: Ahmad Ibrahim\n" +
                           "Target Framework: .NET 10.0 (C# 13)\n" +
                           "UI Subsystem: WPF (MVVM)\n" +
                           "Viewport: WebView2 + Native 3D/2D Projection Engine\n" +
                           $"Current CRS: {StatusCrs}\n" +
                           $"Current Camera: {CurrentCamera}\n" +
                           $"History Stack Depth: {_cameraHistory.Count} states\n" +
                           $"Project Path: {projectPath}\n" +
                           $"Terrain diagnostic: {_latestTerrainProbe}\n" +
                           $"Places Count: {MyPlaces.Count} root items\n" +
                           $"Logs Count: {LoggerService.Instance.MemoryLogs.Count} entries\n" +
                           "REA Integration: optional external tooling (not configured)";

            MessageBox.Show(diagInfo, "Z EARTH Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
