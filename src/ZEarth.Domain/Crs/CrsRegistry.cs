using System;
using System.Collections.Generic;
using System.Linq;

namespace ZEarth.Domain.Crs
{
    public interface ICrsRegistry
    {
        IReadOnlyList<CrsDefinition> GetAll();
        CrsDefinition? GetByEpsg(int epsg);
        IEnumerable<CrsDefinition> Search(string query);
        IEnumerable<CrsDefinition> GetRegionalPresets(string region);
    }

    public class CrsRegistry : ICrsRegistry
    {
        private readonly List<CrsDefinition> _crsList = new();
        private readonly Dictionary<int, CrsDefinition> _byEpsg = new();

        public CrsRegistry()
        {
            RegisterCoreCrs();
        }

        private void Register(CrsDefinition crs)
        {
            _crsList.Add(crs);
            _byEpsg[crs.EpsgCode] = crs;
        }

        private void RegisterCoreCrs()
        {
            // ==================== GEOGRAPHIC COORDINATE SYSTEMS ====================
            // 3D
            Register(new CrsDefinition(4979, "WGS 84 (3D)", "World", false, "degree", "Geographic Coordinate Systems/3D")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                Wkt = "GEOGCRS[\"WGS 84\",DATUM[\"World Geodetic System 1984\",ELLIPSOID[\"WGS 84\",6378137,298.257223563]],CS[ellipsoidal,3],AXIS[\"latitude\",north],AXIS[\"longitude\",east],AXIS[\"ellipsoidal height\",up],UNIT[\"metre\",1]]"
            });

            // World
            Register(new CrsDefinition(4326, "WGS 84", "World", false, "degree", "Geographic Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                Wkt = "GEOGCRS[\"WGS 84\",DATUM[\"World Geodetic System 1984\",ELLIPSOID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],CS[ellipsoidal,2],AXIS[\"geodetic latitude (Lat)\",north],AXIS[\"geodetic longitude (Lon)\",east],UNIT[\"degree\",0.0174532925199433]]"
            });

            // Africa -> Egypt
            Register(new CrsDefinition(4229, "Egypt 1907", "Egypt - onshore", false, "degree", "Geographic Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906"
            });

            // Africa -> Algeria
            Register(new CrsDefinition(4307, "Nord Sahara 1959", "Algeria onshore", false, "degree", "Geographic Coordinate Systems/Africa/Algeria")
            {
                Datum = "Nord Sahara 1959",
                Ellipsoid = "Clarke 1880 (RGS)"
            });

            // Europe
            Register(new CrsDefinition(4258, "ETRS89", "Europe - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Europe")
            {
                Datum = "European Terrestrial Reference System 1989",
                Ellipsoid = "GRS 1980"
            });

            // North America
            Register(new CrsDefinition(4269, "NAD83", "North America - onshore and offshore", false, "degree", "Geographic Coordinate Systems/North America")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4267, "NAD27", "North America - onshore", false, "degree", "Geographic Coordinate Systems/North America")
            {
                Datum = "North American Datum 1927",
                Ellipsoid = "Clarke 1866"
            });

            // Asia
            Register(new CrsDefinition(4301, "Tokyo", "Japan onshore", false, "degree", "Geographic Coordinate Systems/Asia")
            {
                Datum = "Tokyo",
                Ellipsoid = "Bessel 1841"
            });

            // Australia & New Zealand
            Register(new CrsDefinition(7844, "GDA2020", "Australia - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Australia and New Zealand")
            {
                Datum = "Geocentric Datum of Australia 2020",
                Ellipsoid = "GRS 1980"
            });

            // South America
            Register(new CrsDefinition(4674, "SIRGAS 2000", "Latin America", false, "degree", "Geographic Coordinate Systems/South America")
            {
                Datum = "Sistema de Referencia Geocentrico para las Americas 2000",
                Ellipsoid = "GRS 1980"
            });

            // ==================== PROJECTED COORDINATE SYSTEMS ====================
            // Africa -> Egypt (Survey of Egypt National Grid series)
            Register(new CrsDefinition(22992, "Egypt 1907 / Red Belt", "Egypt - onshore central (Cairo, Nile Valley & Delta)", true, "metre", "Projected Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"Egypt 1907 / Red Belt\",BASEGEOGCRS[\"Egypt 1907\"],CONVERSION[\"Red Belt\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",30],PARAMETER[\"Longitude of natural origin\",31],PARAMETER[\"Scale factor at natural origin\",1.0],PARAMETER[\"False easting\",615000],PARAMETER[\"False northing\",810000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(22993, "Egypt 1907 / Purple Belt", "Egypt - onshore west (Alexandria, Western Desert)", true, "metre", "Projected Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(22994, "Egypt 1907 / South Belt", "Egypt - onshore south (Upper Egypt, Aswan, Nubia)", true, "metre", "Projected Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(22995, "Egypt 1907 / Extended Purple Belt", "Egypt - western border with Libya", true, "metre", "Projected Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator"
            });

            // UTM / Zone Series -> Northern Hemisphere / WGS 84
            Register(new CrsDefinition(32635, "WGS 84 / UTM zone 35N", "Western Egypt (Alexandria, Matrouh, Western Desert), Libya, Greece", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"WGS 84 / UTM zone 35N\",BASEGEOGCRS[\"WGS 84\"],CONVERSION[\"UTM zone 35N\"],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(32636, "WGS 84 / UTM zone 36N", "Eastern Egypt (Cairo, Nile Delta, Upper Egypt, Sinai), Jordan, Syria", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"WGS 84 / UTM zone 36N\",BASEGEOGCRS[\"WGS 84\"],CONVERSION[\"UTM zone 36N\"],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(32637, "WGS 84 / UTM zone 37N", "Red Sea, Saudi Arabia, Easternmost Egypt border", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32631, "WGS 84 / UTM zone 31N", "Western Europe (Spain, France, UK)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32632, "WGS 84 / UTM zone 32N", "Central Europe (Germany, Italy, Norway, Switzerland)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32633, "WGS 84 / UTM zone 33N", "Eastern Europe (Poland, Austria, Sweden)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32618, "WGS 84 / UTM zone 18N", "North America (US East Coast, New York, Washington)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Northern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            // UTM / Zone Series -> Southern Hemisphere / WGS 84
            Register(new CrsDefinition(32735, "WGS 84 / UTM zone 35S", "Southern Africa (Angola, Namibia, South Africa)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Southern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32736, "WGS 84 / UTM zone 36S", "Eastern Africa (Kenya, Tanzania, Mozambique, South Africa)", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Southern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(32737, "WGS 84 / UTM zone 37S", "Madagascar, Indian Ocean", true, "metre", "Projected Coordinate Systems/UTM / Zone Series/Southern Hemisphere / WGS 84")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            // Gauss-Krüger / Zone Series
            Register(new CrsDefinition(28404, "Pulkovo 1942 / Gauss-Kruger zone 4", "Russia & Eastern Europe between 21°E and 27°E", true, "metre", "Projected Coordinate Systems/Gauss–Krüger / Zone Series")
            {
                Datum = "Pulkovo 1942",
                Ellipsoid = "Krassowsky 1940",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(28405, "Pulkovo 1942 / Gauss-Kruger zone 5", "Russia & Eastern Europe between 27°E and 33°E", true, "metre", "Projected Coordinate Systems/Gauss–Krüger / Zone Series")
            {
                Datum = "Pulkovo 1942",
                Ellipsoid = "Krassowsky 1940",
                ProjectionMethod = "Transverse Mercator"
            });

            // National Grids
            Register(new CrsDefinition(27700, "OSGB36 / British National Grid", "United Kingdom - onshore", true, "metre", "Projected Coordinate Systems/National Grids")
            {
                Datum = "OSGB 1936",
                Ellipsoid = "Airy 1830",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(2154, "RGF93 v1 / Lambert-93", "France onshore", true, "metre", "Projected Coordinate Systems/National Grids")
            {
                Datum = "Reseau Geodesique Francais 1993",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal (2SP)"
            });

            Register(new CrsDefinition(25832, "ETRS89 / UTM zone 32N", "Europe between 6°E and 12°E", true, "metre", "Projected Coordinate Systems/National Grids")
            {
                Datum = "European Terrestrial Reference System 1989",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Transverse Mercator"
            });

            // State Plane (US)
            Register(new CrsDefinition(2225, "NAD83 / California zone 1", "United States - California - Del Norte, Humboldt, Lassen, Modoc, Plumas, Shasta, Siskiyou, Tehama and Trinity counties", true, "metre", "Projected Coordinate Systems/State Plane")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            Register(new CrsDefinition(2277, "NAD83 / Texas Central", "United States - Texas - Central", true, "metre", "Projected Coordinate Systems/State Plane")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            Register(new CrsDefinition(2263, "NAD83 / New York Long Island", "United States - New York - Long Island, Bronx, Kings, New York, Queens, Richmond", true, "metre", "Projected Coordinate Systems/State Plane")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            // World Projected
            Register(new CrsDefinition(3857, "WGS 84 / Pseudo-Mercator (Web Mercator)", "World between 85.06°S and 85.06°N", true, "metre", "Projected Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Popular Visualisation Pseudo Mercator",
                Wkt = "PROJCRS[\"WGS 84 / Pseudo-Mercator\",BASEGEOGCRS[\"WGS 84\"],CONVERSION[\"Popular Visualisation Pseudo-Mercator\"],CS[Cartesian,2],AXIS[\"easting (X)\",east],AXIS[\"northing (Y)\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(54030, "World Robinson", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Robinson"
            });

            // Polar
            Register(new CrsDefinition(32661, "WGS 84 / UPS North", "Northern hemisphere north of 84°N (Arctic)", true, "metre", "Projected Coordinate Systems/Polar")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Universal Polar Stereographic"
            });

            Register(new CrsDefinition(32761, "WGS 84 / UPS South", "Southern hemisphere south of 80°S (Antarctica)", true, "metre", "Projected Coordinate Systems/Polar")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Universal Polar Stereographic"
            });

            // ==================== ADVANCED (VERTICAL-ONLY, NOT SELECTABLE FOR 2D XY) ====================
            Register(new CrsDefinition(5773, "EGM96 geoid height", "World", false, "metre", "Advanced (Vertical Only)")
            {
                IsVerticalOnly = true,
                Datum = "Earth Gravitational Model 1996",
                Ellipsoid = "WGS 84"
            });

            Register(new CrsDefinition(3855, "EGM2008 geoid height", "World", false, "metre", "Advanced (Vertical Only)")
            {
                IsVerticalOnly = true,
                Datum = "Earth Gravitational Model 2008",
                Ellipsoid = "WGS 84"
            });
        }

        public IReadOnlyList<CrsDefinition> GetAll() => _crsList.AsReadOnly();

        public CrsDefinition? GetByEpsg(int epsg)
        {
            _byEpsg.TryGetValue(epsg, out var def);
            return def;
        }

        public IEnumerable<CrsDefinition> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _crsList;
            query = query.Trim().ToLowerInvariant();

            return _crsList.Where(c =>
                c.EpsgCode.ToString().Contains(query) ||
                c.Name.ToLowerInvariant().Contains(query) ||
                c.AreaOfUse.ToLowerInvariant().Contains(query) ||
                c.Datum.ToLowerInvariant().Contains(query) ||
                c.CategoryPath.ToLowerInvariant().Contains(query) ||
                c.AuthorityCode.ToLowerInvariant().Contains(query));
        }

        public IEnumerable<CrsDefinition> GetRegionalPresets(string region)
        {
            if (string.Equals(region, "Egypt", StringComparison.OrdinalIgnoreCase))
            {
                return _crsList.Where(c => c.EpsgCode == 32636 || c.EpsgCode == 32635 || c.EpsgCode == 22992 || c.EpsgCode == 22993 || c.EpsgCode == 22994 || c.EpsgCode == 4229);
            }
            return _crsList;
        }
    }
}
