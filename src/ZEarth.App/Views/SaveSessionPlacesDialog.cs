using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;

namespace ZEarth.App.Views;

public sealed class SaveSessionPlacesDialog : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    public SaveSessionPlacesDialog(int pendingCount)
    {
        bool arabic = LocalizationService.Instance.IsArabic;
        Title = arabic ? "حفظ عناصر الجلسة — Z EARTH" : "Save Session Places? — Z EARTH";
        Width = 490;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text.Primary");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header with Icon
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 200, 255)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 255)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        iconBorder.Child = new TextBlock
        {
            Text = "💾",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBorder, 0);
        headerGrid.Children.Add(iconBorder);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = arabic
                ? $"لديك {pendingCount} من العناصر المستوردة أو المرسومة في هذه الجلسة."
                : $"You created or imported {pendingCount} item(s) during this session.",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        var subText = new TextBlock
        {
            Text = arabic
                ? "هل ترغب في حفظ هذه العناصر في مجلد الأماكن المؤقتة للجلسات القادمة؟"
                : "Do you want to save them inside Temporary Places for your next session?",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        subText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Secondary");
        textStack.Children.Add(subText);
        Grid.SetColumn(textStack, 1);
        headerGrid.Children.Add(textStack);

        Grid.SetRow(headerGrid, 0);
        root.Children.Add(headerGrid);

        // Buttons
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        Grid.SetRow(btnPanel, 2);

        var saveBtn = new Button
        {
            Content = arabic ? "حفظ في المؤقتة" : "Save",
            Width = 110,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        saveBtn.SetResourceReference(StyleProperty, "PrimaryButtonStyle");
        saveBtn.Click += (_, _) => { Result = MessageBoxResult.Yes; DialogResult = true; Close(); };
        btnPanel.Children.Add(saveBtn);

        var discardBtn = new Button
        {
            Content = arabic ? "إغلاق دون حفظ" : "Discard",
            Width = 120,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0)
        };
        discardBtn.SetResourceReference(StyleProperty, "SecondaryButtonStyle");
        discardBtn.Click += (_, _) => { Result = MessageBoxResult.No; DialogResult = true; Close(); };
        btnPanel.Children.Add(discardBtn);

        var cancelBtn = new Button
        {
            Content = arabic ? "إلغاء" : "Cancel",
            Width = 85,
            Height = 30,
            IsCancel = true
        };
        cancelBtn.SetResourceReference(StyleProperty, "SecondaryButtonStyle");
        cancelBtn.Click += (_, _) => { Result = MessageBoxResult.Cancel; DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);

        root.Children.Add(btnPanel);
        Content = root;
    }
}
