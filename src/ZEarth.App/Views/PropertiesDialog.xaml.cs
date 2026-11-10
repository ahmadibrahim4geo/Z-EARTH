using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;
using ZEarth.Domain;
using ZEarth.Domain.Kml;

namespace ZEarth.App.Views
{
    public partial class PropertiesDialog : Window
    {
        public class AttributeRow
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Category { get; set; } = "General";
            public bool IsEditable { get; set; } = true;
        }

        private readonly PlaceItem _item;
        private readonly ObservableCollection<AttributeRow> _attributes = new();
        private readonly Dictionary<string, string> _initialStyles = new();
        private bool _isUpdatingWebBrowser = false;
        private string _selectedSymbolKey = "Pin";
        private double _selectedScale = 1.0;
        private double _selectedOpacity = 1.0;

        public PropertiesDialog(PlaceItem item)
        {
            InitializeComponent();
            _item = item ?? throw new ArgumentNullException(nameof(item));
            ApplyLocalization();

            NameTextBox.Text = _item.Name;
            FeatureTypeBadge.Text = $"[{_item.ItemType}]";
            RawDescriptionTextBox.Text = _item.Description;

            string initialColor = string.IsNullOrWhiteSpace(_item.ColorHex) ? "#22A8DE" : _item.ColorHex;
            ColorHexTextBox.Text = initialColor;
            ColorPickerButtonControl.ColorHex = initialColor;
            ColorPickerButtonControl.ColorChanged += (s, e) =>
            {
                ColorHexTextBox.Text = ColorPickerButtonControl.ColorHex;
            };
            ColorHexTextBox.TextChanged += (s, e) =>
            {
                string text = ColorHexTextBox.Text.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$"))
                {
                    ColorPickerButtonControl.ColorHex = text;
                }
            };

            _selectedSymbolKey = ReadStringStyle("SymbolShape", "Pin");
            _selectedScale = ReadDoubleStyle("IconScale", 1.0);
            _selectedOpacity = ReadDoubleStyle("IconOpacity", 1.0);
            UpdateSymbolGlyphPreview(_selectedSymbolKey);

            string initialFillColor = ReadStringStyle("FillColor", initialColor);
            FillColorHexTextBox.Text = initialFillColor;
            FillColorPickerButtonControl.ColorHex = initialFillColor;
            FillColorPickerButtonControl.ColorChanged += (s, e) =>
            {
                FillColorHexTextBox.Text = FillColorPickerButtonControl.ColorHex;
            };
            FillColorHexTextBox.TextChanged += (s, e) =>
            {
                string text = FillColorHexTextBox.Text.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$"))
                {
                    FillColorPickerButtonControl.ColorHex = text;
                }
            };

            SelectComboItem(LinePatternComboBox, ReadStringStyle("LinePattern", "Solid"));
            SelectComboItem(FillModeComboBox, ReadStringStyle("FillMode", "Both"));

            LineWidthSlider.Value = ReadDoubleStyle("LineWidth", 3.0);
            FillOpacitySlider.Value = ReadDoubleStyle("FillOpacity", 0.4);
            LabelSizeSlider.Value = ReadDoubleStyle("LabelSize", 12.0);
            SelectComboItem(AltitudeModeComboBox, ReadStringStyle("AltitudeMode", "ClampToGround"));
            ShowLabelCheckBox.IsChecked = !string.Equals(ReadStringStyle("ShowLabel", "true"), "false", StringComparison.OrdinalIgnoreCase);

            // Contextual Visibility
            var isPoint = _item.ItemType == PlaceItemType.Placemark;
            var isPath = _item.ItemType == PlaceItemType.Path;
            var isPolygon = _item.ItemType == PlaceItemType.Polygon || _item.ItemType == PlaceItemType.Circle || _item.ItemType == PlaceItemType.Ellipse;

            PointSymbolRowLabel.Visibility = isPoint ? Visibility.Visible : Visibility.Collapsed;
            PointSymbolStack.Visibility = isPoint ? Visibility.Visible : Visibility.Collapsed;

            LinePatternRowLabel.Visibility = (isPath || isPolygon) ? Visibility.Visible : Visibility.Collapsed;
            LinePatternStack.Visibility = (isPath || isPolygon) ? Visibility.Visible : Visibility.Collapsed;

            LineWidthRowLabel.Visibility = (isPath || isPolygon) ? Visibility.Visible : Visibility.Collapsed;
            LineWidthStack.Visibility = (isPath || isPolygon) ? Visibility.Visible : Visibility.Collapsed;

            FillModeRowLabel.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;
            FillModeStack.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;

            FillColorRowLabel.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;
            FillColorStack.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;

            FillOpacityRowLabel.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;
            FillOpacityStack.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;

            _initialStyles["Color"] = ColorHexTextBox.Text;
            foreach (var (key, value) in CurrentStyles()) _initialStyles[key] = value;

            PopulateAttributes();
            AttributesDataGrid.ItemsSource = _attributes;

            LocalizationService.Instance.ApplyWindow(this);
        }

        private void PropertiesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSanitizedPreview();
        }

        private string ReadStringStyle(string key, string fallback) =>
            _item.ExtendedData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        private double ReadDoubleStyle(string key, double fallback) =>
            _item.ExtendedData.TryGetValue(key, out var value) &&
            double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        private static void SelectComboItem(ComboBox comboBox, string value)
        {
            foreach (var entry in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(entry.Tag?.ToString() ?? entry.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = entry;
                    return;
                }
            }
        }

        private static string SelectedComboText(ComboBox comboBox, string fallback) =>
            (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ??
            (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

        private Dictionary<string, string> CurrentStyles() => new()
        {
            ["SymbolShape"] = _selectedSymbolKey,
            ["IconScale"] = _selectedScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["IconOpacity"] = _selectedOpacity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["LinePattern"] = SelectedComboText(LinePatternComboBox, "Solid"),
            ["LineWidth"] = LineWidthSlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["FillMode"] = SelectedComboText(FillModeComboBox, "Both"),
            ["FillColor"] = FillColorHexTextBox.Text.Trim(),
            ["FillOpacity"] = FillOpacitySlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["LabelSize"] = LabelSizeSlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["AltitudeMode"] = SelectedComboText(AltitudeModeComboBox, "ClampToGround"),
            ["ShowLabel"] = ShowLabelCheckBox.IsChecked == true ? "true" : "false"
        };

        private void ChooseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new IconPickerDialog(_selectedSymbolKey, _selectedScale, _selectedOpacity)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true)
            {
                _selectedSymbolKey = dlg.SelectedSymbolKey;
                _selectedScale = dlg.SelectedScale;
                _selectedOpacity = dlg.SelectedOpacity;
                UpdateSymbolGlyphPreview(_selectedSymbolKey);
            }
        }

        private void UpdateSymbolGlyphPreview(string key)
        {
            var glyph = (key ?? "Pin").ToLowerInvariant() switch
            {
                "pin" => "📍",
                "circle" => "●",
                "square" => "■",
                "triangle" => "▲",
                "star" => "★",
                "diamond" => "◆",
                "target" => "🎯",
                "flag" => "🚩",
                "airport" => "✈",
                "hotel" => "🏨",
                "hospital" => "🏥",
                "school" => "🏫",
                "bank" => "🏦",
                "mosque" => "🕌",
                "home" => "🏠",
                "building" => "🏢",
                "tree" => "🌲",
                "mountain" => "⛰",
                "water" => "💧",
                "camera" => "📷",
                "warning" => "⚠",
                "info" => "ℹ",
                "restaurant" => "🍴",
                "shopping" => "🛒",
                _ => "📍"
            };
            CurrentSymbolGlyph.Text = glyph;
        }

        private void AttributesDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is AttributeRow row && !row.IsEditable)
            {
                e.Cancel = true;
            }
        }

        private void AttributeFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = AttributeFilterTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filter))
            {
                AttributesDataGrid.ItemsSource = _attributes;
            }
            else
            {
                var filtered = _attributes.Where(a =>
                    (a.Name?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (a.Value?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (a.Category?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
                AttributesDataGrid.ItemsSource = filtered;
            }
        }

        private void PopulateAttributes()
        {
            _attributes.Clear();

            // Core Geometry / Metadata Attributes (Read-only reference)
            _attributes.Add(new AttributeRow { Name = "Feature ID", Value = _item.Id, Category = "General", IsEditable = false });
            _attributes.Add(new AttributeRow { Name = "Item Type", Value = _item.ItemType.ToString(), Category = "General", IsEditable = false });
            _attributes.Add(new AttributeRow { Name = "Visibility", Value = _item.IsVisible ? "Visible" : "Hidden", Category = "General", IsEditable = false });

            if (_item.Latitude.HasValue && _item.Longitude.HasValue)
            {
                _attributes.Add(new AttributeRow { Name = "Latitude", Value = $"{_item.Latitude.Value:F6}°", Category = "Geometry", IsEditable = false });
                _attributes.Add(new AttributeRow { Name = "Longitude", Value = $"{_item.Longitude.Value:F6}°", Category = "Geometry", IsEditable = false });
            }

            if (_item.Altitude.HasValue)
            {
                _attributes.Add(new AttributeRow { Name = "Altitude", Value = $"{_item.Altitude.Value:F1} m", Category = "Geometry", IsEditable = false });
            }

            if (_item.Coordinates.Count > 0)
            {
                _attributes.Add(new AttributeRow { Name = "Coordinate Count", Value = _item.Coordinates.Count.ToString(), Category = "Geometry", IsEditable = false });
            }

            if (!string.IsNullOrEmpty(_item.StyleUrl))
            {
                _attributes.Add(new AttributeRow { Name = "Style URL", Value = _item.StyleUrl, Category = "Styling", IsEditable = false });
            }

            if (!string.IsNullOrEmpty(_item.TimeSpanBegin))
            {
                _attributes.Add(new AttributeRow { Name = "TimeSpan Begin", Value = _item.TimeSpanBegin, Category = "Temporal", IsEditable = false });
            }
            if (!string.IsNullOrEmpty(_item.TimeSpanEnd))
            {
                _attributes.Add(new AttributeRow { Name = "TimeSpan End", Value = _item.TimeSpanEnd, Category = "Temporal", IsEditable = false });
            }

            // ExtendedData / SchemaData Attributes (Editable)
            foreach (var kvp in _item.ExtendedData)
            {
                _attributes.Add(new AttributeRow
                {
                    Name = kvp.Key,
                    Value = kvp.Value,
                    Category = "ExtendedData",
                    IsEditable = true
                });
            }
        }

        private void ApplyLocalization()
        {
            bool isAr = LocalizationService.Instance.IsArabic;
            Title = isAr ? "خصائص المعلم — Z EARTH Desktop" : "Feature Properties — Z EARTH Desktop";
            TitleTextBlock.Text = isAr ? "خصائص المعلم" : "Feature Properties";
            NameLabelText.Text = isAr ? "الاسم:" : "Name:";
            TabDescription.Header = isAr ? "الوصف" : "Description";
            TabAttributes.Header = isAr ? "السمات" : "Attributes";
            TabStyle.Header = isAr ? "النمط والرمز" : "Style & Altitude";
            RenderedDescriptionHeader.Text = isAr ? "معاينة الوصف:" : "Rendered Description:";
            RawDescriptionHeader.Text = isAr ? "نص الوصف (قابل للتعديل):" : "Description Source / HTML:";
            FeatureColorRowLabel.Text = isAr ? "لون المعلم:" : "Feature Color:";
            PointSymbolRowLabel.Text = isAr ? "رمز النقطة:" : "Point Symbol:";
            LinePatternRowLabel.Text = isAr ? "نمط الخط:" : "Line pattern:";
            LineWidthRowLabel.Text = isAr ? "سمك الخط / الحدود:" : "Line / outline width:";
            FillModeRowLabel.Text = isAr ? "نمط التعبئة:" : "Fill mode:";
            FillColorRowLabel.Text = isAr ? "لون التعبئة:" : "Fill color:";
            FillOpacityRowLabel.Text = isAr ? "شفافية التعبئة:" : "Fill opacity:";
            ShowLabelCheckBox.Content = isAr ? "إظهار تسمية المعلم" : "Show feature label";
            LocalizationService.Instance.ApplyWindow(this);
        }

        private void UpdateSanitizedPreview()
        {
            if (_isUpdatingWebBrowser || DescriptionWebBrowser == null) return;
            _isUpdatingWebBrowser = true;

            try
            {
                bool isDark = ThemeService.Instance.CurrentTheme == AppTheme.Dark;
                bool isAr = LocalizationService.Instance.IsArabic;
                string raw = RawDescriptionTextBox?.Text?.Trim() ?? string.Empty;

                string html;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    html = HtmlSanitizer.WrapInSafeDocument(
                        isDark
                            ? $"<p style='color:#94a3b8; font-style:italic;'>{(isAr ? "لا يوجد وصف متاح لهذا المعلم." : "No description provided for this feature.")}</p>"
                            : $"<p style='color:#64748b; font-style:italic;'>{(isAr ? "لا يوجد وصف متاح لهذا المعلم." : "No description provided for this feature.")}</p>",
                        isDark);
                }
                else
                {
                    string sanitized = HtmlSanitizer.Sanitize(raw);
                    string textOnly = System.Text.RegularExpressions.Regex.Replace(sanitized, "<[^>]*>", "").Trim();
                    if (string.IsNullOrWhiteSpace(textOnly) && !sanitized.Contains("<img") && !sanitized.Contains("<table"))
                    {
                        html = HtmlSanitizer.WrapInSafeDocument(
                            $"<div style='color:#94a3b8; font-style:italic; padding:12px;'>{(isAr ? "(الوصف يحتوي على وسوم فارغة ولا يوجد نص معروض)" : "(Description contains empty markup with no displayable text)")}</div>",
                            isDark);
                    }
                    else
                    {
                        if (!sanitized.Contains("<p") && !sanitized.Contains("<div") && !sanitized.Contains("<table"))
                        {
                            sanitized = $"<div style='font-size:13px; line-height:1.6;'>{System.Net.WebUtility.HtmlEncode(sanitized).Replace("\n", "<br/>")}</div>";
                        }
                        html = HtmlSanitizer.WrapInSafeDocument(sanitized, isDark);
                    }
                }

                DescriptionWebBrowser.NavigateToString(html);
            }
            catch
            {
                // WebBrowser ActiveX might not be ready yet
            }
            finally
            {
                _isUpdatingWebBrowser = false;
            }
        }

        private void RawDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSanitizedPreview();
        }

        private void AddAttribute_Click(object sender, RoutedEventArgs e)
        {
            _attributes.Add(new AttributeRow
            {
                Name = $"CustomAttribute_{_attributes.Count(a => a.Category == "ExtendedData") + 1}",
                Value = "Value",
                Category = "ExtendedData",
                IsEditable = true
            });
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var color = ColorHexTextBox.Text?.Trim() ?? "#22A8DE";
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
            {
                MessageBox.Show(this, "Feature color must be a hexadecimal color such as #22A8DE.", "Invalid Style", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            AttributesDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            AttributesDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var editable = _attributes.Where(a => a.Category == "ExtendedData" && !string.IsNullOrWhiteSpace(a.Name)).ToList();
            if (editable.GroupBy(a => a.Name.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            {
                MessageBox.Show(this, "Attribute names must be unique.", "Invalid attributes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _item.Name = NameTextBox.Text?.Trim() ?? "Feature";
            _item.Description = RawDescriptionTextBox.Text ?? string.Empty;
            if (!string.Equals(color, _initialStyles["Color"], StringComparison.OrdinalIgnoreCase))
                _item.ColorHex = color;

            // Commit edited ExtendedData attributes
            _item.ExtendedData.Clear();
            foreach (var attr in editable)
            {
                _item.ExtendedData[attr.Name.Trim()] = attr.Value ?? string.Empty;
            }

            // Save visual styles
            foreach (var (key, value) in CurrentStyles())
                if (!string.Equals(value, _initialStyles.GetValueOrDefault(key), StringComparison.Ordinal))
                    _item.ExtendedData[key] = value;

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
