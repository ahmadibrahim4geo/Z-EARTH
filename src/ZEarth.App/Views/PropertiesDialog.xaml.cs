using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;
using ZEarth.Domain;
using ZEarth.Domain.Kml;

namespace ZEarth.App.Views
{
    public class AttributeRow : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string _category = "ExtendedData";
        private bool _isEditable = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public bool IsEditable
        {
            get => _isEditable;
            set { _isEditable = value; OnPropertyChanged(nameof(IsEditable)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public partial class PropertiesDialog : Window
    {
        private readonly PlaceItem _item;
        private readonly ObservableCollection<AttributeRow> _attributes = new();
        private bool _isUpdatingWebBrowser = false;

        public PropertiesDialog(PlaceItem item)
        {
            InitializeComponent();
            _item = item ?? throw new ArgumentNullException(nameof(item));

            NameTextBox.Text = _item.Name;
            FeatureTypeBadge.Text = $"[{_item.ItemType}]";
            RawDescriptionTextBox.Text = _item.Description;

            ColorHexTextBox.Text = string.IsNullOrWhiteSpace(_item.ColorHex) ? "#22A8DE" : _item.ColorHex;
            LineWidthSlider.Value = ReadDoubleStyle("LineWidth", 3.0);
            FillOpacitySlider.Value = ReadDoubleStyle("FillOpacity", 0.4);
            LabelSizeSlider.Value = ReadDoubleStyle("LabelSize", 12.0);
            SelectComboItem(SymbolShapeComboBox, ReadStringStyle("SymbolShape", "Circle"));
            SelectComboItem(AltitudeModeComboBox, ReadStringStyle("AltitudeMode", "ClampToGround"));
            ShowLabelCheckBox.IsChecked = !string.Equals(ReadStringStyle("ShowLabel", "true"), "false", StringComparison.OrdinalIgnoreCase);

            PopulateAttributes();
            AttributesDataGrid.ItemsSource = _attributes;

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
                if (string.Equals(entry.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = entry;
                    return;
                }
            }
        }

        private static string SelectedComboText(ComboBox comboBox, string fallback) =>
            (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

        private void AttributesDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is AttributeRow row && !row.IsEditable)
            {
                e.Cancel = true;
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

            // Preserved XML Fragments
            for (int i = 0; i < _item.PreservedXmlFragments.Count; i++)
            {
                _attributes.Add(new AttributeRow
                {
                    Name = $"XML Fragment #{i + 1}",
                    Value = _item.PreservedXmlFragments[i],
                    Category = "Preserved XML",
                    IsEditable = false
                });
            }
        }

        private void UpdateSanitizedPreview()
        {
            if (_isUpdatingWebBrowser) return;
            _isUpdatingWebBrowser = true;

            try
            {
                bool isDark = ThemeService.Instance.CurrentTheme == AppTheme.Dark;
                string raw = RawDescriptionTextBox.Text ?? string.Empty;
                string sanitized = HtmlSanitizer.Sanitize(raw);
                string html = HtmlSanitizer.WrapInSafeDocument(sanitized, isDark);

                DescriptionWebBrowser.NavigateToString(html);
            }
            catch
            {
                // NavigateToString might fail if string is completely empty or WebBrowser not ready
            }
            finally
            {
                _isUpdatingWebBrowser = false;
            }
        }

        private void RawDescriptionTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
            _item.Name = NameTextBox.Text?.Trim() ?? "Feature";
            _item.Description = RawDescriptionTextBox.Text ?? string.Empty;

            var color = ColorHexTextBox.Text?.Trim() ?? "#22A8DE";
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
            {
                MessageBox.Show(this, "Feature color must be a hexadecimal color such as #22A8DE.", "Invalid Style", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _item.ColorHex = color;

            // Commit edited ExtendedData attributes
            _item.ExtendedData.Clear();
            foreach (var attr in _attributes.Where(a => a.Category == "ExtendedData" && !string.IsNullOrWhiteSpace(a.Name)))
            {
                _item.ExtendedData[attr.Name.Trim()] = attr.Value ?? string.Empty;
            }

            _item.ExtendedData["SymbolShape"] = SelectedComboText(SymbolShapeComboBox, "Circle");
            _item.ExtendedData["LineWidth"] = LineWidthSlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _item.ExtendedData["FillOpacity"] = FillOpacitySlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _item.ExtendedData["LabelSize"] = LabelSizeSlider.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _item.ExtendedData["AltitudeMode"] = SelectedComboText(AltitudeModeComboBox, "ClampToGround");
            _item.ExtendedData["ShowLabel"] = ShowLabelCheckBox.IsChecked == true ? "true" : "false";

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
