using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.Domain;
using ZEarth.Domain.Terrain;

namespace ZEarth.Tests;

[TestClass]
public sealed class TerrainProfilePathBuilderTests
{
    [TestMethod]
    public void SamplesAPathWithValidMonotoneDistancesAndRealEndpoints()
    {
        var route = new [] {
            new CoordinatePoint(31.0, 30.0, -55),
            new CoordinatePoint(31.04, 30.02, 900),
            new CoordinatePoint(31.08, 30.04, 1200)
        };
        var samples = TerrainProfilePathBuilder.Build(route, 50);
        Assert.IsTrue(samples.Count > 3 && samples.Count <= 50);
        Assert.AreEqual(31.0, samples[0].Location.Longitude, 1e-8);
        Assert.AreEqual(30.04, samples[^1].Location.Latitude, 1e-8);
        Assert.IsTrue(samples[^1].DistanceMeters > 0);
        Assert.IsTrue(samples.Zip(samples.Skip(1)).All(pair =>
            pair.First.DistanceMeters < pair.Second.DistanceMeters));
        // Query Z is NOT derived from source vertex Z. Terrain response provides measured height.
        Assert.IsTrue(samples.All(p => p.Location.Altitude == 0));
    }

    [TestMethod]
    public void WrapsAntimeridianInsteadOfFlyingAcrossTheWorld()
    {
        var q = TerrainProfilePathBuilder.Build(new [] {
            new CoordinatePoint(179.5, 12), new CoordinatePoint(-179.5, 12)
        }, 16);
        Assert.AreEqual(16, q.Count);
        Assert.IsTrue(q.All(p => Math.Abs(p.Location.Longitude) > 170));
        Assert.AreEqual(-179.5, q[^1].Location.Longitude, 1e-8);
    }

    [TestMethod]
    public void MissingAndInvalidGeometryDoesNotSynthesizeAnElevationProfile()
    {
        Assert.AreEqual(0, TerrainProfilePathBuilder.Build(new [] {
            new CoordinatePoint(31,30), new CoordinatePoint(31,30) }).Count);
        Assert.AreEqual(0, TerrainProfilePathBuilder.Build(new [] {
            new CoordinatePoint(31,99), new CoordinatePoint(32,30) }).Count);
        Assert.AreEqual(0, TerrainProfilePathBuilder.Build(new [] { new CoordinatePoint(31,30) }).Count);
    }
}
