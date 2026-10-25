using System;
using System.IO;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace ZEarth.Domain.Gis
{
    public static class GdalConfiguration
    {
        private static bool _isConfigured = false;
        private static readonly object _lock = new object();

        public static bool IsAvailable { get; private set; } = false;

        public static void EnsureInitialized()
        {
            if (_isConfigured) return;

            lock (_lock)
            {
                if (_isConfigured) return;

                try
                {
                    MaxRev.Gdal.Core.GdalBase.ConfigureAll();
                    Gdal.AllRegister();
                    Ogr.RegisterAll();
                    IsAvailable = true;
                }
                catch (Exception ex)
                {
                    IsAvailable = false;
                    System.Diagnostics.Debug.WriteLine($"GDAL initialization error: {ex.Message}");
                }
                finally
                {
                    _isConfigured = true;
                }
            }
        }
    }
}
