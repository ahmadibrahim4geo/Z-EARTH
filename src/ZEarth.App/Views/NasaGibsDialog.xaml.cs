using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ZEarth.App.Views;

public partial class NasaGibsDialog : Window
{
    public NasaGibsDialog()
    {
        InitializeComponent();
        ImageryDatePicker.DisplayDateStart = new DateTime(2000, 2, 24);
        ImageryDatePicker.DisplayDateEnd = DateTime.UtcNow.Date;
        ImageryDatePicker.SelectedDate = new DateTime(2012, 7, 9);
    }

    public string LayerId =>
        (SensorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    public string DateIso =>
        ImageryDatePicker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        var start = LayerId.Contains("Aqua", StringComparison.Ordinal)
            ? new DateTime(2002, 7, 3)
            : new DateTime(2000, 2, 24);
        var date = ImageryDatePicker.SelectedDate;
        if (date is null || date.Value < start || date.Value > DateTime.UtcNow.Date)
        {
            MessageBox.Show($"Choose a valid observation date after {start:yyyy-MM-dd} and not in the future.",
                "Invalid NASA GIBS Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
