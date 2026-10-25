using System;

namespace ZEarth.Domain.Crs
{
    public class CrsDefinition
    {
        public int EpsgCode { get; set; }
        public string Authority { get; set; } = "EPSG";
        public string Name { get; set; } = string.Empty;
        public string AreaOfUse { get; set; } = string.Empty;
        public string Unit { get; set; } = "metre";
        public bool IsProjected { get; set; }
        public bool IsVerticalOnly { get; set; } = false;
        public string Datum { get; set; } = "WGS 84";
        public string Ellipsoid { get; set; } = "WGS 84";
        public string ProjectionMethod { get; set; } = string.Empty;
        public string CategoryPath { get; set; } = string.Empty;
        public string Wkt { get; set; } = string.Empty;
        public bool IsFavorite { get; set; } = false;

        public string AuthorityCode => $"{Authority}:{EpsgCode}";

        public CrsDefinition() { }

        public CrsDefinition(int epsg, string name, string areaOfUse, bool isProjected, string unit = "metre", string categoryPath = "")
        {
            EpsgCode = epsg;
            Name = name;
            AreaOfUse = areaOfUse;
            IsProjected = isProjected;
            Unit = unit;
            CategoryPath = categoryPath;
        }

        public override string ToString() => $"{AuthorityCode} - {Name} ({AreaOfUse})";
    }
}
