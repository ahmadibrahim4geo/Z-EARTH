using System;
using System.Threading.Tasks;

namespace ZEarth.Domain
{
    public interface IMapViewport
    {
        CameraState CurrentCamera { get; }
        SceneMode CurrentMode { get; }

        event EventHandler<CameraState>? CameraChanged;
        event EventHandler<SceneMode>? SceneModeChanged;

        Task<bool> FlyToAsync(CameraState target, double durationSeconds = 1.5);
        Task<bool> SetSceneModeAsync(SceneMode mode);
        Task<bool> ZoomInAsync(double factor = 1.5);
        Task<bool> ZoomOutAsync(double factor = 1.5);
        Task<bool> ResetNorthAsync();
        Task<bool> ResetPitchAsync();
        Task<bool> HomeAsync();
        Task<bool> FullExtentAsync();
    }
}
