using System;

namespace ZEarth.Domain.Terrain
{
    public enum TerrainType
    {
        Ellipsoid,
        CesiumWorldTerrain,
        LocalDem,
        Custom
    }

    public class TerrainProviderConfig
    {
        public const double MinExaggeration = 0.2;
        public const double MaxExaggeration = 10.0;
        public const double DefaultExaggeration = 1.0;

        public static readonly double[] Presets = new double[] { 1.0, 1.5, 2.0, 3.0, 5.0 };

        public TerrainType Type { get; set; } = TerrainType.Ellipsoid;
        public string Name { get; set; } = "WGS84 Smooth Ellipsoid (Offline)";
        public string Url { get; set; } = string.Empty;
        public double Exaggeration { get; private set; } = DefaultExaggeration;
        public bool RequestVertexNormals { get; set; } = true;
        public bool RequestWaterMask { get; set; } = true;
        public string Attribution { get; set; } = "WGS84 Reference Ellipsoid";

        public TerrainProviderConfig()
        {
        }

        public TerrainProviderConfig(TerrainType type, string name, string url, string attribution = "")
        {
            Type = type;
            Name = name;
            Url = url;
            Attribution = attribution;
        }

        public void SetExaggeration(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                Exaggeration = DefaultExaggeration;
                return;
            }

            if (value < MinExaggeration) Exaggeration = MinExaggeration;
            else if (value > MaxExaggeration) Exaggeration = MaxExaggeration;
            else Exaggeration = Math.Round(value, 2);
        }

        public void ResetExaggeration()
        {
            Exaggeration = DefaultExaggeration;
        }

        public static TerrainProviderConfig CreateEllipsoid()
        {
            return new TerrainProviderConfig(TerrainType.Ellipsoid, "WGS84 Smooth Ellipsoid (Offline Default)", string.Empty, "WGS84 Reference Ellipsoid");
        }

        public static TerrainProviderConfig CreateGlobalOpenTerrain()
        {
            return new TerrainProviderConfig(
                TerrainType.LocalDem,
                "Global Elevation Terrain (AWS Open Data)",
                "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png",
                "Mapzen Terrarium elevation tiles hosted in the AWS Open Data registry"
            );
        }

        [Obsolete("Synthetic terrain was removed. Use CreateGlobalOpenTerrain instead.")]
        public static TerrainProviderConfig CreateSyntheticRegional() => CreateGlobalOpenTerrain();

        public static TerrainProviderConfig CreateWorldTerrain()
        {
            return new TerrainProviderConfig(
                TerrainType.CesiumWorldTerrain,
                "Global 3D Mesh Terrain",
                "https://assets.ion.cesium.com/1",
                "Terrain data © Cesium Ion / Copernicus DEM GLO-30"
            );
        }
    }
}
