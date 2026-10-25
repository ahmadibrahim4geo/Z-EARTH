using System;
using System.Windows;

namespace ZEarth.App.Views
{
    public partial class GraticuleOptionsDialog : Window
    {
        public GraticuleOptionsDialog(
            bool showLabels,
            bool majorLinesOnly,
            bool showKeyParallels,
            bool showUtmZones,
            int spacingLat,
            int spacingLon)
        {
            InitializeComponent();
            LabelsCheck.IsChecked = showLabels;
            MajorOnlyCheck.IsChecked = majorLinesOnly;
            KeyParallelsCheck.IsChecked = showKeyParallels;
            UtmZonesCheck.IsChecked = showUtmZones;
            SelectSpacing(LatSpacingCombo, spacingLat);
            SelectSpacing(LonSpacingCombo, spacingLon);
        }

        private static void SelectSpacing(System.Windows.Controls.ComboBox combo, int value)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == value.ToString())
                {
                    item.IsSelected = true;
                    return;
                }
            }
        }

        public bool ShowLabels => LabelsCheck.IsChecked == true;
        public bool MajorLinesOnly => MajorOnlyCheck.IsChecked == true;
        public bool ShowKeyParallels => KeyParallelsCheck.IsChecked == true;
        public bool ShowUtmZones => UtmZonesCheck.IsChecked == true;
        public int SpacingLat => int.TryParse(((System.Windows.Controls.ComboBoxItem)LatSpacingCombo.SelectedItem)?.Tag?.ToString(), out var l) ? l : 10;
        public int SpacingLon => int.TryParse(((System.Windows.Controls.ComboBoxItem)LonSpacingCombo.SelectedItem)?.Tag?.ToString(), out var l) ? l : 15;

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
