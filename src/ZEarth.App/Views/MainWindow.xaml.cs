using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;

namespace ZEarth.App.Views
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel => (MainViewModel)DataContext;

        private System.Windows.Threading.DispatcherTimer? _telemetrySummaryTimer;
        private bool _isClosing;
        private static readonly HttpClient TerrainHttpClient = CreateTerrainHttpClient();

        private static HttpClient CreateTerrainHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ZEarth-Desktop-GIS/2.2");
            return client;
        }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsSidebarDockedLeft) ||
                    e.PropertyName == nameof(MainViewModel.IsSidebarCollapsed))
                {
                    ApplySidebarLayout();
                }
            };

            ThemeService.Instance.ThemeChanged += (s, theme) =>
            {
                ThemeService.Instance.ApplyWindowTheme(this, theme);
            };

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Guard against re-entrant close handling while an async save is in progress (RC-7).
            if (_isClosing)
            {
                return;
            }

            // Imported KML/KMZ/vector roots stay beside the built-in folders while Z EARTH is
            // running. At shutdown the user decides whether they should be retained under the
            // Temporary Places folder, discarded, or whether closing should be cancelled.
            if (ViewModel != null && !ViewModel.ResolvePendingPlacesOnClose())
            {
                e.Cancel = true;
                return;
            }

            if (ViewModel != null && ViewModel.IsModified)
            {
                var projectName = ViewModel.CurrentProject.Name;
                var res = MessageBox.Show(
                    $"Do you want to save changes to '{projectName}' before closing?",
                    "Z EARTH Desktop GIS",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    _isClosing = true;
                    try
                    {
                        await ViewModel.SaveProjectCommandDirectAsync();
                        ViewModel.ClearActiveSession();
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogError("Failed to save project during shutdown", ex);
                    }
                    CleanupOnClose();
                    Application.Current.Shutdown();
                    return;
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            CleanupOnClose();
            ViewModel?.ClearActiveSession();
        }

        private void CleanupOnClose()
        {
            try
            {
                _telemetrySummaryTimer?.Stop();
                _telemetrySummaryTimer = null;

                // Resolve any in-flight correlated request so shutdown never hangs waiting (RC-3/RC-7).
                ViewModel?.GlobeBridge?.CancelAllPending("Application is shutting down.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Close cleanup notice: {ex.Message}");
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            ThemeService.Instance.ApplyWindowTheme(this, ThemeService.Instance.CurrentTheme);
            ApplySidebarLayout();
        }

        private void ApplySidebarLayout()
        {
            if (ViewModel == null) return;

            bool isLeft = ViewModel.IsSidebarDockedLeft;
            bool isCollapsed = ViewModel.IsSidebarCollapsed;

            if (isCollapsed)
            {
                SidebarBorder.Visibility = Visibility.Collapsed;
                WorkspaceSplitter.Visibility = Visibility.Collapsed;
                if (isLeft)
                {
                    ColLeft.Width = new GridLength(28);
                    ColLeft.MinWidth = 28;
                    ColLeft.MaxWidth = 28;
                    ColRight.Width = new GridLength(1, GridUnitType.Star);
                    ColRight.MinWidth = 400;
                    ColRight.MaxWidth = double.PositiveInfinity;

                    Grid.SetColumn(RevealSidebarLeftBtn, 0);
                    RevealSidebarLeftBtn.Visibility = Visibility.Visible;
                    RevealSidebarRightBtn.Visibility = Visibility.Collapsed;

                    Grid.SetColumn(ViewportBorder, 2);
                }
                else
                {
                    ColRight.Width = new GridLength(28);
                    ColRight.MinWidth = 28;
                    ColRight.MaxWidth = 28;
                    ColLeft.Width = new GridLength(1, GridUnitType.Star);
                    ColLeft.MinWidth = 400;
                    ColLeft.MaxWidth = double.PositiveInfinity;

                    Grid.SetColumn(RevealSidebarRightBtn, 2);
                    RevealSidebarRightBtn.Visibility = Visibility.Visible;
                    RevealSidebarLeftBtn.Visibility = Visibility.Collapsed;

                    Grid.SetColumn(ViewportBorder, 0);
                }
            }
            else
            {
                RevealSidebarLeftBtn.Visibility = Visibility.Collapsed;
                RevealSidebarRightBtn.Visibility = Visibility.Collapsed;
                SidebarBorder.Visibility = Visibility.Visible;
                WorkspaceSplitter.Visibility = Visibility.Visible;

                if (isLeft)
                {
                    ColLeft.Width = new GridLength(ViewModel.SidebarWidth);
                    ColLeft.MinWidth = 220;
                    ColLeft.MaxWidth = 520;
                    ColRight.Width = new GridLength(1, GridUnitType.Star);
                    ColRight.MinWidth = 400;
                    ColRight.MaxWidth = double.PositiveInfinity;

                    Grid.SetColumn(SidebarBorder, 0);
                    SidebarBorder.BorderThickness = new Thickness(0, 0, 1, 0);
                    Grid.SetColumn(WorkspaceSplitter, 1);
                    Grid.SetColumn(ViewportBorder, 2);
                }
                else
                {
                    ColLeft.Width = new GridLength(1, GridUnitType.Star);
                    ColLeft.MinWidth = 400;
                    ColLeft.MaxWidth = double.PositiveInfinity;
                    ColRight.Width = new GridLength(ViewModel.SidebarWidth);
                    ColRight.MinWidth = 220;
                    ColRight.MaxWidth = 520;

                    Grid.SetColumn(ViewportBorder, 0);
                    Grid.SetColumn(WorkspaceSplitter, 1);
                    Grid.SetColumn(SidebarBorder, 2);
                    SidebarBorder.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WorkspaceSplitter.DragCompleted += (s, e) =>
            {
                if (ViewModel.IsSidebarDockedLeft)
                {
                    ViewModel.SidebarWidth = ColLeft.ActualWidth;
                }
                else
                {
                    ViewModel.SidebarWidth = ColRight.ActualWidth;
                }
            };

            await InitializeGlobeWebViewAsync();
        }

        private async Task InitializeGlobeWebViewAsync()
        {
            try
            {
                // Validate the deployed assets before navigation so a missing file yields a clear
                // diagnostic instead of a blank viewport and silent request timeouts (RC-8/RC-9).
                if (!Globe.GlobeBridge.TryGetValidatedAssetsDirectory(out var assetsDir, out var assetsError))
                {
                    LoggerService.Instance.LogError($"Globe asset validation failed: {assetsError}");
                    MessageBox.Show(
                        $"Globe assets could not be located.\n{assetsError}\n\nThe viewport will not be initialized. See logs for details.",
                        "Z EARTH Viewport",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth", "WebView2UserData");
                Directory.CreateDirectory(userDataDir);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
                await GlobeWebView.EnsureCoreWebView2Async(env);

                GlobeWebView.CoreWebView2.Settings.IsScriptEnabled = true;
                GlobeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
                GlobeWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
                GlobeWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

                // Wire up GlobeBridge. Posting is dispatched asynchronously so the WebView2 message
                // pump is never blocked by a synchronous Invoke (RC-4).
                ViewModel.GlobeBridge.SendJsonToWebView = json =>
                {
                    if (Dispatcher.CheckAccess())
                    {
                        GlobeWebView.CoreWebView2?.PostWebMessageAsJson(json);
                        return Task.CompletedTask;
                    }

                    return Dispatcher.InvokeAsync(() =>
                    {
                        GlobeWebView.CoreWebView2?.PostWebMessageAsJson(json);
                    }).Task;
                };

                ViewModel.GlobeBridge.EngineReady += async (s, msg) =>
                {
                    LoggerService.Instance.LogInfo($"CesiumJS Geographic Engine Ready: {msg}");
                    try
                    {
                        var themeStr = ThemeService.Instance.CurrentTheme == AppTheme.Light ? "light" : "dark";
                        await ViewModel.GlobeBridge.SetThemeAsync(themeStr);

                        var dUnit = Domain.Units.UnitService.Instance.SelectedDistanceUnit.ToString();
                        var aUnit = Domain.Units.UnitService.Instance.SelectedAreaUnit.ToString();
                        var eUnit = Domain.Units.UnitService.Instance.SelectedElevationUnit.ToString();
                        await ViewModel.GlobeBridge.SetUnitsConfigAsync(dUnit, aUnit, eUnit);

                        var snap = Domain.Settings.SnappingSettings.Instance;
                        await ViewModel.GlobeBridge.SetSnappingConfigAsync(
                            snap.IsEnabled,
                            snap.Mode.ToString(),
                            snap.TolerancePixels,
                            snap.GroundToleranceMeters,
                            snap.SnapToVertices,
                            snap.SnapToMidpoints,
                            snap.SnapToEdges,
                            snap.SnapToIntersections
                        );

                        var appSettings = SettingsService.Instance.Settings;
                        await ViewModel.GlobeBridge.SetGraticulesConfigAsync(
                            appSettings.ShowGraticules,
                            appSettings.ShowGraticuleLabels,
                            appSettings.ShowKeyParallels,
                            appSettings.ShowUtmZones,
                            appSettings.ShowLongitudeLines,
                            appSettings.ShowLatitudeLines,
                            appSettings.LongitudeIntervalDegrees,
                            appSettings.LatitudeIntervalDegrees);

                        if (!string.IsNullOrEmpty(ViewModel.SelectedBasemapId))
                        {
                            await ViewModel.GlobeBridge.SetBasemapAsync(ViewModel.SelectedBasemapId);
                        }

                        await ViewModel.GlobeBridge.SetTerrainProviderAsync(
                            appSettings.TerrainProviderId,
                            appSettings.CustomTerrainUrl,
                            appSettings.CesiumIonToken);

                        // Restore saved startup scene on launch (SIDEBAR-01 SB-05 & SB-06)
                        await ViewModel.RestoreStartupSceneAsync();
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogError("Error synchronizing initial engine state", ex);
                    }
                };

                ViewModel.GlobeBridge.WebError += (s, msg) =>
                {
                    LoggerService.Instance.LogError($"CesiumJS Web Error/Warning: {msg}");
                };

                GlobeWebView.CoreWebView2.WebMessageReceived += (s, args) =>
                {
                    string msg;
                    try
                    {
                        msg = args.TryGetWebMessageAsString();
                    }
                    catch
                    {
                        msg = args.WebMessageAsJson;
                    }

                    // NOTE: individual messages are intentionally NOT logged here — doing so wrote
                    // thousands of synchronous disk lines per session and stalled the UI thread
                    // (RC-1). Aggregate counters are logged periodically instead (see below).
                    ViewModel.GlobeBridge.HandleWebMessage(msg);
                };

                GlobeWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        LoggerService.Instance.LogInfo($"Globe WebView2 navigation completed successfully. HTTP: {args.HttpStatusCode}");
                    }
                    else
                    {
                        LoggerService.Instance.LogError($"Globe WebView2 navigation error: {args.WebErrorStatus}, HTTP: {args.HttpStatusCode}");
                    }
                };

                // The public Mapzen/AWS Terrarium bucket serves valid elevation PNGs but does
                // not include browser CORS headers. Proxy only the tightly validated numeric tile
                // path through the desktop host so the local WebView can decode the pixels.
                GlobeWebView.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://terrain.local/terrarium/*",
                    CoreWebView2WebResourceContext.All);
                GlobeWebView.CoreWebView2.WebResourceRequested += TerrainWebResourceRequested;

                // Map local assets folder to virtual host https://cesium.local/ for secure origin and Web Workers
                GlobeWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "cesium.local",
                    assetsDir,
                    CoreWebView2HostResourceAccessKind.Allow
                );

                GlobeWebView.CoreWebView2.Navigate("https://cesium.local/index.html");
                LoggerService.Instance.LogInfo($"Globe WebView2 mapped virtual host 'cesium.local' to {assetsDir} and navigated to https://cesium.local/index.html");

                StartTelemetrySummaryTimer();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError("Failed to initialize WebView2 Globe", ex);
                MessageBox.Show($"Globe initialization notice: {ex.Message}\nCheck WebView2 runtime or logs for details.", "Z EARTH Viewport", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void TerrainWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            using var deferral = e.GetDeferral();
            try
            {
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var requestUri) ||
                    !string.Equals(requestUri.Host, "terrain.local", StringComparison.OrdinalIgnoreCase))
                {
                    e.Response = CreateTerrainProxyResponse(Array.Empty<byte>(), 400, "Bad Request", "text/plain");
                    return;
                }

                var parts = requestUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4 ||
                    !string.Equals(parts[0], "terrarium", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(parts[1], out var level) ||
                    !int.TryParse(parts[2], out var x) ||
                    !parts[3].EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(parts[3][..^4], out var y) ||
                    level < 0 || level > 15)
                {
                    e.Response = CreateTerrainProxyResponse(Array.Empty<byte>(), 400, "Bad Request", "text/plain");
                    return;
                }

                var maxCoordinate = (1 << level) - 1;
                if (x < 0 || y < 0 || x > maxCoordinate || y > maxCoordinate)
                {
                    e.Response = CreateTerrainProxyResponse(Array.Empty<byte>(), 400, "Bad Request", "text/plain");
                    return;
                }

                var upstream = new Uri(
                    $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{level}/{x}/{y}.png");
                using var upstreamResponse = await TerrainHttpClient.GetAsync(upstream);
                var content = await upstreamResponse.Content.ReadAsByteArrayAsync();
                e.Response = CreateTerrainProxyResponse(
                    content,
                    (int)upstreamResponse.StatusCode,
                    upstreamResponse.ReasonPhrase ?? "Terrain Response",
                    upstreamResponse.Content.Headers.ContentType?.ToString() ?? "image/png");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Terrain tile proxy failed: {ex.Message}");
                e.Response = CreateTerrainProxyResponse(
                    System.Text.Encoding.UTF8.GetBytes("Terrain tile unavailable"),
                    502,
                    "Bad Gateway",
                    "text/plain; charset=utf-8");
            }
        }

        private CoreWebView2WebResourceResponse CreateTerrainProxyResponse(
            byte[] content,
            int statusCode,
            string reasonPhrase,
            string contentType)
        {
            var stream = new MemoryStream(content, writable: false);
            var headers =
                $"Content-Type: {contentType}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Cache-Control: public, max-age=86400\r\n";
            return GlobeWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                stream,
                statusCode,
                reasonPhrase,
                headers);
        }

        /// <summary>
        /// Logs a single aggregated telemetry line every 10 seconds instead of one line per message.
        /// Replaces the per-message logging that saturated the log file and blocked the UI (RC-1).
        /// </summary>
        private void StartTelemetrySummaryTimer()
        {
            if (_telemetrySummaryTimer != null) return;

            _telemetrySummaryTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _telemetrySummaryTimer.Tick += (s, e) =>
            {
                try
                {
                    var c = ViewModel.GlobeBridge.SnapshotAndResetCounters();
                    if (c.Telemetry == 0 && c.Notable == 0 && c.Commands == 0 && c.Errors == 0 && c.Pending == 0)
                    {
                        return;
                    }

                    LoggerService.Instance.LogInfo(
                        $"Globe telemetry (10s): telemetry={c.Telemetry} notable={c.Notable} commands={c.Commands} " +
                        $"errors={c.Errors} pending={c.Pending} dropped={LoggerService.Instance.DroppedEntryCount}");
                }
                catch
                {
                    // Telemetry reporting must never fault the timer
                }
            };
            _telemetrySummaryTimer.Start();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is PlaceItem item)
            {
                ViewModel.SelectedPlace = item;
            }
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedPlace != null)
            {
                ViewModel.FlyToPlaceCommand.Execute(ViewModel.SelectedPlace);
            }
        }

        public void SidebarPlacesTab_Click(object? sender = null, RoutedEventArgs? e = null)
        {
            ViewModel.SelectedSidebarTab = 0;
        }

        public void SidebarLayersTab_Click(object? sender = null, RoutedEventArgs? e = null)
        {
            ViewModel.SelectedSidebarTab = 1;
        }

        private void SearchModeByName_Click(object sender, RoutedEventArgs e)
        {
            if (SearchByNamePanel != null && SearchByCoordsPanel != null)
            {
                SearchByNamePanel.Visibility = Visibility.Visible;
                SearchByCoordsPanel.Visibility = Visibility.Collapsed;
                ViewModel.SelectedSearchMode = 0;
            }
        }

        private void SearchModeByCoordinates_Click(object sender, RoutedEventArgs e)
        {
            if (SearchByNamePanel != null && SearchByCoordsPanel != null)
            {
                SearchByNamePanel.Visibility = Visibility.Collapsed;
                SearchByCoordsPanel.Visibility = Visibility.Visible;
                ViewModel.SelectedSearchMode = 1;
            }
        }

        private void SearchByNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ViewModel.SearchByNameCommand.CanExecute(null))
                {
                    ViewModel.SearchByNameCommand.Execute(null);
                }
            }
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (source is CheckBox || source is Button) return;

            var curr = source;
            while (curr != null)
            {
                if (curr is CheckBox || curr is Button) return;
                curr = VisualTreeHelper.GetParent(curr);
            }

            while (source != null && source is not TreeViewItem)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            if (source is TreeViewItem tvi)
            {
                tvi.Focus();
                tvi.IsSelected = true;
                if (tvi.DataContext is PlaceItem item)
                {
                    ViewModel.SelectedPlace = item;
                }
            }
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            if (source is TreeViewItem tvi)
            {
                tvi.Focus();
                tvi.IsSelected = true;
                if (tvi.DataContext is PlaceItem item)
                {
                    ViewModel.SelectedPlace = item;
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ViewModel.SearchCommand.CanExecute(null))
                {
                    ViewModel.SearchCommand.Execute(null);
                }
            }
        }

        public async Task<System.Drawing.Bitmap> CaptureFullWindowBitmapAsync()
        {
            UpdateLayout();
            int width = (int)Math.Max(ActualWidth, 1380);
            int height = (int)Math.Max(ActualHeight, 850);

            // 1. Render complete WPF chrome, Ribbon, and Sidebars to RenderTargetBitmap
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(this);

            using var ms = new MemoryStream();
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            encoder.Save(ms);
            ms.Position = 0;
            var windowBmp = new System.Drawing.Bitmap(ms);

            // 2. Composite true WebView2 Cesium viewport into the window bitmap
            if (GlobeWebView?.CoreWebView2 != null)
            {
                try
                {
                    using var wvMs = new MemoryStream();
                    await GlobeWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, wvMs);
                    wvMs.Position = 0;
                    using var wvBmp = new System.Drawing.Bitmap(wvMs);

                    var pt = GlobeWebView.TransformToAncestor(this).Transform(new Point(0, 0));
                    int vx = (int)Math.Round(pt.X);
                    int vy = (int)Math.Round(pt.Y);
                    int vw = (int)Math.Round(GlobeWebView.ActualWidth);
                    int vh = (int)Math.Round(GlobeWebView.ActualHeight);

                    if (vw > 0 && vh > 0)
                    {
                        using var g = System.Drawing.Graphics.FromImage(windowBmp);
                        g.DrawImage(wvBmp, new System.Drawing.Rectangle(vx, vy, vw, vh));
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogWarning($"WebView2 CapturePreviewAsync composite notice: {ex.Message}");
                }
            }

            return windowBmp;
        }
    }
}
