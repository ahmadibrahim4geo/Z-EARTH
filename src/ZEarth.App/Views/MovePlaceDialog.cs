using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZEarth.App.Services;
using ZEarth.Domain;

namespace ZEarth.App.Views
{
    /// <summary>Choose the Explorer root or a folder; no items are copied or moved by this dialog.</summary>
    public sealed class MovePlaceDialog : Window
    {
        private sealed record Choice(string Label, PlaceItem? Folder)
        {
            public override string ToString() => Label;
        }

        private readonly ComboBox _choices;
        public PlaceItem? Destination => (_choices.SelectedItem as Choice)?.Folder;

        public MovePlaceDialog(IEnumerable<PlaceItem> roots, PlaceItem item)
        {
            bool arabic = LocalizationService.Instance.IsArabic;
            Title = arabic ? "نقل العنصر" : "Move item";
            Width = 460;
            Height = 200;
            MinWidth = 360;
            MinHeight = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            SetResourceReference(BackgroundProperty, "Brush.Window.Background");
            SetResourceReference(ForegroundProperty, "Brush.Text.Primary");
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = arabic ? "اختر مجلد الوجهة أو جذر المستكشف:" : "Choose the Explorer root or a destination folder:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            _choices = new ComboBox { MinWidth = 300, MaxDropDownHeight = 300 };
            _choices.Items.Add(new Choice(arabic ? "جذر المستكشف" : "Explorer root", null));
            void AddFolders(IEnumerable<PlaceItem> items, string prefix)
            {
                foreach (var folder in items)
                {
                    if (ReferenceEquals(folder, item) || folder.ItemType != PlaceItemType.Folder) continue;
                    var path = string.IsNullOrEmpty(prefix) ? folder.Name : prefix + " / " + folder.Name;
                    if (folder.Id != "folder_saved_scenes" || item.ItemType == PlaceItemType.SavedScene)
                        _choices.Items.Add(new Choice(path, folder));
                    AddFolders(folder.Children, path);
                }
            }
            AddFolders(roots, "");
            _choices.SelectedIndex = 0;
            panel.Children.Add(_choices);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var move = new Button { Content = arabic ? "نقل" : "Move", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
            move.Click += (_, _) => DialogResult = true;
            buttons.Children.Add(move);
            buttons.Children.Add(new Button { Content = arabic ? "إلغاء" : "Cancel", IsCancel = true, MinWidth = 90 });
            panel.Children.Add(buttons);
            Content = panel;
        }
    }
}
