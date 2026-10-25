using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ZEarth.App.Services;

namespace ZEarth.App.Views;

public partial class HistoricalImageryDialog : Window
{
    private readonly Func<WaybackRelease, Task>? _previewReleaseAsync;
    private readonly DispatcherTimer _previewTimer;
    private IReadOnlyList<WaybackRelease> _releases = Array.Empty<WaybackRelease>();
    private bool _updatingSelection;

    public WaybackRelease? SelectedRelease { get; private set; }

    public HistoricalImageryDialog(Func<WaybackRelease, Task>? previewReleaseAsync = null)
    {
        _previewReleaseAsync = previewReleaseAsync;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _previewTimer.Tick += OnPreviewTimerTick;
        InitializeComponent();
        ThemeService.Instance.ApplyWindowTheme(this, ThemeService.Instance.CurrentTheme);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Loading the public Wayback release catalog…";
        try
        {
            _releases = (await new WaybackService().GetReleasesAsync())
                .OrderBy(release => release.Date)
                .ToList();
            ReleaseCombo.ItemsSource = _releases;
            YearCombo.ItemsSource = _releases.Select(release => release.Date.Year).Distinct().OrderBy(year => year).ToList();
            if (_releases.Count == 0)
            {
                StatusText.Text = "No historical releases are currently available.";
                return;
            }

            ReleaseSlider.Maximum = _releases.Count - 1;
            OldestDateText.Text = _releases[0].Date.ToString("yyyy-MM-dd");
            NewestDateText.Text = _releases[^1].Date.ToString("yyyy-MM-dd");
            SelectRelease(_releases.Count - 1, preview: true);
            StatusText.Text = $"{_releases.Count:N0} releases available • live preview enabled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Wayback catalog unavailable: {ex.Message}";
        }
    }

    private void OnTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSelection || _releases.Count == 0) return;
        SelectRelease((int)Math.Round(e.NewValue), preview: true);
    }

    private void OnReleaseSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || ReleaseCombo.SelectedItem is not WaybackRelease release) return;
        SelectRelease(IndexOfRelease(release), preview: true);
    }

    private void OnYearSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || YearCombo.SelectedItem is not int year) return;
        var index = _releases.Select((release, i) => (release, i))
            .Where(item => item.release.Date.Year == year)
            .Select(item => item.i)
            .DefaultIfEmpty(-1)
            .Max();
        if (index >= 0) SelectRelease(index, preview: true);
    }

    private int IndexOfRelease(WaybackRelease release)
    {
        for (var i = 0; i < _releases.Count; i++)
        {
            if (_releases[i].ReleaseId == release.ReleaseId) return i;
        }
        return 0;
    }

    private void SelectRelease(int index, bool preview)
    {
        if (_releases.Count == 0) return;
        index = Math.Clamp(index, 0, _releases.Count - 1);
        var release = _releases[index];
        _updatingSelection = true;
        ReleaseSlider.Value = index;
        ReleaseCombo.SelectedItem = release;
        YearCombo.SelectedItem = release.Date.Year;
        SelectedRelease = release;
        SelectedDateText.Text = release.Date.ToString("yyyy-MM-dd");
        _updatingSelection = false;

        if (preview && _previewReleaseAsync != null)
        {
            _previewTimer.Stop();
            _previewTimer.Start();
        }
    }

    private async void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        if (SelectedRelease == null || _previewReleaseAsync == null) return;
        try
        {
            StatusText.Text = $"Loading {SelectedRelease.Date:yyyy-MM-dd}…";
            await _previewReleaseAsync(SelectedRelease);
            StatusText.Text = $"Showing {SelectedRelease.Date:yyyy-MM-dd} on the globe";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview unavailable: {ex.Message}";
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRelease == null) return;
        DialogResult = true;
    }
}
