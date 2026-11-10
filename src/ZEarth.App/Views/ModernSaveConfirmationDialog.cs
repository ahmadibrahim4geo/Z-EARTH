using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZEarth.App.Services;

namespace ZEarth.App.Views;

/// <summary>
/// Professional modern dark confirmation dialog matching Z EARTH dark GIS theme.
/// Replaces standard Win32 MessageBox with native WPF dark styling and full Arabic/English bilingual support.
/// </summary>
public sealed class ModernSaveConfirmationDialog : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    public ModernSaveConfirmationDialog(string projectName, string? customPrompt = null, string? customSubText = null)
    {
        bool isAr = LocalizationService.Instance.IsArabic;
        Title = isAr ? "حفظ التغييرات — Z EARTH" : "Save Changes — Z EARTH";
        Width = 510;
        Height = 230;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x26, 0x29));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header with Modern Badge Icon
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(40, 0x22, 0xA8, 0xDE)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xA8, 0xDE)),
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        iconBorder.Child = new TextBlock
        {
            Text = "💾",
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBorder, 0);
        headerGrid.Children.Add(iconBorder);

        var promptMessage = customPrompt ?? (isAr
            ? $"هل تريد حفظ التغييرات في '{projectName}' قبل الإغلاق؟"
            : $"Do you want to save changes to '{projectName}' before closing?");

        var subMessage = customSubText ?? (isAr
            ? "إذا أغلقت دون حفظ، فستفقد كل التعديلات التي أجريتها في هذه الجلسة."
            : "If you close without saving, your unsaved places and edits will be lost.");

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        textStack.Children.Add(new TextBlock
        {
            Text = promptMessage,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var subText = new TextBlock
        {
            Text = subMessage,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0xA3, 0xA8)),
            TextWrapping = TextWrapping.Wrap
        };
        textStack.Children.Add(subText);
        Grid.SetColumn(textStack, 1);
        headerGrid.Children.Add(textStack);

        Grid.SetRow(headerGrid, 0);
        root.Children.Add(headerGrid);

        // Action Buttons Row: [ Yes (Save) ] [ No (Discard) ] [ Cancel ]
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        Grid.SetRow(btnPanel, 2);

        var yesBtn = new Button
        {
            Content = isAr ? "نعم (حفظ)" : "Yes (Save)",
            Width = 105,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0xA8, 0xDE)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsDefault = true
        };
        yesBtn.Click += (_, _) => { Result = MessageBoxResult.Yes; DialogResult = true; Close(); };

        var noBtn = new Button
        {
            Content = isAr ? "لا (تجاهل)" : "No (Discard)",
            Width = 105,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x59, 0x60)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        noBtn.Click += (_, _) => { Result = MessageBoxResult.No; DialogResult = false; Close(); };

        var cancelBtn = new Button
        {
            Content = isAr ? "إلغاء" : "Cancel",
            Width = 85,
            Height = 32,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD3, 0xD6)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x59, 0x60)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsCancel = true
        };
        cancelBtn.Click += (_, _) => { Result = MessageBoxResult.Cancel; DialogResult = null; Close(); };

        btnPanel.Children.Add(yesBtn);
        btnPanel.Children.Add(noBtn);
        btnPanel.Children.Add(cancelBtn);

        root.Children.Add(btnPanel);
        Content = root;
    }

    public static MessageBoxResult Show(Window? owner, string projectName, string? customPrompt = null, string? customSubText = null)
    {
        var dlg = new ModernSaveConfirmationDialog(projectName, customPrompt, customSubText);
        if (owner != null && owner.IsVisible)
        {
            dlg.Owner = owner;
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dlg.Owner = Application.Current.MainWindow;
        }
        dlg.ShowDialog();
        return dlg.Result;
    }
}
