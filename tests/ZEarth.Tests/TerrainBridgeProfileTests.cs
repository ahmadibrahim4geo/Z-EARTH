using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests;

[TestClass]
public sealed class TerrainBridgeProfileTests
{
    [TestMethod]
    public async Task CorrelatedPayload_ReturnsRealNumericTerrainHeights()
    {
        var bridge = new GlobeBridge();
        bridge.SendJsonToWebView = json =>
        {
            using var request = JsonDocument.Parse(json);
            Assert.AreEqual("terrain.samplePath", request.RootElement.GetProperty("action").GetString());
            var points = request.RootElement.GetProperty("payload").GetProperty("points");
            Assert.AreEqual(2, points.GetArrayLength());
            var response = new
            {
                requestId = request.RootElement.GetProperty("requestId").GetString(),
                action = "terrain.samplePath", status = "completed", success = true,
                payload = new { samples = new[] {
                    new { longitude = 31.0, latitude = 30.0, height = 145.25 },
                    new { longitude = 31.1, latitude = 30.1, height = 168.50 }
                } }
            };
            bridge.HandleWebMessage(JsonSerializer.Serialize(response));
            return Task.CompletedTask;
        };
        var samples = await bridge.SampleTerrainHeightsAsync(new[] {
            new CoordinatePoint(31,30), new CoordinatePoint(31.1,30.1)
        });
        Assert.IsNotNull(samples);
        Assert.AreEqual(2, samples.Count);
        Assert.AreEqual(145.25, samples[0].ElevationMeters, 1e-8);
        Assert.AreEqual(168.50, samples[1].ElevationMeters, 1e-8);
    }

    [TestMethod]
    public async Task IncompleteDemResponse_IsNotFakedAsZeroElevation()
    {
        var bridge = new GlobeBridge();
        bridge.SendJsonToWebView = json =>
        {
            using var req = JsonDocument.Parse(json);
            bridge.HandleWebMessage(JsonSerializer.Serialize(new
            {
                requestId = req.RootElement.GetProperty("requestId").GetString(),
                action = "terrain.samplePath", status = "completed", success = true,
                payload = new { samples = new object?[] {
                    new { longitude = 31.0, latitude = 30.0, height = (double?)145 },
                    new { longitude = 31.1, latitude = 30.1, height = (double?)null }
                } }
            }));
            return Task.CompletedTask;
        };
        var samples = await bridge.SampleTerrainHeightsAsync(new[] {
            new CoordinatePoint(31,30), new CoordinatePoint(31.1,30.1)
        });
        Assert.IsNull(samples, "Incomplete height data must not become a fictional profile.");
    }

    [TestMethod]
    public async Task InvalidGeometry_RejectsBeforeSendingToCesium()
    {
        var bridge = new GlobeBridge();
        var messages = 0;
        bridge.SendJsonToWebView = _ => { messages++; return Task.CompletedTask; };
        var invalid = await bridge.SampleTerrainHeightsAsync(new[] {
            new CoordinatePoint(31,30), new CoordinatePoint(31,120)
        });
        Assert.IsNull(invalid);
        Assert.AreEqual(0, messages);
        Assert.IsNull(await bridge.SampleTerrainHeightsAsync(new [] { new CoordinatePoint(31,30) }));
    }
}
