using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;
using ZEarth.Domain;

namespace ZEarth.App.Views;

public sealed class ChooseLabelFieldDialog : Window
{
    private readonly ComboBox _fieldCombo;
    private readonly TextBlock _previewText;
    private readonly CheckBox? _applyToChildrenCheck;
    private readonly PlaceItem _item;
    private readonly List<string> _availableFields;

    public string? SelectedField => _fieldCombo.SelectedItem?.ToString();
    public bool ApplyToChildren => _applyToChildrenCheck?.IsChecked == true;

    public ChooseLabelFieldDialog(PlaceItem item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        bool arabic = LocalizationService.Instance.IsArabic;
        Title = arabic ? "تحديد حقل التسمية — Z EARTH" : "Choose Label Field — Z EARTH";
        Width = 460;
        Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text.Primary");

        // Collect attribute fields
        var fieldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CollectKeys(PlaceItem p)
        {
            foreach (var k in p.ExtendedData.Keys) fieldSet.Add(k);
            foreach (var c in p.Children) CollectKeys(c);
        }
        CollectKeys(_item);
        _availableFields = fieldSet.OrderBy(k => k).ToList();

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerText = new TextBlock
        {
            Text = arabic
                ? "اختر حقل البيانات الذي سيتم عرضه كتسمية على الخريطة:"
                : "Select an attribute field to display as the label on the 3D globe:",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(headerText, 0);
        root.Children.Add(headerText);

        _fieldCombo = new ComboBox
        {
            Height = 30,
            ItemsSource = _availableFields,
            Margin = new Thickness(0, 0, 0, 10),
            SelectedIndex = _availableFields.Count > 0 ? 0 : -1
        };
        _fieldCombo.SelectionChanged += FieldCombo_SelectionChanged;
        Grid.SetRow(_fieldCombo, 1);
        root.Children.Add(_fieldCombo);

        _previewText = new TextBlock
        {
            Text = arabic ? "قيمة العينة: —" : "Sample value: —",
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _previewText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Secondary");
        Grid.SetRow(_previewText, 2);
        root.Children.Add(_previewText);

        if (_item.ItemType == PlaceItemType.Folder)
        {
            _applyToChildrenCheck = new CheckBox
            {
                Content = arabic ? "تطبيق هذا الحقل على جميع العناصر داخل المجلد" : "Apply to all child features in this folder",
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_applyToChildrenCheck, 3);
            root.Children.Add(_applyToChildrenCheck);
        }

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(btnPanel, 5);

        var okBtn = new Button
        {
            Content = arabic ? "تطبيق" : "Apply",
            Width = 90,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okBtn.SetResourceReference(StyleProperty, "PrimaryButtonStyle");
        okBtn.Click += (_, _) => { DialogResult = true; Close(); };
        btnPanel.Children.Add(okBtn);

        var cancelBtn = new Button
        {
            Content = arabic ? "إلغاء" : "Cancel",
            Width = 80,
            Height = 28,
            IsCancel = true
        };
        cancelBtn.SetResourceReference(StyleProperty, "SecondaryButtonStyle");
        btnPanel.Children.Add(cancelBtn);

        root.Children.Add(btnPanel);
        Content = root;

        UpdatePreview();
    }

    private void FieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_previewText == null) return;
        var field = SelectedField;
        if (string.IsNullOrEmpty(field))
        {
            _previewText.Text = LocalizationService.Instance.IsArabic ? "لا توجد حقول بيانات متاحة." : "No attribute fields available.";
            return;
        }

        string? sample = null;
        if (_item.ExtendedData.TryGetValue(field, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            sample = val;
        }
        else
        {
            foreach (var c in _item.Children)
            {
                if (c.ExtendedData.TryGetValue(field, out var cVal) && !string.IsNullOrWhiteSpace(cVal))
                {
                    sample = cVal;
                    break;
                }
            }
        }

        _previewText.Text = LocalizationService.Instance.IsArabic
            ? $"قيمة العينة: {sample ?? "—"}"
            : $"Sample value: {sample ?? "—"}";
    }
}
