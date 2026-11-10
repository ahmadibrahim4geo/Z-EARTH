using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZEarth.App.Services;

namespace ZEarth.App.Views
{
    /// <summary>
    /// Compact, dependency-free colour picker used by the Map Grid settings.
    /// The stored value is always an opaque #RRGGBB string so it serializes
    /// cleanly and converts deterministically to a Cesium rgba() colour.
    /// </summary>
    public partial class ColorPickerButton : UserControl
    {
        public static readonly DependencyProperty ColorHexProperty = DependencyProperty.Register(
            nameof(ColorHex),
            typeof(string),
            typeof(ColorPickerButton),
            new FrameworkPropertyMetadata(
                "#FFFFFF",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnColorHexChanged));

        private static readonly string[] Palette =
        {
            // Row 1: Vivid Primary & Vibrant Accents
            "#EF4444", "#F97316", "#F59E0B", "#FACC15", "#84CC16", "#10B981", "#06B6D4", "#3B82F6",
            // Row 2: Deep Saturated Shades
            "#DC2626", "#EA580C", "#D97706", "#CA8A04", "#65A30D", "#059669", "#0891B2", "#2563EB",
            // Row 3: Violet, Purple & Magenta Spectrum
            "#6366F1", "#8B5CF6", "#A855F7", "#D946EF", "#EC4899", "#F43F5E", "#E11D48", "#9333EA",
            // Row 4: Pastels & Light Tones
            "#FCA5A5", "#FDBA74", "#FDE047", "#86EFAC", "#67E8F9", "#93C5FD", "#C4B5FD", "#F9A8D4",
            // Row 5: Earth, Terrain & Surveying Tones
            "#78350F", "#92400E", "#B45309", "#854D0E", "#3F6212", "#14532D", "#155E75", "#1E3A8A",
            // Row 6: Monochrome & Grayscale Spectrum
            "#FFFFFF", "#F3F4F6", "#E5E7EB", "#D1D5DB", "#9CA3AF", "#6B7280", "#374151", "#000000"
        };

        public ColorPickerButton()
        {
            InitializeComponent();
            BuildPalette();
            UpdatePreview();
        }

        public string ColorHex
        {
            get => (string)GetValue(ColorHexProperty);
            set => SetValue(ColorHexProperty, value);
        }

        public event EventHandler? ColorChanged;

        private static void OnColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerButton picker) picker.UpdatePreview();
        }

        private void BuildPalette()
        {
            foreach (var hex in Palette)
            {
                var button = new Button
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(ParseColor(hex)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                    BorderThickness = new Thickness(1),
                    ToolTip = hex,
                    Tag = hex,
                    Cursor = Cursors.Hand
                };
                button.Click += (_, _) => Commit(hex);
                SwatchGrid.Children.Add(button);
            }
        }

        private void UpdatePreview()
        {
            if (SwatchBorder == null) return;
            if (GridColor.TryNormalize(ColorHex, out var normalized))
            {
                SwatchBorder.Background = new SolidColorBrush(ParseColor(normalized));
                if (ValidationText != null) ValidationText.Visibility = Visibility.Collapsed;
            }
            else
            {
                SwatchBorder.Background = Brushes.Transparent;
                if (ValidationText != null)
                {
                    ValidationText.Text = "Use #RRGGBB or #AARRGGBB.";
                    ValidationText.Visibility = Visibility.Visible;
                }
            }
            if (HexInput != null && !HexInput.IsKeyboardFocusWithin)
                HexInput.Text = ColorHex;
        }

        private static Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (FormatException)
            {
                return Colors.White;
            }
        }

        private void Commit(string hex)
        {
            if (!GridColor.TryNormalize(hex, out var normalized)) return;
            ColorHex = normalized;
            HexInput.Text = normalized;
            ValidationText.Visibility = Visibility.Collapsed;
            DropDownToggle.IsChecked = false;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyTypedValue()
        {
            if (GridColor.TryNormalize(HexInput.Text, out var normalized))
            {
                Commit(normalized);
            }
            else
            {
                ValidationText.Text = "Invalid colour. Use #RRGGBB or #AARRGGBB.";
                ValidationText.Visibility = Visibility.Visible;
                HexInput.Focus();
                HexInput.SelectAll();
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e) => ApplyTypedValue();

        private void OnHexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyTypedValue();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DropDownToggle.IsChecked = false;
                e.Handled = true;
            }
        }
    }
}
