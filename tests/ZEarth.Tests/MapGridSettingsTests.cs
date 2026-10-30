using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.Globe;

namespace ZEarth.Tests;

/// <summary>
/// Map Grid behaviour that must survive restarts and match the Cesium payload.
/// These tests are T2 evidence: they prove defaults, validation, colour
/// conversion and persistence, not native WebView2 rendering.
/// </summary>
[TestClass]
public sealed class MapGridSettingsTests
{
    [TestMethod]
    public void FreshSettingsMatchTheDocumentedGridDefaults()
    {
        var fresh = new AppSettingsModel();

        Assert.IsTrue(fresh.ShowKeyParallels, "Major geographic references default ON.");
        Assert.IsTrue(fresh.ShowLongitudeLines, "Meridians are ready when the master grid is enabled.");
        Assert.IsTrue(fresh.ShowLatitudeLines, "Parallels are ready when the master grid is enabled.");
        Assert.IsFalse(fresh.ShowUtmZones, "UTM zones default OFF.");
        Assert.AreEqual("DD", fresh.GridLabelFormat);
        Assert.AreEqual(0.0, fresh.LongitudeIntervalDegrees);
        Assert.AreEqual(0.0, fresh.LatitudeIntervalDegrees);
        Assert.AreEqual("GridZone", fresh.UtmLabelMode);
        Assert.AreEqual("Center", fresh.UtmLabelPlacement);
        Assert.AreEqual("Large", fresh.GridLabelSize);
        Assert.IsFalse(fresh.ShowGraticules, "The global grid toggle still defaults OFF.");

        // Five independent colours, all valid opaque hex.
        var colors = new[]
        {
            fresh.GridMajorColor, fresh.GridLongitudeColor, fresh.GridLatitudeColor,
            fresh.GridLabelColor, fresh.GridUtmColor
        };
        Assert.IsTrue(colors.All(c => GridColor.TryNormalize(c, out _)));
        Assert.AreEqual(5, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Every grid component must keep its own colour value.");
    }

    [TestMethod]
    public void ApplyingDefaultsNeverTouchesTheGlobalVisibilityToggle()
    {
        var settings = new AppSettingsModel { ShowGraticules = true };
        GridSettingsDefaults.Apply(settings);

        Assert.IsTrue(settings.ShowGraticules,
            "Reset to defaults must not silently switch the global grid off.");
        Assert.IsTrue(settings.ShowKeyParallels);
        Assert.IsTrue(settings.ShowLongitudeLines);
        Assert.IsTrue(settings.ShowLatitudeLines);
        Assert.IsFalse(settings.ShowUtmZones);
    }

    [TestMethod]
    public void GridColorAcceptsOpaqueAndAlphaHexAndRejectsInvalidText()
    {
        Assert.IsTrue(GridColor.TryNormalize("#ff3b30", out var lower));
        Assert.AreEqual("#FF3B30", lower);
        Assert.IsTrue(GridColor.TryNormalize("#80FF3B30", out var alpha));
        Assert.AreEqual("#FF3B30", alpha, "Stored colours are always opaque #RRGGBB.");
        Assert.IsTrue(GridColor.TryNormalize("  #1e90ff  ", out var trimmed));
        Assert.AreEqual("#1E90FF", trimmed);

        Assert.IsFalse(GridColor.TryNormalize(null, out _));
        Assert.IsFalse(GridColor.TryNormalize("", out _));
        Assert.IsFalse(GridColor.TryNormalize("red", out _));
        Assert.IsFalse(GridColor.TryNormalize("#12345", out _));
        Assert.IsFalse(GridColor.TryNormalize("#GGGGGG", out _));

        Assert.AreEqual("#FFFFFF", GridColor.NormalizeOrFallback("not-a-colour", "#FFFFFF"));
    }

    [TestMethod]
    public void GridColorConvertsToCesiumRgbaWithExplicitAlpha()
    {
        Assert.AreEqual("rgba(255, 59, 48, 0.78)", GridColor.ToCssRgba("#FF3B30", 0.78));
        Assert.AreEqual("rgba(30, 144, 255, 1)", GridColor.ToCssRgba("#1E90FF", 1.0));
        Assert.AreEqual("rgba(0, 0, 0, 0)", GridColor.ToCssRgba("#000000", 0.0));
        // Out-of-range alpha is clamped, never forwarded as NaN.
        Assert.AreEqual("rgba(255, 255, 255, 1)", GridColor.ToCssRgba("#FFFFFF", double.NaN));
        Assert.AreEqual("rgba(255, 255, 255, 1)", GridColor.ToCssRgba("#FFFFFF", 5.0));
    }

    [TestMethod]
    public void IntervalValidationAcceptsAutoAndRejectsOutOfRangeValues()
    {
        Assert.AreEqual(0.0, GridSettingsDefaults.NormalizeInterval(0, 10));
        Assert.AreEqual(0.05, GridSettingsDefaults.NormalizeInterval(0.05, 10));
        Assert.AreEqual(60.0, GridSettingsDefaults.NormalizeInterval(60, 10));
        Assert.AreEqual(10.0, GridSettingsDefaults.NormalizeInterval(0.01, 10));
        Assert.AreEqual(10.0, GridSettingsDefaults.NormalizeInterval(61, 10));
        Assert.AreEqual(10.0, GridSettingsDefaults.NormalizeInterval(-5, 10));
        Assert.AreEqual(10.0, GridSettingsDefaults.NormalizeInterval(double.NaN, 10));
        Assert.AreEqual(10.0, GridSettingsDefaults.NormalizeInterval(double.PositiveInfinity, 10));
    }

    [TestMethod]
    public void NormalizeRepairsInvalidValuesWithoutOverwritingValidUserChoices()
    {
        var settings = new AppSettingsModel
        {
            GridLabelFormat = "bogus",
            GridLabelSize = "Huge",
            UtmLabelMode = "nonsense",
            UtmLabelPlacement = "Boundary",
            GridMajorColor = "red",
            GridLongitudeColor = "#1e90ff",
            LongitudeIntervalDegrees = 2.5,
            LatitudeIntervalDegrees = 999
        };

        GridSettingsDefaults.Normalize(settings);

        Assert.AreEqual("DD", settings.GridLabelFormat);
        Assert.AreEqual("Large", settings.GridLabelSize);
        Assert.AreEqual("GridZone", settings.UtmLabelMode);
        Assert.AreEqual("Center", settings.UtmLabelPlacement);
        Assert.AreEqual(GridSettingsDefaults.GridMajorColor, settings.GridMajorColor);
        Assert.AreEqual("#1E90FF", settings.GridLongitudeColor, "Valid user colours must be preserved.");
        Assert.AreEqual(2.5, settings.LongitudeIntervalDegrees, "Valid user intervals must be preserved.");
        Assert.AreEqual(0.0, settings.LatitudeIntervalDegrees);
    }

    [TestMethod]
    public void GridSettingsSurviveAJsonRoundTrip()
    {
        var original = new AppSettingsModel
        {
            ShowGraticules = true,
            ShowKeyParallels = false,
            ShowLongitudeLines = true,
            ShowLatitudeLines = true,
            ShowUtmZones = true,
            GridLabelFormat = "DMS",
            GridLabelSize = "ExtraLarge",
            LongitudeIntervalDegrees = 5,
            LatitudeIntervalDegrees = 2,
            UtmLabelMode = "Both",
            UtmLabelPlacement = "Center",
            GridMajorColor = "#111213",
            GridLongitudeColor = "#222324",
            GridLatitudeColor = "#333435",
            GridLabelColor = "#F0F0F0",
            GridUtmColor = "#AABBCC"
        };

        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<AppSettingsModel>(json)!;
        GridSettingsDefaults.Normalize(loaded);

        Assert.IsTrue(loaded.ShowGraticules);
        Assert.IsFalse(loaded.ShowKeyParallels);
        Assert.IsTrue(loaded.ShowLongitudeLines);
        Assert.IsTrue(loaded.ShowLatitudeLines);
        Assert.IsTrue(loaded.ShowUtmZones);
        Assert.AreEqual("DMS", loaded.GridLabelFormat);
        Assert.AreEqual("ExtraLarge", loaded.GridLabelSize);
        Assert.AreEqual(5.0, loaded.LongitudeIntervalDegrees);
        Assert.AreEqual(2.0, loaded.LatitudeIntervalDegrees);
        Assert.AreEqual("Both", loaded.UtmLabelMode);
        Assert.AreEqual("#111213", loaded.GridMajorColor);
        Assert.AreEqual("#222324", loaded.GridLongitudeColor);
        Assert.AreEqual("#333435", loaded.GridLatitudeColor);
        Assert.AreEqual("#F0F0F0", loaded.GridLabelColor);
        Assert.AreEqual("#AABBCC", loaded.GridUtmColor);
    }

    [TestMethod]
    public void GridLabelFormatIsIndependentFromTheStatusBarCoordinateFormat()
    {
        var settings = new AppSettingsModel
        {
            CoordinateFormat = "DecimalDegrees",
            GridLabelFormat = "DMS"
        };

        Assert.AreEqual("DecimalDegrees", settings.CoordinateFormat);
        Assert.AreEqual("DMS", settings.GridLabelFormat);

        settings.CoordinateFormat = "Utm";
        Assert.AreEqual("DMS", settings.GridLabelFormat,
            "Changing the status-bar format must not change rendered grid labels.");

        settings.GridLabelFormat = "Degrees";
        Assert.AreEqual("Utm", settings.CoordinateFormat,
            "Changing the grid format must not change the status-bar format.");
    }

    [TestMethod]
    public void AtomicSettingsStorePersistsEveryGridField()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zearth-grid-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            var saved = new AppSettingsModel
            {
                ShowGraticules = true,
                ShowKeyParallels = true,
                ShowLongitudeLines = true,
                ShowLatitudeLines = false,
                ShowUtmZones = true,
                GridLabelFormat = "DM",
                GridLabelSize = "Medium",
                LongitudeIntervalDegrees = 15,
                LatitudeIntervalDegrees = 0,
                UtmLabelMode = "Hemisphere",
                UtmLabelPlacement = "Center",
                GridMajorColor = "#010203",
                GridLongitudeColor = "#040506",
                GridLatitudeColor = "#070809",
                GridLabelColor = "#0A0B0C",
                GridUtmColor = "#0D0E0F"
            };

            AppSettingsStore.Save(path, saved);
            var loaded = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(path))!;
            GridSettingsDefaults.Normalize(loaded);

            Assert.IsTrue(loaded.ShowGraticules);
            Assert.IsTrue(loaded.ShowLongitudeLines);
            Assert.IsFalse(loaded.ShowLatitudeLines);
            Assert.IsTrue(loaded.ShowUtmZones);
            Assert.AreEqual("DM", loaded.GridLabelFormat);
            Assert.AreEqual("Medium", loaded.GridLabelSize);
            Assert.AreEqual(15.0, loaded.LongitudeIntervalDegrees);
            Assert.AreEqual(0.0, loaded.LatitudeIntervalDegrees, "0 means automatic spacing.");
            Assert.AreEqual("Hemisphere", loaded.UtmLabelMode);
            Assert.AreEqual("#010203", loaded.GridMajorColor);
            Assert.AreEqual("#040506", loaded.GridLongitudeColor);
            Assert.AreEqual("#070809", loaded.GridLatitudeColor);
            Assert.AreEqual("#0A0B0C", loaded.GridLabelColor);
            Assert.AreEqual("#0D0E0F", loaded.GridUtmColor);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void SaveAppliesEveryGridFieldAndCancelWritesNothing()
    {
        var dialog = File.ReadAllText(FindSource("src/ZEarth.App/Views/OptionsDialog.xaml.cs"));
        var xaml = File.ReadAllText(FindSource("src/ZEarth.App/Views/OptionsDialog.xaml"));

        // Save is the only place that writes the grid model, and it must both
        // persist and push the same values to the Cesium engine.
        var save = dialog.Substring(dialog.IndexOf("OnSaveClick", StringComparison.Ordinal));
        foreach (var assignment in new[]
                 {
                     "settings.GridLabelFormat =", "settings.ShowKeyParallels =",
                     "settings.ShowLongitudeLines =", "settings.ShowLatitudeLines =",
                     "settings.LongitudeIntervalDegrees =", "settings.LatitudeIntervalDegrees =",
                     "settings.ShowUtmZones =", "settings.UtmLabelMode =",
                     "settings.UtmLabelPlacement =", "settings.GridLabelSize =",
                     "settings.GridMajorColor =", "settings.GridLongitudeColor =",
                     "settings.GridLatitudeColor =", "settings.GridLabelColor =",
                     "settings.GridUtmColor ="
                 })
        {
            StringAssert.Contains(save, assignment, $"Save must write {assignment}");
        }
        StringAssert.Contains(save, "SettingsService.Instance.Save();");
        StringAssert.Contains(save, "SetGraticulesConfigAsync(");

        // Cancel is a pure close: no handler, no settings write, no bridge call.
        var cancelLine = xaml.Split('\n').First(l => l.Contains("Content=\"Cancel\""));
        StringAssert.Contains(cancelLine, "IsCancel=\"True\"");
        Assert.IsFalse(cancelLine.Contains("Click="),
            "Cancel must not have a click handler that could apply staged values.");
    }

    [TestMethod]
    public void RibbonToggleIsAMasterGateThatPreservesComponentChoices()
    {
        var viewModel = File.ReadAllText(FindSource("src/ZEarth.App/ViewModels/MainViewModel.cs"));
        var toggle = viewModel.Substring(viewModel.IndexOf("ToggleGraticulesCommand =", StringComparison.Ordinal));
        toggle = toggle.Substring(0, toggle.IndexOf("}, \"Toggle coordinate grid\");", StringComparison.Ordinal));
        StringAssert.Contains(toggle, "SetGraticulesVisibleAsync(target)");
        // Off must be a master gate: turning it back on restores exactly the
        // components the user configured, because none of them are rewritten.
        foreach (var field in new[]
                 {
                     "ShowKeyParallels", "ShowLongitudeLines", "ShowLatitudeLines", "ShowUtmZones"
                 })
        {
            Assert.IsFalse(toggle.Contains(field + " ="),
                $"The global toggle must not rewrite {field}.");
        }
    }

    [TestMethod]
    public void BridgeDefaultsMatchTheDocumentedGridDefaults()
    {
        var method = typeof(GlobeBridge).GetMethod("SetGraticulesConfigAsync",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, "The bridge must expose one graticule configuration entry point.");
        var parameters = method!.GetParameters()
            .Where(p => p.HasDefaultValue)
            .ToDictionary(p => p.Name!, p => p.DefaultValue, StringComparer.Ordinal);

        Assert.AreEqual(true, parameters["showLongitudeLines"]);
        Assert.AreEqual(true, parameters["showLatitudeLines"]);
        Assert.AreEqual(false, parameters["showUtmZones"]);
        Assert.AreEqual(true, parameters["showKeyParallels"]);
        Assert.AreEqual("DD", parameters["gridLabelFormat"]);
        Assert.AreEqual(0.0, parameters["longitudeIntervalDegrees"]);
        Assert.AreEqual(0.0, parameters["latitudeIntervalDegrees"]);
        Assert.AreEqual("GridZone", parameters["utmLabelMode"]);
        Assert.AreEqual("Center", parameters["utmLabelPlacement"]);
        Assert.AreEqual("Large", parameters["gridLabelSize"]);
        Assert.AreEqual(GridSettingsDefaults.GridMajorColor, parameters["gridMajorColor"]);
        Assert.AreEqual(GridSettingsDefaults.GridLongitudeColor, parameters["gridLongitudeColor"]);
        Assert.AreEqual(GridSettingsDefaults.GridLatitudeColor, parameters["gridLatitudeColor"]);
        Assert.AreEqual(GridSettingsDefaults.GridLabelColor, parameters["gridLabelColor"]);
        Assert.AreEqual(GridSettingsDefaults.GridUtmColor, parameters["gridUtmColor"]);
    }

    [TestMethod]
    public void BridgePayloadCarriesEveryGridSetting()
    {
        var source = File.ReadAllText(FindSource("src/ZEarth.Globe/GlobeBridge.cs"));
        foreach (var field in new[]
                 {
                     "gridMajorColor", "gridLongitudeColor", "gridLatitudeColor",
                     "gridLabelColor", "gridUtmColor", "gridLabelSize", "utmLabelPlacement",
                     "longitudeIntervalDegrees", "latitudeIntervalDegrees", "gridLabelFormat"
                 })
        {
            StringAssert.Contains(source, field);
        }
        Assert.AreEqual(2, CountOccurrences(source, "gridUtmColor"),
            "The UTM colour appears once as a parameter and once in the payload.");
        Assert.AreEqual(0, CountOccurrences(source, "utmBoundaryColor"),
            "There must not be a second, independent UTM boundary colour property.");
        Assert.AreEqual(0, CountOccurrences(source, "utmColor"),
            "There must not be a duplicate UTM colour alias.");
    }

    private static string FindSource(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relative);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
