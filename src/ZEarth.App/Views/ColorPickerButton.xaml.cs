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
            "#FF3B30", "#FF9500", "#FFD60A", "#34C759", "#00C7BE", "#32ADE6",
            "#1E90FF", "#5E5CE6", "#AF52DE", "#FF00FF", "#FF2D55", "#A2845E",
            "#FFFFFF", "#D1D5DB", "#9CA3AF", "#6B7280", "#374151", "#111827",
            "#F97316", "#84CC16", "#06B6D4", "#3B82F6", "#8B5CF6", "#EC4899"
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
                    Width = 26,
                    Height = 26,
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
