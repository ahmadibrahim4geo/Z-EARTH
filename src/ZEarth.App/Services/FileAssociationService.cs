using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ZEarth.App.Services
{
    public static class FileAssociationService
    {
        public static bool RegisterKmlKmzAssociation()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;

                // 1. Register ProgID
                using (var progIdKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ZEarth.GeoDocument"))
                {
                    progIdKey.SetValue("", "Z EARTH Geographic Document (KML/KMZ)");
                    using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                    {
                        iconKey.SetValue("", $"\"{exePath}\",0");
                    }
                    using (var cmdKey = progIdKey.CreateSubKey(@"shell\open\command"))
                    {
                        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                // 2. Associate .kml
                using (var kmlKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.kml"))
                {
                    kmlKey.SetValue("", "ZEarth.GeoDocument");
                }

                // 3. Associate .kmz
                using (var kmzKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.kmz"))
                {
                    kmzKey.SetValue("", "ZEarth.GeoDocument");
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError("Failed to register KML/KMZ association", ex);
                return false;
            }
        }
    }
}
