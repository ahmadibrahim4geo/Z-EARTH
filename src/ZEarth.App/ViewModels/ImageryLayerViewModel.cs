using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZEarth.Domain.Imagery;

namespace ZEarth.App.ViewModels
{
    public class ImageryLayerViewModel : INotifyPropertyChanged
    {
        private readonly ImageryLayer _model;
        private Action<ImageryLayerViewModel>? _onVisibilityChanged;
        private Action<ImageryLayerViewModel>? _onOpacityChanged;

        public ImageryLayer Model => _model;

        public string Id => _model.Id;
        public string Name => _model.Name;
        public ImageryProviderType ProviderType => _model.ProviderType;
        public string UrlTemplate => _model.UrlTemplate;
        public string Attribution => _model.Attribution;
        public bool IsBaseLayer => _model.IsBaseLayer;
        public bool IsOfflineAvailable => _model.IsOfflineAvailable;

        public bool IsVisible
        {
            get => _model.IsVisible;
            set
            {
                if (_model.IsVisible != value)
                {
                    _model.IsVisible = value;
                    OnPropertyChanged();
                    _onVisibilityChanged?.Invoke(this);
                }
            }
        }

        public double Opacity
        {
            get => _model.Opacity;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_model.Opacity - clamped) > 0.001)
                {
                    _model.SetOpacity(clamped);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpacityPercentText));
                    _onOpacityChanged?.Invoke(this);
                }
            }
        }

        public string OpacityPercentText => $"{Math.Round(_model.Opacity * 100)}%";

        public ImageryLayerViewModel(ImageryLayer model, Action<ImageryLayerViewModel>? onVisibilityChanged = null, Action<ImageryLayerViewModel>? onOpacityChanged = null)
        {
            _model = model;
            _onVisibilityChanged = onVisibilityChanged;
            _onOpacityChanged = onOpacityChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
