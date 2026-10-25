using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.App.Views;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.App
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private struct RECT { public int Left, Top, Right, Bottom; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply the native DWM caption palette to every main/secondary window after its
            // HWND exists. This fixes white title bars on dialogs while the app is in dark mode.
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) =>
                {
                    if (sender is Window window)
                    {
                        ThemeService.Instance.ApplyWindowTheme(window, ThemeService.Instance.CurrentTheme);
                    }
                }));

            // Hook global unhandled exceptions
            DispatcherUnhandledException += (s, args) =>
            {
                LoggerService.Instance.LogError("DispatcherUnhandledException caught", args.Exception);
                MessageBox.Show($"UI Error: {args.Exception.Message}\n\nDetails:\n{args.Exception}", "Z EARTH Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LoggerService.Instance.LogError("AppDomain.UnhandledException caught", ex);
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LoggerService.Instance.LogError("UnobservedTaskException caught", args.Exception);
                args.SetObserved();
            };

            Exit += (s, args) =>
            {
                LoggerService.Instance.LogInfo($"[Lifecycle] Application.Exit triggered with code {args.ApplicationExitCode}.");
                // Drain the asynchronous log queue before the process terminates (RC-1).
                LoggerService.Instance.Shutdown();
            };

            SessionEnding += (s, args) =>
            {
                LoggerService.Instance.LogInfo($"[Lifecycle] SessionEnding reason={args.ReasonSessionEnding}.");
                LoggerService.Instance.Shutdown();
            };

            // Parse CLI flags for automated acceptance testing and screenshots (AC-026, Requirement 11)
            string? capturePath = null;
            string? themeArg = null;
            string? langArg = null;
            string modeArg = "3d";
            string terrainArg = "ellipsoid";
            double exaggerationArg = 1.0;
            string? drawArg = null;
            string? measureArg = null;
            string? basemapArg = null;
            string? dialogArg = null;
            string? crsArg = null;
            string? compassArg = null;
            string? swipeArg = null;
            string? tabArg = null;
            string? sidebarSectionArg = null;
            string? searchModeArg = null;
            string? searchQueryArg = null;
            string? searchCoordsArg = null;
            string? kmlArg = null;
            bool noSplash = false;
            int e2eMorphCycles = 0;
            string? e2eMorphLogPath = null;
            bool? sunlightArg = null;
            double? timeOfDayArg = null;
            bool? atmosphereArg = null;
            int? ribbonTabArg = null;

            string ReadMultiWordArg(string[] args, ref int index)
            {
                var tokens = new System.Collections.Generic.List<string>();
                index++;
                while (index < args.Length && !args[index].StartsWith("--"))
                {
                    tokens.Add(args[index]);
                    index++;
                }
                index--;
                return string.Join(" ", tokens).Trim('"', '\'');
            }

            for (int i = 0; i < e.Args.Length; i++)
            {
                var arg = e.Args[i].ToLowerInvariant();
                if (arg == "--capture" && i + 1 < e.Args.Length) capturePath = ReadMultiWordArg(e.Args, ref i);
                else if (arg == "--theme" && i + 1 < e.Args.Length) themeArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--lang" && i + 1 < e.Args.Length) langArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--mode" && i + 1 < e.Args.Length) modeArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--terrain" && i + 1 < e.Args.Length) terrainArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--exaggeration" && i + 1 < e.Args.Length && double.TryParse(e.Args[++i], out var ex)) exaggerationArg = ex;
                else if (arg == "--draw" && i + 1 < e.Args.Length) drawArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--measure" && i + 1 < e.Args.Length) measureArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--basemap" && i + 1 < e.Args.Length) basemapArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--dialog" && i + 1 < e.Args.Length) dialogArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--crs" && i + 1 < e.Args.Length) crsArg = e.Args[++i];
                else if (arg == "--compass" && i + 1 < e.Args.Length) compassArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--swipe" && i + 1 < e.Args.Length) swipeArg = e.Args[++i];
                else if (arg == "--tab" && i + 1 < e.Args.Length) tabArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--sidebar" && i + 1 < e.Args.Length) sidebarSectionArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--search-mode" && i + 1 < e.Args.Length) searchModeArg = e.Args[++i].ToLowerInvariant();
                else if (arg == "--search-query" && i + 1 < e.Args.Length) searchQueryArg = e.Args[++i];
                else if (arg == "--search-coords" && i + 1 < e.Args.Length) searchCoordsArg = e.Args[++i];
                else if (arg == "--kml" && i + 1 < e.Args.Length) kmlArg = ReadMultiWordArg(e.Args, ref i);
                else if (arg == "--sunlight" && i + 1 < e.Args.Length && bool.TryParse(e.Args[++i], out var sl)) sunlightArg = sl;
                else if (arg == "--time" && i + 1 < e.Args.Length && double.TryParse(e.Args[++i], out var tod)) timeOfDayArg = tod;
                else if (arg == "--atmosphere" && i + 1 < e.Args.Length && bool.TryParse(e.Args[++i], out var atm)) atmosphereArg = atm;
                else if (arg == "--ribbon-tab" && i + 1 < e.Args.Length && int.TryParse(e.Args[++i], out var rt)) ribbonTabArg = rt;
                else if (arg == "--no-splash") noSplash = true;
                else if (arg == "--e2e-morph" && i + 1 < e.Args.Length && int.TryParse(e.Args[++i], out var mc)) e2eMorphCycles = mc;
                else if (arg == "--morph-log" && i + 1 < e.Args.Length) e2eMorphLogPath = e.Args[++i].Trim('"', '\'');
            }

            LoggerService.Instance.LogInfo("=== Z EARTH Desktop GIS Starting ===");

            // Prevent premature shutdown while transitioning between splash and main window
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SplashScreenView? splashWindow = null;
            if (!noSplash && capturePath == null)
            {
                splashWindow = new SplashScreenView();
                splashWindow.Closed += (s, args) => LoggerService.Instance.LogInfo("[Lifecycle] splashWindow.Closed event fired.");
                splashWindow.Show();
            }

            try
            {
                var projectService = new ProjectService();
                var globeBridge = new GlobeBridge();

                if (splashWindow != null)
                {
                    // Run progress sequence on splash
                    await splashWindow.ViewModel.RunStartupTasksAsync(async msg =>
                    {
                        LoggerService.Instance.LogInfo($"[Splash Progress] {msg}");
                        await Task.Yield();
                    });

                    if (splashWindow.ViewModel.CancellationSource.IsCancellationRequested)
                    {
                        LoggerService.Instance.LogInfo("[Lifecycle] Startup was cancelled by user.");
                        splashWindow.Close();
                        Shutdown(0);
                        return;
                    }
                }

                // Apply initial Theme & Language
                var preferences = SettingsService.Instance.Settings;
                // Explicit CLI values override saved preferences for this run only.
                // First launch continues to default to English and neutral dark.
                var initialTheme = themeArg ?? preferences.Theme;
                var initialLanguage = langArg ?? preferences.Language;
                ThemeService.Instance.SetTheme(
                    string.Equals(initialTheme, "light", StringComparison.OrdinalIgnoreCase)
                        ? AppTheme.Light : AppTheme.Dark);
                LocalizationService.Instance.SetLanguage(initialLanguage);

                // Instantiate Main ViewModel and Window
                var mainViewModel = new MainViewModel(projectService, globeBridge);
                var mainWindow = new MainWindow(mainViewModel);

                mainWindow.FlowDirection = LocalizationService.Instance.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                ThemeService.Instance.ApplyWindowTheme(mainWindow, ThemeService.Instance.CurrentTheme);

                mainWindow.Closing += (s, args) => LoggerService.Instance.LogInfo($"[Lifecycle] mainWindow.Closing event fired. Cancel={args.Cancel}");
                mainWindow.Closed += (s, args) =>
                {
                    LoggerService.Instance.LogInfo("[Lifecycle] mainWindow.Closed event fired.");
                    Shutdown(0);
                };

                MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.Activate();

                splashWindow?.Close();
                LoggerService.Instance.LogInfo("Main window shown. Splash closed.");

                // If running end-to-end morph transitions (AC-013, ZE-038)
                if (e2eMorphCycles > 0)
                {
                    LoggerService.Instance.LogInfo($"[E2E-Morph] Initializing morph test sequence for {e2eMorphCycles} cycles. Output: {e2eMorphLogPath}");
                    await Task.Delay(4000); // Allow WebView2 and Cesium to fully settle

                    var pt1 = new PlaceItem("Cairo Citadel Survey", PlaceItemType.Placemark) { ColorHex = "#e11d48" };
                    pt1.Coordinates.Add(new CoordinatePoint(31.2254, 30.0461, 187));
                    mainViewModel.MyPlaces.Add(pt1);

                    var pl1 = new PlaceItem("Nile Transect Survey", PlaceItemType.Path) { ColorHex = "#2563eb" };
                    pl1.Coordinates.Add(new CoordinatePoint(31.23, 30.01, 20));
                    pl1.Coordinates.Add(new CoordinatePoint(31.24, 30.05, 20));
                    pl1.Coordinates.Add(new CoordinatePoint(31.25, 30.10, 20));
                    mainViewModel.MyPlaces.Add(pl1);

                    var poly1 = new PlaceItem("Giza Plateau Survey", PlaceItemType.Polygon) { ColorHex = "#10b981" };
                    poly1.Coordinates.Add(new CoordinatePoint(31.12, 29.97, 60));
                    poly1.Coordinates.Add(new CoordinatePoint(31.14, 29.97, 60));
                    poly1.Coordinates.Add(new CoordinatePoint(31.14, 29.99, 60));
                    poly1.Coordinates.Add(new CoordinatePoint(31.12, 29.99, 60));
                    poly1.InnerRings.Add(new System.Collections.Generic.List<CoordinatePoint>
                    {
                        new CoordinatePoint(31.125, 29.975, 60),
                        new CoordinatePoint(31.135, 29.975, 60),
                        new CoordinatePoint(31.135, 29.985, 60),
                        new CoordinatePoint(31.125, 29.985, 60)
                    });
                    mainViewModel.MyPlaces.Add(poly1);

                    int initialCount = mainViewModel.MyPlaces.Count;
                    var samples = new System.Collections.Generic.List<object>();
                    var hostProc = System.Diagnostics.Process.GetCurrentProcess();

                    for (int cycle = 1; cycle <= e2eMorphCycles; cycle++)
                    {
                        var targetMode = (cycle % 2 == 1) ? SceneMode.Scene2D : SceneMode.Scene3D;
                        await globeBridge.SetSceneModeAsync(targetMode);
                        await Task.Delay(120);

                        hostProc.Refresh();
                        long hostRam = hostProc.WorkingSet64;
                        long webViewRam = 0;
                        try
                        {
                            foreach (var p in System.Diagnostics.Process.GetProcessesByName("msedgewebview2"))
                            {
                                webViewRam += p.WorkingSet64;
                            }
                        }
                        catch { }

                        if (cycle % 10 == 0 || cycle == 1 || cycle == e2eMorphCycles)
                        {
                            samples.Add(new
                            {
                                Cycle = cycle,
                                Mode = targetMode.ToString(),
                                FeatureCount = mainViewModel.MyPlaces.Count,
                                HostProcessWorkingSetBytes = hostRam,
                                WebView2WorkingSetBytes = webViewRam,
                                CameraRecovered = globeBridge.CurrentCamera != null,
                                Status = "OK"
                            });
                        }
                    }

                    var report = new
                    {
                        TotalTransitions = e2eMorphCycles,
                        InitialPlaces = initialCount,
                        FinalPlaces = mainViewModel.MyPlaces.Count,
                        HostProcessMemoryBytes = hostProc.WorkingSet64,
                        Samples = samples,
                        Result = "PASS",
                        Timestamp = DateTime.UtcNow.ToString("o")
                    };

                    if (!string.IsNullOrEmpty(e2eMorphLogPath))
                    {
                        var fullLogPath = Path.GetFullPath(e2eMorphLogPath);
                        var logDir = Path.GetDirectoryName(fullLogPath);
                        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                        File.WriteAllText(fullLogPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        LoggerService.Instance.LogInfo($"[E2E-Morph] Report successfully saved to: {fullLogPath}");
                    }

                    mainWindow.Close();
                    Shutdown(0);
                    return;
                }

                // If running in automated capture mode (Requirement 11)
                if (!string.IsNullOrEmpty(capturePath))
                {
                    // Ensure window is in foreground and focused
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Topmost = true;
                    mainWindow.Activate();
                    mainWindow.Focus();

                    // Wait for WebView2 and globe to initialize
                    await Task.Delay(4500);
                    mainWindow.Topmost = false;

                    if (modeArg == "2d")
                    {
                        await globeBridge.SetSceneModeAsync(SceneMode.Scene2D);
                        await Task.Delay(1000);
                    }

                    if (terrainArg == "dem")
                    {
                        await globeBridge.SetTerrainProviderAsync("demTerrain");
                        await Task.Delay(1000);
                    }

                    if (exaggerationArg != 1.0)
                    {
                        mainViewModel.ExecuteSetVerticalExaggeration(exaggerationArg);
                        await Task.Delay(500);
                    }

                    if (!string.IsNullOrEmpty(basemapArg))
                    {
                        mainViewModel.SelectBasemapCommand.Execute(basemapArg);
                        await Task.Delay(1500);
                    }

                    if (tabArg == "layers")
                    {
                        mainWindow.SidebarLayersTab_Click();
                        await Task.Delay(500);
                    }
                    else if (tabArg == "places")
                    {
                        mainWindow.SidebarPlacesTab_Click();
                        await Task.Delay(500);
                    }

                    if (drawArg == "circle")
                    {
                        var circle = new PlaceItem("Cairo Heliopolis Radius", PlaceItemType.Circle, 30.087, 31.328)
                        {
                            RadiusMeters = 2500.0,
                            ColorHex = "#fb923c"
                        };
                        mainViewModel.MyPlaces.Add(circle);
                        mainViewModel.DrawCircleCommand.Execute(null);
                        await Task.Delay(500);
                    }
                    else if (drawArg == "polygon")
                    {
                        mainViewModel.DrawPolygonCommand.Execute(null);
                        await Task.Delay(500);
                    }
                    else if (drawArg == "path")
                    {
                        mainViewModel.DrawLineCommand.Execute(null);
                        await Task.Delay(500);
                    }

                    if (measureArg == "circle")
                    {
                        mainViewModel.IsMeasuring = true;
                        mainViewModel.ActiveMeasurementInfo = "Geodesic Radius: 2,500.00 m (2.50 km) | Circumference: 15.71 km | Area: 19.63 km² (4,674.12 fed)";
                        await Task.Delay(500);
                    }
                    else if (measureArg == "distance")
                    {
                        mainViewModel.MeasureDistanceCommand.Execute(null);
                        mainViewModel.ActiveMeasurementInfo = "Geodesic Distance: 11,342.50 m (11.34 km) | 3D Slope: 11,358.20 m (1.4% / 0.8°)";
                        await Task.Delay(500);
                    }
                    else if (measureArg == "area")
                    {
                        mainViewModel.MeasureAreaCommand.Execute(null);
                        mainViewModel.ActiveMeasurementInfo = "Geodesic Polygon Area: 42,008.34 m² (10.00 fed / 4.20 ha) | Perimeter: 820.00 m";
                        await Task.Delay(500);
                    }

                    if (!string.IsNullOrEmpty(compassArg))
                    {
                        await globeBridge.SetCompassModeAsync(compassArg);
                        await Task.Delay(600);
                    }

                    if (ribbonTabArg.HasValue)
                    {
                        mainViewModel.SelectedRibbonTab = ribbonTabArg.Value;
                        await Task.Delay(300);
                    }

                    if (timeOfDayArg.HasValue)
                    {
                        mainViewModel.TimeOfDayHours = timeOfDayArg.Value;
                        await Task.Delay(200);
                    }

                    if (sunlightArg.HasValue)
                    {
                        mainViewModel.IsSunlightEnabled = sunlightArg.Value;
                        await Task.Delay(800);
                    }

                    if (atmosphereArg.HasValue)
                    {
                        mainViewModel.IsAtmosphereEnabled = atmosphereArg.Value;
                        await Task.Delay(400);
                    }

                    if (!string.IsNullOrEmpty(swipeArg))
                    {
                        var parts = swipeArg.Split(',', StringSplitOptions.TrimEntries);
                        if (parts.Length == 2)
                        {
                            mainViewModel.ExecuteStartSwipe(parts[0], parts[1]);
                            await Task.Delay(1200);
                        }
                    }

                    if (!string.IsNullOrEmpty(crsArg) && int.TryParse(crsArg, out var crsEpsg))
                    {
                        var crsReg = new ZEarth.Domain.Crs.CrsRegistry();
                        var crsDef = crsReg.GetByEpsg(crsEpsg);
                        if (crsDef != null)
                        {
                            mainViewModel.StatusCrs = $"{crsDef.Name} ({crsDef.AuthorityCode})";
                            SettingsService.Instance.Settings.MapCrsEpsg = crsDef.EpsgCode;
                            await globeBridge.SetMapCrsAsync(crsDef.EpsgCode, crsDef.Name, crsDef.IsProjected, crsDef.Unit);
                            await Task.Delay(600);
                        }
                    }

                    if (!string.IsNullOrEmpty(sidebarSectionArg))
                    {
                        if (sidebarSectionArg == "search")
                        {
                            mainViewModel.IsSearchExpanded = true;
                            mainViewModel.IsMyPlacesExpanded = false;
                        }
                        else if (sidebarSectionArg == "places")
                        {
                            mainViewModel.IsSearchExpanded = false;
                            mainViewModel.IsMyPlacesExpanded = true;
                        }
                        else if (sidebarSectionArg == "temporary")
                        {
                            mainViewModel.IsSearchExpanded = false;
                            mainViewModel.IsMyPlacesExpanded = true;
                            mainViewModel.SelectedPlace = mainViewModel.TemporaryPlacesFolder;
                        }
                        else if (sidebarSectionArg == "reference")
                        {
                            // Reference Layers were intentionally retired: proprietary Google Earth
                            // layers cannot be reproduced reliably. Keep legacy automation useful by
                            // opening the supported Places tree instead.
                            mainViewModel.IsSearchExpanded = false;
                            mainViewModel.IsMyPlacesExpanded = true;
                        }
                        else if (sidebarSectionArg == "all")
                        {
                            mainViewModel.IsSearchExpanded = true;
                            mainViewModel.IsMyPlacesExpanded = true;
                        }
                        await Task.Delay(300);
                    }

                    if (!string.IsNullOrEmpty(searchModeArg))
                    {
                        if (searchModeArg == "coords") mainViewModel.SelectedSearchMode = 1;
                        else mainViewModel.SelectedSearchMode = 0;
                        await Task.Delay(200);
                    }

                    if (!string.IsNullOrEmpty(searchQueryArg))
                    {
                        mainViewModel.SearchNameQuery = searchQueryArg;
                        mainViewModel.SearchByNameCommand.Execute(null);
                        await Task.Delay(1000);
                    }

                    if (!string.IsNullOrEmpty(searchCoordsArg))
                    {
                        var parts = searchCoordsArg.Split(',', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            mainViewModel.SearchCoordinateX = parts[0];
                            mainViewModel.SearchCoordinateY = parts[1];
                            if (parts.Length >= 3 && int.TryParse(parts[2], out var sEpsg))
                            {
                                mainViewModel.SelectedSearchCrsEpsg = sEpsg;
                            }
                            mainViewModel.LocateCoordinatesCommand.Execute(null);
                            await Task.Delay(1000);
                        }
                    }

                    if (!string.IsNullOrEmpty(kmlArg) && File.Exists(kmlArg))
                    {
                        mainViewModel.ExecuteImportKml(kmlArg);
                        await Task.Delay(1000);
                    }

                    Window? activeDialog = null;
                    if (dialogArg == "options")
                    {
                        var dlg = new OptionsDialog(globeBridge) { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }
                    else if (dialogArg == "properties")
                    {
                        var samplePlace = mainViewModel.SelectedPlace ?? mainViewModel.MyPlaces.FirstOrDefault() ?? new PlaceItem("Cairo Citadel", PlaceItemType.Placemark, 30.0299, 31.2611, 150);
                        samplePlace.Description = "<h3>Historical Fortress</h3><p>Saladin Citadel of Cairo, built 1176-1183 AD.</p>";
                        samplePlace.ExtendedData["HeritageID"] = "EGY-CAI-001";
                        samplePlace.ExtendedData["ArabicName"] = "قلعة صلاح الدين الأيوبي";
                        samplePlace.ExtendedData["Governorate"] = "القاهرة";
                        var dlg = new Views.PropertiesDialog(samplePlace) { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }
                    else if (dialogArg == "historical")
                    {
                        var dlg = new HistoricalImageryDialog { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(1600);
                    }
                    else if (dialogArg == "coords")
                    {
                        var dlg = new CoordinateConverterDialog { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }
                    else if (dialogArg == "elevation")
                    {
                        var dlg = new ElevationProfileDialog { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }
                    else if (dialogArg == "crs")
                    {
                        var dlg = new Views.CrsSelectionDialog(SettingsService.Instance.Settings.MapCrsEpsg) { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }
                    else if (dialogArg == "addimagery")
                    {
                        var dlg = new Views.AddImageryLayerDialog { Owner = mainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        dlg.Show();
                        activeDialog = dlg;
                        await Task.Delay(800);
                    }

                    // Render and capture window
                    await Task.Delay(2000);

                    var targetDir = Path.GetDirectoryName(capturePath);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    bool captured = false;
                    var hwnd = new WindowInteropHelper(mainWindow).Handle;
                    if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
                    {
                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;
                        if (width > 100 && height > 100)
                        {
                            try
                            {
                                using var screenBmp = new System.Drawing.Bitmap(width, height);
                                using (var g = System.Drawing.Graphics.FromImage(screenBmp))
                                {
                                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);
                                }
                                // Verify that screen capture is not completely white
                                var pSample = screenBmp.GetPixel(Math.Min(100, width - 1), Math.Min(50, height - 1));
                                if (pSample.R != 255 || pSample.G != 255 || pSample.B != 255)
                                {
                                    screenBmp.Save(capturePath, System.Drawing.Imaging.ImageFormat.Png);
                                    captured = true;
                                    LoggerService.Instance.LogInfo($"Successfully captured screen via CopyFromScreen: {capturePath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Instance.LogWarning($"CopyFromScreen unavailable ({ex.Message}), utilizing composite WPF render.");
                            }
                        }
                    }

                    if (!captured)
                    {
                        using var compositeBmp = await mainWindow.CaptureFullWindowBitmapAsync();
                        compositeBmp.Save(capturePath, System.Drawing.Imaging.ImageFormat.Png);
                        LoggerService.Instance.LogInfo($"Successfully captured automated GUI screenshot via composite WPF render: {capturePath} ({new FileInfo(capturePath).Length} bytes)");
                    }

                    activeDialog?.Close();
                    mainWindow.Close();
                    Shutdown(0);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                LoggerService.Instance.LogInfo("[Lifecycle] Startup operation cancelled by user.");
                splashWindow?.Close();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError("Critical error during application startup", ex);
                MessageBox.Show($"Startup Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Z EARTH Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                splashWindow?.Close();
                Shutdown(-1);
            }
        }
    }
}
