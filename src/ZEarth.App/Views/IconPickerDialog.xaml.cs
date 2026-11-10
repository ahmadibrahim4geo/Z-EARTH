using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZEarth.App.Services;

namespace ZEarth.App.Views
{
    public partial class IconPickerDialog : Window
    {
        public class SymbolEntry
        {
            public string Key { get; set; } = string.Empty;
            public string Glyph { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string DisplayNameAr { get; set; } = string.Empty;
        }

        private static readonly List<SymbolEntry> AvailableSymbols = new()
        {
            // Core shapes
            new SymbolEntry { Key = "Pin", Glyph = "📍", DisplayName = "Pin", DisplayNameAr = "دبوس" },
            new SymbolEntry { Key = "Circle", Glyph = "●", DisplayName = "Circle", DisplayNameAr = "دائرة" },
            new SymbolEntry { Key = "Square", Glyph = "■", DisplayName = "Square", DisplayNameAr = "مربع" },
            new SymbolEntry { Key = "Triangle", Glyph = "▲", DisplayName = "Triangle", DisplayNameAr = "مثلث" },
            new SymbolEntry { Key = "Star", Glyph = "★", DisplayName = "Star", DisplayNameAr = "نجمة" },
            new SymbolEntry { Key = "Diamond", Glyph = "◆", DisplayName = "Diamond", DisplayNameAr = "معين" },
            new SymbolEntry { Key = "Target", Glyph = "🎯", DisplayName = "Target", DisplayNameAr = "هدف" },
            new SymbolEntry { Key = "Flag", Glyph = "🚩", DisplayName = "Flag", DisplayNameAr = "راية" },

            // Places & Landmarks
            new SymbolEntry { Key = "Home", Glyph = "🏠", DisplayName = "Home", DisplayNameAr = "سكن" },
            new SymbolEntry { Key = "Building", Glyph = "🏢", DisplayName = "Building", DisplayNameAr = "مبنى" },
            new SymbolEntry { Key = "Hotel", Glyph = "🏨", DisplayName = "Hotel", DisplayNameAr = "فندق" },
            new SymbolEntry { Key = "Hospital", Glyph = "🏥", DisplayName = "Hospital", DisplayNameAr = "مستشفى" },
            new SymbolEntry { Key = "School", Glyph = "🏫", DisplayName = "School", DisplayNameAr = "مدرسة" },
            new SymbolEntry { Key = "Bank", Glyph = "🏦", DisplayName = "Bank", DisplayNameAr = "مصرف" },
            new SymbolEntry { Key = "Mosque", Glyph = "🕌", DisplayName = "Mosque", DisplayNameAr = "مسجد" },

            // Transport & Services
            new SymbolEntry { Key = "Airport", Glyph = "✈", DisplayName = "Airport", DisplayNameAr = "مطار" },
            new SymbolEntry { Key = "Port", Glyph = "⚓", DisplayName = "Port", DisplayNameAr = "ميناء" },
            new SymbolEntry { Key = "Train", Glyph = "🚆", DisplayName = "Train", DisplayNameAr = "قطار" },
            new SymbolEntry { Key = "Bus", Glyph = "🚌", DisplayName = "Bus", DisplayNameAr = "حافلة" },
            new SymbolEntry { Key = "Fuel", Glyph = "⛽", DisplayName = "Fuel", DisplayNameAr = "محطة وقود" },
            new SymbolEntry { Key = "Restaurant", Glyph = "🍴", DisplayName = "Restaurant", DisplayNameAr = "مطعم" },
            new SymbolEntry { Key = "Shopping", Glyph = "🛒", DisplayName = "Store", DisplayNameAr = "متجر" },

            // Environment & Outdoor
            new SymbolEntry { Key = "Tree", Glyph = "🌲", DisplayName = "Tree", DisplayNameAr = "شجرة" },
            new SymbolEntry { Key = "Mountain", Glyph = "⛰", DisplayName = "Mountain", DisplayNameAr = "جبل" },
            new SymbolEntry { Key = "Water", Glyph = "💧", DisplayName = "Water", DisplayNameAr = "مياه" },
            new SymbolEntry { Key = "Camera", Glyph = "📷", DisplayName = "Viewpoint", DisplayNameAr = "نقطة تصوير" },
            new SymbolEntry { Key = "Warning", Glyph = "⚠", DisplayName = "Warning", DisplayNameAr = "تنبيه" },
            new SymbolEntry { Key = "Info", Glyph = "ℹ", DisplayName = "Information", DisplayNameAr = "معلومات" },

            // Infrastructure, Utilities & Geodesy
            new SymbolEntry { Key = "Benchmark", Glyph = "📐", DisplayName = "Survey Benchmark", DisplayNameAr = "نقطة مساحية" },
            new SymbolEntry { Key = "GpsStation", Glyph = "🛰", DisplayName = "GPS/GNSS Station", DisplayNameAr = "محطة رصد أقمار صناعية" },
            new SymbolEntry { Key = "Tower", Glyph = "🗼", DisplayName = "Transmission Tower", DisplayNameAr = "برج نقل كهرباء/اتصالات" },
            new SymbolEntry { Key = "Telecom", Glyph = "📶", DisplayName = "Telecom Antenna", DisplayNameAr = "هوائي شبكة اتصالات" },
            new SymbolEntry { Key = "Valve", Glyph = "🔧", DisplayName = "Pipeline Valve", DisplayNameAr = "محبس خط أنابيب" },
            new SymbolEntry { Key = "Hydrant", Glyph = "🚰", DisplayName = "Water Hydrant", DisplayNameAr = "محبس مياه/حنفية حريق" },
            new SymbolEntry { Key = "Electricity", Glyph = "⚡", DisplayName = "Power Substation", DisplayNameAr = "محطة تحويل كهرباء" },
            new SymbolEntry { Key = "Manhole", Glyph = "🕳", DisplayName = "Utility Manhole", DisplayNameAr = "غرفة تفتيش / بالوعة" },
            new SymbolEntry { Key = "OilTank", Glyph = "🛢", DisplayName = "Storage Tank", DisplayNameAr = "خزان وقود / بترول" },
            new SymbolEntry { Key = "Factory", Glyph = "🏭", DisplayName = "Industrial Plant", DisplayNameAr = "منشأة صناعية / مصنع" },
            new SymbolEntry { Key = "Crane", Glyph = "🏗", DisplayName = "Construction Site", DisplayNameAr = "موقع إنشاءات / رافعة" },
            new SymbolEntry { Key = "ReticleTarget", Glyph = "⊕", DisplayName = "Target Reticle", DisplayNameAr = "علامة تنشين بؤرية" },

            // Letters & Numbers
            new SymbolEntry { Key = "Num1", Glyph = "①", DisplayName = "Number 1", DisplayNameAr = "رقم 1" },
            new SymbolEntry { Key = "Num2", Glyph = "②", DisplayName = "Number 2", DisplayNameAr = "رقم 2" },
            new SymbolEntry { Key = "Num3", Glyph = "③", DisplayName = "Number 3", DisplayNameAr = "رقم 3" },
            new SymbolEntry { Key = "Num4", Glyph = "④", DisplayName = "Number 4", DisplayNameAr = "رقم 4" },
            new SymbolEntry { Key = "Num5", Glyph = "⑤", DisplayName = "Number 5", DisplayNameAr = "رقم 5" },
            new SymbolEntry { Key = "CharA", Glyph = "Ⓐ", DisplayName = "Letter A", DisplayNameAr = "حرف A" },
            new SymbolEntry { Key = "CharB", Glyph = "Ⓑ", DisplayName = "Letter B", DisplayNameAr = "حرف B" },
            new SymbolEntry { Key = "CharC", Glyph = "Ⓒ", DisplayName = "Letter C", DisplayNameAr = "حرف C" }
        };

        public string SelectedSymbolKey { get; private set; } = "Pin";
        public double SelectedScale { get; private set; } = 1.0;
        public double SelectedOpacity { get; private set; } = 1.0;

        public IconPickerDialog(string currentSymbolKey, double currentScale = 1.0, double currentOpacity = 1.0)
        {
            InitializeComponent();
            LocalizationService.Instance.ApplyWindow(this);

            SelectedSymbolKey = string.IsNullOrWhiteSpace(currentSymbolKey) ? "Pin" : currentSymbolKey;
            SelectedScale = currentScale > 0 ? currentScale : 1.0;
            SelectedOpacity = currentOpacity > 0 ? currentOpacity : 1.0;

            ScaleSlider.Value = SelectedScale;
            OpacitySlider.Value = SelectedOpacity;

            BuildSymbolGrid();
            UpdateSelectionPreview();
        }

        private void BuildSymbolGrid()
        {
            bool isAr = LocalizationService.Instance.IsArabic;
            SymbolsWrapPanel.Children.Clear();
            foreach (var sym in AvailableSymbols)
            {
                var btn = new Button
                {
                    Width = 46,
                    Height = 46,
                    Margin = new Thickness(2),
                    Tag = sym,
                    ToolTip = isAr ? sym.DisplayNameAr : sym.DisplayName,
                    Background = (Brush)FindResource("Brush.Control.Background"),
                    BorderBrush = (Brush)FindResource("Brush.Control.Border"),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Content = new TextBlock
                    {
                        Text = sym.Glyph,
                        FontSize = 20,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                btn.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is SymbolEntry entry)
                    {
                        SelectedSymbolKey = entry.Key;
                        UpdateSelectionPreview();
                    }
                };

                SymbolsWrapPanel.Children.Add(btn);
            }
        }

        private void UpdateSelectionPreview()
        {
            var match = AvailableSymbols.Find(s => s.Key.Equals(SelectedSymbolKey, StringComparison.OrdinalIgnoreCase))
                        ?? AvailableSymbols[0];

            bool isAr = LocalizationService.Instance.IsArabic;
            string name = isAr ? match.DisplayNameAr : match.DisplayName;
            SelectedIconGlyph.Text = match.Glyph;
            SelectedIconNameText.Text = isAr ? $"المحدد: {name}" : $"Selected: {name}";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedScale = ScaleSlider.Value;
            SelectedOpacity = OpacitySlider.Value;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
