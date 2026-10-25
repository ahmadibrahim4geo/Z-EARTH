using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class SettingsModelSerializationTests
{
    [TestMethod]
    public void UserOptionsRoundTripPreservesSnappingUnitsAndAppearanceWithoutTokens()
    {
        var changed = new AppSettingsModel
        {
            SnappingEnabled = false,
            SnapGroundToleranceMeters = 18,
            SnapVertices = false,
            SnapMidpoints = true,
            SnapEdges = false,
            SnapIntersections = false,
            DistanceUnit = "Kilometers",
            AreaUnit = "EgyptianFeddan",
            ElevationUnit = "Feet",
            Theme = "Light",
            Language = "ar",
            LongitudeIntervalDegrees = 2.5,
            LatitudeIntervalDegrees = 1.25,
            ShowGraticules = true,
            CesiumIonToken = "secret-token-must-not-persist",
            EsriApiKey = "secret-key-must-not-persist"
        };
        var serialized = JsonSerializer.Serialize(changed);
        Assert.IsFalse(serialized.Contains("secret-token-must-not-persist"));
        Assert.IsFalse(serialized.Contains("secret-key-must-not-persist"));
        var loaded = JsonSerializer.Deserialize<AppSettingsModel>(serialized)!;
        Assert.IsFalse(loaded.SnappingEnabled);
        Assert.AreEqual(18.0, loaded.SnapGroundToleranceMeters);
        Assert.IsFalse(loaded.SnapVertices);
        Assert.IsFalse(loaded.SnapEdges);
        Assert.IsFalse(loaded.SnapIntersections);
        Assert.AreEqual("Kilometers", loaded.DistanceUnit);
        Assert.AreEqual("EgyptianFeddan", loaded.AreaUnit);
        Assert.AreEqual("Feet", loaded.ElevationUnit);
        Assert.AreEqual("Light", loaded.Theme);
        Assert.AreEqual("ar", loaded.Language);
        Assert.AreEqual(2.5, loaded.LongitudeIntervalDegrees);
        Assert.AreEqual(1.25, loaded.LatitudeIntervalDegrees);
        Assert.IsTrue(loaded.ShowGraticules);
        Assert.IsNull(loaded.CesiumIonToken);
        Assert.IsNull(loaded.EsriApiKey);
    }
}
