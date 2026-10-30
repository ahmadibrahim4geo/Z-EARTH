using System.IO;
using System.Runtime.CompilerServices;

namespace ZEarth.Tests;

internal static class AssemblyTestEnvironment
{
    private static string? _settingsDirectory;

    [ModuleInitializer]
    internal static void Initialize()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZEarth.Tests",
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("ZEARTH_SETTINGS_DIRECTORY", _settingsDirectory);
        Environment.SetEnvironmentVariable("ZEARTH_PROFILE_DIRECTORY", _settingsDirectory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
    }

    private static void Cleanup()
    {
        Environment.SetEnvironmentVariable("ZEARTH_SETTINGS_DIRECTORY", null);
        Environment.SetEnvironmentVariable("ZEARTH_PROFILE_DIRECTORY", null);
        if (string.IsNullOrWhiteSpace(_settingsDirectory)) return;

        var full = Path.GetFullPath(_settingsDirectory);
        var safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ZEarth.Tests"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (full.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
    }
}
