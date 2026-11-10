using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;

namespace ZEarth.App.Views
{
    /// <summary>
    /// Simple modal dialog for renaming a PlaceItem.
    /// </summary>
    public sealed class RenamePlaceDialog : Window
    {
        private readonly TextBox _nameBox;
        public string NewName => _nameBox.Text.Trim();

        public RenamePlaceDialog(string currentName)
        {
            bool arabic = LocalizationService.Instance.IsArabic;
            Title = arabic ? "إعادة تسمية العنصر" : "Rename Item";
            Width = 420;
            Height = 170;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            SetResourceReference(BackgroundProperty, "Brush.Window.Background");
            SetResourceReference(ForegroundProperty, "Brush.Text.Primary");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = arabic ? "أدخل الاسم الجديد:" : "Enter new name:",
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 12
            });

            _nameBox = new TextBox
            {
                Text = currentName,
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 13
            };
            panel.Children.Add(_nameBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = arabic ? "موافق" : "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(_nameBox.Text))
                {
                    DialogResult = true;
                }
            };
            var cancel = new Button { Content = arabic ? "إلغاء" : "Cancel", IsCancel = true, MinWidth = 80 };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            Content = panel;
            Loaded += (_, _) =>
            {
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }
    }
}
