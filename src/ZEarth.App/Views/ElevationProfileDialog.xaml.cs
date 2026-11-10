using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using ZEarth.App.Services;
using ZEarth.Domain;
using ZEarth.Domain.Measurement;
using ZEarth.Domain.Units;

namespace ZEarth.App.Views
{
    public record ProfileSample(double DistanceMeters, double ElevationMeters, double Longitude, double Latitude);

    public partial class ElevationProfileDialog : Window
    {
        public event Action<double, double>? SampleHovered;
        public event Action? SampleHoverCleared;

        private readonly List<ProfileSample> _samples = new();
        private Line? _hoverLine;
        private Ellipse? _hoverDot;
        private bool _isDocked;
        private Rect _floatingBounds;
        private double _exaggerationFactor = -1.0; // -1 for Auto-Fit

        public ElevationProfileDialog(IEnumerable<CoordinatePoint>? pathPoints = null)
        {
            InitializeComponent();
            LocalizationService.Instance.ApplyWindow(this);
            DockToggleButton.Content = LocalizationService.Instance.IsArabic ? "📌 تثبيت بالأسفل" : "📌 Dock to Bottom";
            Closed += (_, _) =>
            {
                SampleHoverCleared?.Invoke();
                DetachMainWindowEvents();
            };
            GenerateProfileSamples(pathPoints);
            ComputeStatisticsAndDraw();
        }

        /// <summary>Measured samples supplied by the active terrain provider through WebView2.</summary>
        public ElevationProfileDialog(IEnumerable<ProfileSample> measuredSamples, string provenance)
        {
            InitializeComponent();
            LocalizationService.Instance.ApplyWindow(this);
            DockToggleButton.Content = LocalizationService.Instance.IsArabic ? "📌 تثبيت بالأسفل" : "📌 Dock to Bottom";
            Closed += (_, _) =>
            {
                SampleHoverCleared?.Invoke();
                DetachMainWindowEvents();
            };
            var values = measuredSamples?.ToList() ?? new List<ProfileSample>();
            if (values.Count >= 2 && values.All(p =>
                double.IsFinite(p.DistanceMeters) && double.IsFinite(p.ElevationMeters) &&
                double.IsFinite(p.Longitude) && double.IsFinite(p.Latitude)))
            {
                _samples.AddRange(values);
            }
            ProfileTitleText.Text = LocalizationService.Instance.IsArabic ? "الملف التضاريسي للارتفاعات (عينات DEM)" : "Terrain Elevation Profile (DEM samples)";
            ProfileSourceText.Text = provenance ?? (LocalizationService.Instance.IsArabic ? "مصدر ارتفاع DEM النشط؛ مرجع الارتفاع خاص بالمزود." : "Active DEM height source; height datum is provider-specific.");
            ComputeStatisticsAndDraw();
        }

        private void OnExaggerationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExaggerationCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "auto") _exaggerationFactor = -1.0;
                else if (double.TryParse(tag, NumberStyles.Any, CultureInfo.InvariantCulture, out var val)) _exaggerationFactor = val;
                DrawGraph();
            }
        }

        private void OnDockToggleClick(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return;

            if (!_isDocked)
            {
                _floatingBounds = new Rect(Left, Top, Width, Height);
                _isDocked = true;
                DockToggleButton.Content = LocalizationService.Instance.IsArabic ? "🗗 نافذة عائمة" : "🗗 Float Window";

                AttachMainWindowEvents(mainWindow);
                UpdateDockedPosition(mainWindow);
            }
            else
            {
                _isDocked = false;
                DetachMainWindowEvents();
                DockToggleButton.Content = LocalizationService.Instance.IsArabic ? "📌 تثبيت بالأسفل" : "📌 Dock to Bottom";
                if (_floatingBounds.Width > 0 && _floatingBounds.Height > 0)
                {
                    Left = _floatingBounds.Left;
                    Top = _floatingBounds.Top;
                    Width = _floatingBounds.Width;
                    Height = _floatingBounds.Height;
                }
                else
                {
                    Width = 760;
                    Height = 520;
                    WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
            }
        }

        private void AttachMainWindowEvents(Window mainWindow)
        {
            mainWindow.LocationChanged += OnMainWindowMovedOrResized;
            mainWindow.SizeChanged += OnMainWindowMovedOrResized;
        }

        private void DetachMainWindowEvents()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.LocationChanged -= OnMainWindowMovedOrResized;
                mainWindow.SizeChanged -= OnMainWindowMovedOrResized;
            }
        }

        private void OnMainWindowMovedOrResized(object? sender, EventArgs e)
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null) UpdateDockedPosition(mainWindow);
        }

        private void UpdateDockedPosition(Window mainWindow)
        {
            if (!_isDocked) return;
            try
            {
                var viewport = mainWindow.FindName("ViewportBorder") as FrameworkElement;
                if (viewport != null && viewport.IsVisible)
                {
                    Point screenPos = viewport.PointToScreen(new Point(0, 0));
                    double dockHeight = Math.Min(340, Math.Max(260, mainWindow.ActualHeight * 0.40));
                    Left = screenPos.X;
                    Top = screenPos.Y + viewport.ActualHeight - dockHeight;
                    Width = Math.Max(400, viewport.ActualWidth);
                    Height = dockHeight;
                    return;
                }
            }
            catch { }

            double fallbackHeight = Math.Min(340, Math.Max(260, mainWindow.ActualHeight * 0.40));
            double leftOffset = mainWindow.ActualWidth > 800 ? 320 : 0;
            Left = mainWindow.Left + leftOffset;
            Top = mainWindow.Top + mainWindow.ActualHeight - fallbackHeight - 34;
            Width = Math.Max(400, mainWindow.ActualWidth - leftOffset);
            Height = fallbackHeight;
        }

        private void GenerateProfileSamples(IEnumerable<CoordinatePoint>? rawPoints)
        {
            _samples.Clear();
            var list = rawPoints?.ToList() ?? new List<CoordinatePoint>();

            // Do NOT fabricate a mock Cairo terrain profile: invalid path means no data.
            if (list.Count < 2)
                return;

            // Real points: interpolate along segments
            double accumDist = 0.0;
            for (int i = 0; i < list.Count; i++)
            {
                var pt = list[i];
                if (i > 0)
                {
                    var prev = list[i - 1];
                    double segDist = GeodesicMeasurementService.ComputeDistanceMeters(prev.Longitude, prev.Latitude, pt.Longitude, pt.Latitude);
                    accumDist += segDist;
                }

                if (!double.IsFinite(pt.Altitude) ||
                    !double.IsFinite(pt.Longitude) || !double.IsFinite(pt.Latitude) ||
                    pt.Latitude < -90 || pt.Latitude > 90 || pt.Longitude < -180 || pt.Longitude > 180)
                {
                    _samples.Clear();
                    return;
                }
                // Use only stored vertex altitude. 0 and negative Z may be valid!
                // This is NOT sampled terrain; the chart explicitly says "Vertex Z".
                _samples.Add(new ProfileSample(accumDist, pt.Altitude, pt.Longitude, pt.Latitude));
            }
        }

        private void ComputeStatisticsAndDraw()
        {
            if (_samples.Count < 2)
            {
                ProfileTitleText.Text = "Elevation profile unavailable";
                ProfileSourceText.Text = "No complete elevation sample set is available.";
                StatDistanceText.Text = StatMinElevText.Text = StatMaxElevText.Text = "—";
                StatDeltaZText.Text = StatAvgSlopeText.Text = StatMaxSlopeText.Text = "—";
                HoverInfoText.Text = "No valid elevation samples. Check the active terrain source and selected path.";
                return;
            }

            double totalDist = _samples.Last().DistanceMeters;
            double minElev = _samples.Min(s => s.ElevationMeters);
            double maxElev = _samples.Max(s => s.ElevationMeters);
            double deltaZ = Math.Abs(_samples.Last().ElevationMeters - _samples.First().ElevationMeters);
            double avgSlope = totalDist > 0.1 ? (deltaZ / totalDist) * 100.0 : 0.0;

            double maxSlope = 0.0;
            for (int i = 0; i < _samples.Count - 1; i++)
            {
                double dDist = _samples[i + 1].DistanceMeters - _samples[i].DistanceMeters;
                double dElev = Math.Abs(_samples[i + 1].ElevationMeters - _samples[i].ElevationMeters);
                if (dDist > 1.0)
                {
                    double slope = (dElev / dDist) * 100.0;
                    if (slope > maxSlope) maxSlope = slope;
                }
            }

            StatDistanceText.Text = UnitService.Instance.FormatDistance(totalDist);
            StatMinElevText.Text = UnitService.Instance.FormatElevation(minElev);
            StatMaxElevText.Text = UnitService.Instance.FormatElevation(maxElev);
            StatDeltaZText.Text = UnitService.Instance.FormatElevation(deltaZ);
            StatAvgSlopeText.Text = $"{avgSlope:F1}%";
            StatMaxSlopeText.Text = $"{maxSlope:F1}%";

            DrawGraph();
        }

        private void OnGraphSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGraph();
        }

        private void DrawGraph()
        {
            if (GraphCanvas == null || _samples.Count < 2) return;
            GraphCanvas.Children.Clear();

            double w = GraphContainer.ActualWidth;
            double h = GraphContainer.ActualHeight;
            if (w < 40 || h < 40) return;

            double paddingLeft = 55;
            double paddingRight = 20;
            double paddingTop = 25;
            double paddingBottom = 35;

            double plotW = w - paddingLeft - paddingRight;
            double plotH = h - paddingTop - paddingBottom;
            if (plotW <= 0 || plotH <= 0) return;

            double minElev = _samples.Min(s => s.ElevationMeters);
            double maxElev = _samples.Max(s => s.ElevationMeters);
            double delta = maxElev - minElev;
            double chartMin, chartMax, chartRange;

            if (_exaggerationFactor <= 0) // Auto-Fit mode
            {
                double pad = delta > 0.001 ? Math.Max(0.15, delta * 0.15) : 1.0;
                chartMin = minElev - pad;
                chartMax = maxElev + pad;
                chartRange = chartMax - chartMin;
            }
            else
            {
                double midElev = (minElev + maxElev) / 2.0;
                double baseSpan = Math.Max(6.0, delta * 1.5) / _exaggerationFactor;
                chartMin = midElev - (baseSpan / 2.0);
                chartMax = midElev + (baseSpan / 2.0);
                chartRange = chartMax - chartMin;
            }

            double totalDist = _samples.Last().DistanceMeters;
            if (totalDist <= 0.01) totalDist = 1.0;

            // Background Grid Lines
            int yTicks = 4;
            for (int i = 0; i <= yTicks; i++)
            {
                double yVal = chartMin + (i * (chartRange / yTicks));
                double yPos = paddingTop + plotH - (i * (plotH / yTicks));

                var gridLine = new Line
                {
                    X1 = paddingLeft,
                    Y1 = yPos,
                    X2 = paddingLeft + plotW,
                    Y2 = yPos,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                GraphCanvas.Children.Add(gridLine);

                string tickFormat = chartRange < 1.0 ? "F2" : (chartRange < 10.0 ? "F1" : "F0");
                var yText = new TextBlock
                {
                    Text = $"{yVal.ToString(tickFormat, CultureInfo.InvariantCulture)} m",
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Canvas.SetLeft(yText, paddingLeft - 48);
                Canvas.SetTop(yText, yPos - 7);
                GraphCanvas.Children.Add(yText);
            }

            // X Axis ticks
            int xTicks = 5;
            for (int i = 0; i <= xTicks; i++)
            {
                double xDist = i * (totalDist / xTicks);
                double xPos = paddingLeft + (i * (plotW / xTicks));

                var xText = new TextBlock
                {
                    Text = UnitService.Instance.FormatDistance(xDist),
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                };
                Canvas.SetLeft(xText, xPos - 18);
                Canvas.SetTop(xText, paddingTop + plotH + 8);
                GraphCanvas.Children.Add(xText);
            }

            // Points mapping
            var pts = new PointCollection();
            var polyPts = new PointCollection();
            polyPts.Add(new Point(paddingLeft, paddingTop + plotH));

            foreach (var s in _samples)
            {
                double normX = s.DistanceMeters / totalDist;
                double normY = (s.ElevationMeters - chartMin) / chartRange;
                normY = Math.Clamp(normY, 0.0, 1.0);

                double px = paddingLeft + (normX * plotW);
                double py = paddingTop + plotH - (normY * plotH);

                var pt = new Point(px, py);
                pts.Add(pt);
                polyPts.Add(pt);
            }

            polyPts.Add(new Point(paddingLeft + plotW, paddingTop + plotH));

            // Filled Area under Curve
            var areaPolygon = new Polygon
            {
                Points = polyPts,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(100, 34, 168, 222), 0.0),
                        new GradientStop(Color.FromArgb(10, 34, 168, 222), 1.0)
                    }
                }
            };
            GraphCanvas.Children.Add(areaPolygon);

            // Curve Polyline
            var curveLine = new Polyline
            {
                Points = pts,
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 2.5
            };
            GraphCanvas.Children.Add(curveLine);

            // Hover indicator elements
            _hoverLine = new Line
            {
                Y1 = paddingTop,
                Y2 = paddingTop + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Visibility = Visibility.Collapsed
            };
            GraphCanvas.Children.Add(_hoverLine);

            _hoverDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };
            GraphCanvas.Children.Add(_hoverDot);
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_samples.Count < 2 || _hoverLine == null || _hoverDot == null) return;

            Point mousePos = e.GetPosition(GraphCanvas);
            double w = GraphContainer.ActualWidth;
            double h = GraphContainer.ActualHeight;

            double paddingLeft = 55;
            double paddingRight = 20;
            double paddingTop = 25;
            double paddingBottom = 35;
            double plotW = w - paddingLeft - paddingRight;
            double plotH = h - paddingTop - paddingBottom;

            if (mousePos.X < paddingLeft || mousePos.X > paddingLeft + plotW)
            {
                OnCanvasMouseLeave(sender, e);
                return;
            }

            double totalDist = _samples.Last().DistanceMeters;
            double fraction = (mousePos.X - paddingLeft) / plotW;
            double targetDist = fraction * totalDist;

            // Find closest sample
            var nearest = _samples.OrderBy(s => Math.Abs(s.DistanceMeters - targetDist)).First();
            double minElev = _samples.Min(s => s.ElevationMeters);
            double maxElev = _samples.Max(s => s.ElevationMeters);
            double elevRange = Math.Max(10.0, maxElev - minElev);

            double snapX = paddingLeft + ((nearest.DistanceMeters / totalDist) * plotW);
            double snapY = paddingTop + plotH - (((nearest.ElevationMeters - minElev) / elevRange) * plotH);

            _hoverLine.X1 = snapX;
            _hoverLine.X2 = snapX;
            _hoverLine.Visibility = Visibility.Visible;

            Canvas.SetLeft(_hoverDot, snapX - 4);
            Canvas.SetTop(_hoverDot, snapY - 4);
            _hoverDot.Visibility = Visibility.Visible;

            HoverInfoText.Text = $"Distance: {UnitService.Instance.FormatDistance(nearest.DistanceMeters)} | Elevation: {UnitService.Instance.FormatElevation(nearest.ElevationMeters)} | Coordinates: {nearest.Latitude:F5}° N, {nearest.Longitude:F5}° E";
            SampleHovered?.Invoke(nearest.Longitude, nearest.Latitude);
        }

        private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoverLine != null) _hoverLine.Visibility = Visibility.Collapsed;
            if (_hoverDot != null) _hoverDot.Visibility = Visibility.Collapsed;
            HoverInfoText.Text = "Hover over the profile chart to inspect exact distance, elevation, and coordinates.";
            SampleHoverCleared?.Invoke();
        }

        private void OnExportCsvClick(object sender, RoutedEventArgs e)
        {
            if (_samples.Count < 2)
            {
                MessageBox.Show("No valid path vertex heights to export.", "Elevation Profile",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Export Elevation Profile to CSV",
                FileName = $"Elevation_Profile_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Distance_Meters,Stored_Vertex_Z_Meters,Longitude,Latitude");
                foreach (var s in _samples)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2:F6},{3:F6}",
                        s.DistanceMeters, s.ElevationMeters, s.Longitude, s.Latitude));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Elevation profile successfully exported to:\n{dlg.FileName}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
