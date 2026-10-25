using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZEarth.App.ViewModels
{
    public sealed class SplashViewModel : ViewModelBase
    {
        private string _statusMessage = "Starting Z EARTH Desktop GIS...";
        private double _progressValue = 0;
        private string _versionString = "Version 0.3.0-preview (Sprint 2: Navigation)";
        private bool _isError = false;
        private string _errorMessage = string.Empty;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string VersionString
        {
            get => _versionString;
            set => SetProperty(ref _versionString, value);
        }

        public bool IsError
        {
            get => _isError;
            set => SetProperty(ref _isError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public static System.Windows.Controls.BooleanToVisibilityConverter BooleanToVisibilityConverter { get; } = new();

        public CancellationTokenSource CancellationSource { get; } = new();

        public async Task RunStartupTasksAsync(Func<string, Task> onProgressUpdated)
        {
            var ct = CancellationSource.Token;
            ct.ThrowIfCancellationRequested();

            // Stage 1: Environment & Settings
            StatusMessage = "Loading user settings and environment...";
            ProgressValue = 20;
            await onProgressUpdated(StatusMessage);
            await Task.Delay(250, ct);

            // Stage 2: WebView2 Core Runtime
            StatusMessage = "Initializing WebView2 core runtime...";
            ProgressValue = 45;
            await onProgressUpdated(StatusMessage);
            await Task.Delay(250, ct);

            // Stage 3: Local 3D Globe Engine & Vector Assets
            StatusMessage = "Loading local CesiumJS 3D engine & offline vector assets...";
            ProgressValue = 70;
            await onProgressUpdated(StatusMessage);
            await Task.Delay(250, ct);

            // Stage 4: Places Catalog & Workspace
            StatusMessage = "Restoring workspace and Places catalog...";
            ProgressValue = 90;
            await onProgressUpdated(StatusMessage);
            await Task.Delay(200, ct);

            // Stage 5: Ready
            StatusMessage = "Ready. Launching Z EARTH Desktop...";
            ProgressValue = 100;
            await onProgressUpdated(StatusMessage);
            await Task.Delay(150, ct);
        }

        public void CancelStartup()
        {
            CancellationSource.Cancel();
            StatusMessage = "Startup cancelled by user.";
        }
    }
}
