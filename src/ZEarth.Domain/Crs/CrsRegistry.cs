using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Data.Sqlite;
using ZEarth.Domain.Gis;
using OSGeo.OSR;

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
            // Merge the complete installed PROJ EPSG catalog into the curated presets.
            // Do not claim the tiny built-in list is a complete world CRS catalog.
            foreach (var item in ProjCatalog.Value)
            {
                if (!_byEpsg.ContainsKey(item.EpsgCode))
                {
                    Register(item);
                }
            }
        }

        private void Register(CrsDefinition crs)
        {
            _crsList.Add(crs);
            _byEpsg[crs.EpsgCode] = crs;
        }

        private void RegisterCoreCrs()
        {
            // ==================== GEOGRAPHIC COORDINATE SYSTEMS ====================
            // World
            Register(new CrsDefinition(4326, "WGS 84", "World", false, "degree", "Geographic Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                Wkt = "GEOGCRS[\"WGS 84\",DATUM[\"World Geodetic System 1984\",ELLIPSOID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],CS[ellipsoidal,2],AXIS[\"geodetic latitude (Lat)\",north],AXIS[\"geodetic longitude (Lon)\",east],UNIT[\"degree\",0.0174532925199433]]"
            });

            Register(new CrsDefinition(4979, "WGS 84 (3D)", "World", false, "degree", "Geographic Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                Wkt = "GEOGCRS[\"WGS 84\",DATUM[\"World Geodetic System 1984\",ELLIPSOID[\"WGS 84\",6378137,298.257223563]],CS[ellipsoidal,3],AXIS[\"latitude\",north],AXIS[\"longitude\",east],AXIS[\"ellipsoidal height\",up],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(4322, "WGS 72", "World", false, "degree", "Geographic Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1972",
                Ellipsoid = "WGS 72"
            });

            Register(new CrsDefinition(4019, "GRS 1980", "World", false, "degree", "Geographic Coordinate Systems/World")
            {
                Datum = "Not specified (based on GRS 1980 ellipsoid)",
                Ellipsoid = "GRS 1980"
            });

            // Africa
            Register(new CrsDefinition(4229, "Egypt 1907", "Egypt - onshore", false, "degree", "Geographic Coordinate Systems/Africa/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906"
            });

            Register(new CrsDefinition(4307, "Nord Sahara 1959", "Algeria onshore", false, "degree", "Geographic Coordinate Systems/Africa/Algeria")
            {
                Datum = "Nord Sahara 1959",
                Ellipsoid = "Clarke 1880 (RGS)"
            });

            Register(new CrsDefinition(4261, "Merchich", "Morocco onshore", false, "degree", "Geographic Coordinate Systems/Africa/Morocco")
            {
                Datum = "Merchich",
                Ellipsoid = "Clarke 1880 (IGN)"
            });

            Register(new CrsDefinition(4223, "Carthage", "Tunisia onshore", false, "degree", "Geographic Coordinate Systems/Africa/Tunisia")
            {
                Datum = "Carthage",
                Ellipsoid = "Clarke 1880 (IGN)"
            });

            Register(new CrsDefinition(4201, "Adindan", "Sudan & Ethiopia", false, "degree", "Geographic Coordinate Systems/Africa/Sudan")
            {
                Datum = "Adindan",
                Ellipsoid = "Clarke 1880 (RGS)"
            });

            Register(new CrsDefinition(4148, "Hartebeesthoek94", "South Africa", false, "degree", "Geographic Coordinate Systems/Africa/South Africa")
            {
                Datum = "Hartebeesthoek94",
                Ellipsoid = "WGS 84"
            });

            // Asia & Middle East
            Register(new CrsDefinition(4204, "Ain el Abd 1970", "Saudi Arabia & Arabian Gulf", false, "degree", "Geographic Coordinate Systems/Asia/Saudi Arabia")
            {
                Datum = "Ain el Abd 1970",
                Ellipsoid = "International 1924 (Hayford)"
            });

            Register(new CrsDefinition(4281, "Palestine 1923", "Palestine & Levant", false, "degree", "Geographic Coordinate Systems/Asia/Palestine")
            {
                Datum = "Palestine 1923",
                Ellipsoid = "Clarke 1880 (Benoit)"
            });

            Register(new CrsDefinition(4237, "Jordan", "Jordan onshore", false, "degree", "Geographic Coordinate Systems/Asia/Jordan")
            {
                Datum = "Jordan",
                Ellipsoid = "International 1924"
            });

            Register(new CrsDefinition(4253, "Kuwait KOC", "Kuwait onshore & offshore", false, "degree", "Geographic Coordinate Systems/Asia/Kuwait")
            {
                Datum = "Kuwait Oil Company",
                Ellipsoid = "Clarke 1880"
            });

            Register(new CrsDefinition(4286, "Qatar 1974", "Qatar onshore & offshore", false, "degree", "Geographic Coordinate Systems/Asia/Qatar")
            {
                Datum = "Qatar 1974",
                Ellipsoid = "International 1924"
            });

            Register(new CrsDefinition(4311, "Trucial Coast 1948", "United Arab Emirates", false, "degree", "Geographic Coordinate Systems/Asia/United Arab Emirates")
            {
                Datum = "Trucial Coast 1948",
                Ellipsoid = "Helmert 1906"
            });

            Register(new CrsDefinition(4301, "Tokyo", "Japan onshore", false, "degree", "Geographic Coordinate Systems/Asia/Japan")
            {
                Datum = "Tokyo",
                Ellipsoid = "Bessel 1841"
            });

            Register(new CrsDefinition(6668, "JGD2011", "Japan onshore & offshore", false, "degree", "Geographic Coordinate Systems/Asia/Japan")
            {
                Datum = "Japanese Geodetic Datum 2011",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4490, "CGCS2000", "China onshore & offshore", false, "degree", "Geographic Coordinate Systems/Asia/China")
            {
                Datum = "China Geodetic Coordinate System 2000",
                Ellipsoid = "CGCS2000"
            });

            Register(new CrsDefinition(4214, "Beijing 1954", "China", false, "degree", "Geographic Coordinate Systems/Asia/China")
            {
                Datum = "Beijing 1954",
                Ellipsoid = "Krassowsky 1940"
            });

            Register(new CrsDefinition(4284, "Pulkovo 1942", "Russia & Former Soviet Union", false, "degree", "Geographic Coordinate Systems/Europe/Russia")
            {
                Datum = "Pulkovo 1942",
                Ellipsoid = "Krassowsky 1940"
            });

            // Europe
            Register(new CrsDefinition(4258, "ETRS89", "Europe - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Europe")
            {
                Datum = "European Terrestrial Reference System 1989",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4230, "ED50", "Europe - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Europe")
            {
                Datum = "European Datum 1950",
                Ellipsoid = "International 1924"
            });

            Register(new CrsDefinition(4277, "OSGB36", "United Kingdom - onshore", false, "degree", "Geographic Coordinate Systems/Europe/United Kingdom")
            {
                Datum = "OSGB 1936",
                Ellipsoid = "Airy 1830"
            });

            Register(new CrsDefinition(4171, "RGF93 v1", "France onshore & offshore", false, "degree", "Geographic Coordinate Systems/Europe/France")
            {
                Datum = "Reseau Geodesique Francais 1993",
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

            // South America
            Register(new CrsDefinition(4674, "SIRGAS 2000", "Latin America", false, "degree", "Geographic Coordinate Systems/South America")
            {
                Datum = "Sistema de Referencia Geocentrico para las Americas 2000",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4291, "SAD69", "South America", false, "degree", "Geographic Coordinate Systems/South America")
            {
                Datum = "South American Datum 1969",
                Ellipsoid = "GRS 1967"
            });

            // Australia & New Zealand
            Register(new CrsDefinition(7844, "GDA2020", "Australia - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Australia and New Zealand")
            {
                Datum = "Geocentric Datum of Australia 2020",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4283, "GDA94", "Australia - onshore and offshore", false, "degree", "Geographic Coordinate Systems/Australia and New Zealand")
            {
                Datum = "Geocentric Datum of Australia 1994",
                Ellipsoid = "GRS 1980"
            });

            Register(new CrsDefinition(4167, "NZGD2000", "New Zealand", false, "degree", "Geographic Coordinate Systems/Australia and New Zealand")
            {
                Datum = "New Zealand Geodetic Datum 2000",
                Ellipsoid = "GRS 1980"
            });

            // ==================== PROJECTED COORDINATE SYSTEMS ====================
            // 1. UTM Zone Series: Complete Northern Hemisphere (1N to 60N, EPSG 32601 to 32660)
            for (int z = 1; z <= 60; z++)
            {
                int epsg = 32600 + z;
                int lon0 = (z * 6) - 183;
                string hemisphere = "Northern Hemisphere";
                string zoneDesc = z switch
                {
                    35 => "Western Egypt (Alexandria, Matrouh, Western Desert), Libya, Greece",
                    36 => "Eastern Egypt (Cairo, Nile Delta, Upper Egypt, Sinai), Jordan, Syria",
                    37 => "Red Sea, Saudi Arabia, Easternmost Egypt border",
                    31 => "Western Europe (Spain, France, UK)",
                    32 => "Central Europe (Germany, Italy, Norway, Switzerland)",
                    33 => "Eastern Europe (Poland, Austria, Sweden)",
                    18 => "North America (US East Coast, New York, Washington)",
                    _ => $"Between {lon0 - 3}° and {lon0 + 3}° East/West (Central Meridian: {lon0}°)"
                };

                Register(new CrsDefinition(epsg, $"WGS 84 / UTM zone {z}N", zoneDesc, true, "metre", $"Projected Coordinate Systems/UTM/WGS 1984/{hemisphere}")
                {
                    Datum = "World Geodetic System 1984",
                    Ellipsoid = "WGS 84",
                    ProjectionMethod = "Transverse Mercator",
                    Wkt = $"PROJCRS[\"WGS 84 / UTM zone {z}N\",BASEGEOGCRS[\"WGS 84\"],CONVERSION[\"UTM zone {z}N\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",0],PARAMETER[\"Longitude of natural origin\",{lon0}],PARAMETER[\"Scale factor at natural origin\",0.9996],PARAMETER[\"False easting\",500000],PARAMETER[\"False northing\",0]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
                });
            }

            // 2. UTM Zone Series: Complete Southern Hemisphere (1S to 60S, EPSG 32701 to 32760)
            for (int z = 1; z <= 60; z++)
            {
                int epsg = 32700 + z;
                int lon0 = (z * 6) - 183;
                string hemisphere = "Southern Hemisphere";
                string zoneDesc = z switch
                {
                    35 => "Southern Africa (Angola, Namibia, South Africa)",
                    36 => "Eastern Africa (Kenya, Tanzania, Mozambique, South Africa)",
                    37 => "Madagascar, Indian Ocean",
                    _ => $"Between {lon0 - 3}° and {lon0 + 3}° East/West (Central Meridian: {lon0}°)"
                };

                Register(new CrsDefinition(epsg, $"WGS 84 / UTM zone {z}S", zoneDesc, true, "metre", $"Projected Coordinate Systems/UTM/WGS 1984/{hemisphere}")
                {
                    Datum = "World Geodetic System 1984",
                    Ellipsoid = "WGS 84",
                    ProjectionMethod = "Transverse Mercator",
                    Wkt = $"PROJCRS[\"WGS 84 / UTM zone {z}S\",BASEGEOGCRS[\"WGS 84\"],CONVERSION[\"UTM zone {z}S\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",0],PARAMETER[\"Longitude of natural origin\",{lon0}],PARAMETER[\"Scale factor at natural origin\",0.9996],PARAMETER[\"False easting\",500000],PARAMETER[\"False northing\",10000000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
                });
            }

            // 3. National Grids -> Egypt (Survey of Egypt National Grids)
            Register(new CrsDefinition(22992, "Egypt 1907 / Red Belt", "Egypt - onshore central (Cairo, Nile Valley & Delta)", true, "metre", "Projected Coordinate Systems/National Grids/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"Egypt 1907 / Red Belt\",BASEGEOGCRS[\"Egypt 1907\"],CONVERSION[\"Red Belt\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",30],PARAMETER[\"Longitude of natural origin\",31],PARAMETER[\"Scale factor at natural origin\",1.0],PARAMETER[\"False easting\",615000],PARAMETER[\"False northing\",810000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(22993, "Egypt 1907 / Purple Belt", "Egypt - onshore west (Alexandria, Western Desert)", true, "metre", "Projected Coordinate Systems/National Grids/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"Egypt 1907 / Purple Belt\",BASEGEOGCRS[\"Egypt 1907\"],CONVERSION[\"Purple Belt\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",30],PARAMETER[\"Longitude of natural origin\",27],PARAMETER[\"Scale factor at natural origin\",1.0],PARAMETER[\"False easting\",365000],PARAMETER[\"False northing\",810000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(22994, "Egypt 1907 / South Belt", "Egypt - onshore south (Upper Egypt, Aswan, Nubia)", true, "metre", "Projected Coordinate Systems/National Grids/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"Egypt 1907 / South Belt\",BASEGEOGCRS[\"Egypt 1907\"],CONVERSION[\"South Belt\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",22],PARAMETER[\"Longitude of natural origin\",31],PARAMETER[\"Scale factor at natural origin\",1.0],PARAMETER[\"False easting\",615000],PARAMETER[\"False northing\",300000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            Register(new CrsDefinition(22995, "Egypt 1907 / Extended Purple Belt", "Egypt - western border with Libya", true, "metre", "Projected Coordinate Systems/National Grids/Egypt")
            {
                Datum = "Egypt 1907",
                Ellipsoid = "Helmert 1906",
                ProjectionMethod = "Transverse Mercator",
                Wkt = "PROJCRS[\"Egypt 1907 / Extended Purple Belt\",BASEGEOGCRS[\"Egypt 1907\"],CONVERSION[\"Extended Purple Belt\",METHOD[\"Transverse Mercator\"],PARAMETER[\"Latitude of natural origin\",30],PARAMETER[\"Longitude of natural origin\",25],PARAMETER[\"Scale factor at natural origin\",1.0],PARAMETER[\"False easting\",165000],PARAMETER[\"False northing\",810000]],CS[Cartesian,2],AXIS[\"Easting\",east],AXIS[\"Northing\",north],UNIT[\"metre\",1]]"
            });

            // 4. National Grids -> Arabian Peninsula & Middle East
            Register(new CrsDefinition(20137, "Adindan / UTM zone 37N", "Sudan - onshore", true, "metre", "Projected Coordinate Systems/Africa/Sudan") { Datum = "Adindan", Ellipsoid = "Clarke 1880 (RGS)", ProjectionMethod = "Transverse Mercator" });
            Register(new CrsDefinition(20435, "Nord Sahara 1959 / UTM zone 31N", "Algeria - onshore", true, "metre", "Projected Coordinate Systems/Africa/Algeria") { Datum = "Nord Sahara 1959", Ellipsoid = "Clarke 1880 (RGS)", ProjectionMethod = "Transverse Mercator" });
            Register(new CrsDefinition(20437, "Ain el Abd 1970 / UTM zone 37N", "Saudi Arabia - Western Province, Hejaz, Asir", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Ain el Abd 1970",
                Ellipsoid = "International 1924",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(20438, "Ain el Abd 1970 / UTM zone 38N", "Saudi Arabia - Central Province, Riyadh, Najd", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Ain el Abd 1970",
                Ellipsoid = "International 1924",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(20439, "Ain el Abd 1970 / UTM zone 39N", "Saudi Arabia - Eastern Province, Dammam, Arabian Gulf", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Ain el Abd 1970",
                Ellipsoid = "International 1924",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(28191, "Palestine 1923 / Palestine Grid", "Palestine & Levant onshore (Cassini-Soldner)", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Palestine 1923",
                Ellipsoid = "Clarke 1880 (Benoit)",
                ProjectionMethod = "Cassini-Soldner"
            });

            Register(new CrsDefinition(2039, "Israel 1993 / Israeli TM Grid", "Israel & Palestine - onshore", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Israel 1993",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(23847, "Jordan / JTM", "Jordan - onshore (Jordan Transverse Mercator)", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Jordan",
                Ellipsoid = "International 1924",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(2932, "Qatar 1974 / Qatar National Grid", "Qatar - onshore and offshore", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Qatar 1974",
                Ellipsoid = "International 1924",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(3997, "Dubai Local TM (DLTM)", "United Arab Emirates - Dubai", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "WGS 84",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(3438, "Oman National Grid", "Oman - onshore", true, "metre", "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East")
            {
                Datum = "Fahud",
                Ellipsoid = "Clarke 1880",
                ProjectionMethod = "Transverse Mercator"
            });

            // 5. National Grids -> United Kingdom & Europe
            Register(new CrsDefinition(27700, "OSGB36 / British National Grid", "United Kingdom - onshore", true, "metre", "Projected Coordinate Systems/National Grids/United Kingdom")
            {
                Datum = "OSGB 1936",
                Ellipsoid = "Airy 1830",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(2154, "RGF93 v1 / Lambert-93", "France onshore", true, "metre", "Projected Coordinate Systems/National Grids/France")
            {
                Datum = "Reseau Geodesique Francais 1993",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal (2SP)"
            });

            Register(new CrsDefinition(31467, "DHDN / 3-degree Gauss-Kruger zone 3", "Germany - 7.5°E to 10.5°E", true, "metre", "Projected Coordinate Systems/National Grids/Germany")
            {
                Datum = "Deutsches Hauptdreiecksnetz",
                Ellipsoid = "Bessel 1841",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(25832, "ETRS89 / UTM zone 32N", "Europe between 6°E and 12°E", true, "metre", "Projected Coordinate Systems/National Grids/Europe")
            {
                Datum = "European Terrestrial Reference System 1989",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Transverse Mercator"
            });

            Register(new CrsDefinition(3035, "ETRS89 / LAEA Europe", "Europe - statistical analysis and mapping", true, "metre", "Projected Coordinate Systems/National Grids/Europe")
            {
                Datum = "European Terrestrial Reference System 1989",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Azimuthal Equal Area"
            });

            // 6. Gauss-Krüger / Pulkovo 1942
            for (int z = 4; z <= 32; z++)
            {
                int epsg = 28400 + z;
                int lon0 = (z * 6) - 3;
                Register(new CrsDefinition(epsg, $"Pulkovo 1942 / Gauss-Kruger zone {z}", $"Russia & Eastern Europe between {lon0 - 3}°E and {lon0 + 3}°E", true, "metre", "Projected Coordinate Systems/Gauss–Krüger/Pulkovo 1942")
                {
                    Datum = "Pulkovo 1942",
                    Ellipsoid = "Krassowsky 1940",
                    ProjectionMethod = "Transverse Mercator"
                });
            }

            // 7. State Plane (US SPCS NAD83)
            Register(new CrsDefinition(2225, "NAD83 / California zone 1", "United States - California - Zone 1", true, "metre", "Projected Coordinate Systems/State Plane/NAD 1983 (Meters)")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            Register(new CrsDefinition(2277, "NAD83 / Texas Central", "United States - Texas - Central", true, "metre", "Projected Coordinate Systems/State Plane/NAD 1983 (Meters)")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            Register(new CrsDefinition(2263, "NAD83 / New York Long Island", "United States - New York - Long Island, NYC", true, "metre", "Projected Coordinate Systems/State Plane/NAD 1983 (Meters)")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Lambert Conic Conformal"
            });

            Register(new CrsDefinition(2236, "NAD83 / Florida East", "United States - Florida - East", true, "metre", "Projected Coordinate Systems/State Plane/NAD 1983 (Meters)")
            {
                Datum = "North American Datum 1983",
                Ellipsoid = "GRS 1980",
                ProjectionMethod = "Transverse Mercator"
            });

            // 8. World Projected
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

            Register(new CrsDefinition(54009, "World Mollweide", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Mollweide"
            });

            Register(new CrsDefinition(54003, "World Miller Cylindrical", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Miller Cylindrical"
            });

            Register(new CrsDefinition(54008, "World Sinusoidal", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Sinusoidal"
            });

            Register(new CrsDefinition(4087, "WGS 84 / World Equidistant Cylindrical", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Equidistant Cylindrical (Plate Carrée)"
            });

            Register(new CrsDefinition(54004, "World Mercator", "World between 80°S and 84°N", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Mercator"
            });

            Register(new CrsDefinition(54029, "World Van der Grinten I", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Van der Grinten I"
            });

            Register(new CrsDefinition(54042, "World Winkel Tripel", "World", true, "metre", "Projected Coordinate Systems/World")
            {
                Authority = "ESRI",
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Winkel Tripel"
            });

            // 9. Polar
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

            Register(new CrsDefinition(3995, "WGS 84 / Arctic Polar Stereographic", "Northern hemisphere north of 60°N", true, "metre", "Projected Coordinate Systems/Polar")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Polar Stereographic"
            });

            Register(new CrsDefinition(3031, "WGS 84 / Antarctic Polar Stereographic", "Southern hemisphere south of 60°S", true, "metre", "Projected Coordinate Systems/Polar")
            {
                Datum = "World Geodetic System 1984",
                Ellipsoid = "WGS 84",
                ProjectionMethod = "Polar Stereographic"
            });

            // ==================== VERTICAL SYSTEMS ====================
            Register(new CrsDefinition(5773, "EGM96 geoid height", "World", false, "metre", "Vertical Coordinate Systems/Gravity-related")
            {
                IsVerticalOnly = true,
                Datum = "Earth Gravitational Model 1996",
                Ellipsoid = "WGS 84"
            });

            Register(new CrsDefinition(3855, "EGM2008 geoid height", "World", false, "metre", "Vertical Coordinate Systems/Gravity-related")
            {
                IsVerticalOnly = true,
                Datum = "Earth Gravitational Model 2008",
                Ellipsoid = "WGS 84"
            });
        }

        private static readonly Lazy<IReadOnlyList<CrsDefinition>> ProjCatalog = new(() =>
        {
            var sqliteRecords = LoadProjCatalog();
            return sqliteRecords.Count >= 1000 ? sqliteRecords : LoadGdalCrsCatalog();
        });

        public int ProjCatalogCount => ProjCatalog.Value.Count;

        private static string? LocateProjDatabase()
        {
            GdalConfiguration.EnsureInitialized();
            var candidates = new List<string>();
            foreach (var key in new[] { "PROJ_DATA", "PROJ_LIB" })
            {
                var folder = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(folder))
                    candidates.Add(Path.Combine(folder, "proj.db"));
            }
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "proj.db"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "maxrev.gdal.core.libshared", "proj.db"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "maxrev.gdal.core.libshared", "proj.db"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "share", "proj", "proj.db"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "share", "proj", "proj.db"));

            foreach (var file in candidates)
                if (File.Exists(file)) return file;

            try
            {
                var deployed = Directory.EnumerateFiles(AppContext.BaseDirectory, "proj.db", SearchOption.AllDirectories).FirstOrDefault();
                if (deployed != null) return deployed;

                var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
                if (string.IsNullOrWhiteSpace(nugetRoot))
                    nugetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

                if (Directory.Exists(nugetRoot))
                {
                    foreach (var pkgDir in Directory.GetDirectories(nugetRoot, "maxrev.gdal*"))
                    {
                        var found = Directory.EnumerateFiles(pkgDir, "proj.db", SearchOption.AllDirectories).FirstOrDefault();
                        if (found != null) return found;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PROJ catalog discovery: " + ex.Message);
                return null;
            }
        }

        private static readonly (string Region, string Names)[] CountryRegions = {
            ("Africa", "Algeria|Angola|Benin|Botswana|Burkina Faso|Burundi|Cabo Verde|Cape Verde|Cameroon|Central African Republic|Chad|Comoros|Congo|Democratic Republic of the Congo|Djibouti|Egypt|Equatorial Guinea|Eritrea|Eswatini|Ethiopia|Gabon|Gambia|Ghana|Guinea-Bissau|Guinea|Ivory Coast|Côte d'Ivoire|Kenya|Lesotho|Liberia|Libya|Madagascar|Malawi|Mali|Mauritania|Mauritius|Morocco|Mozambique|Namibia|Niger|Nigeria|Rwanda|Sao Tome|São Tomé|Senegal|Seychelles|Sierra Leone|Somalia|South Africa|South Sudan|Sudan|Tanzania|Togo|Tunisia|Uganda|Western Sahara|Zambia|Zimbabwe"),
            ("Asia", "Afghanistan|Armenia|Azerbaijan|Bahrain|Bangladesh|Bhutan|Brunei|Cambodia|China|Cyprus|Georgia|India|Indonesia|Iran|Iraq|Israel|Japan|Jordan|Kazakhstan|Kuwait|Kyrgyzstan|Laos|Lebanon|Malaysia|Maldives|Mongolia|Myanmar|Nepal|North Korea|Oman|Pakistan|Palestine|Philippines|Qatar|Saudi Arabia|Singapore|South Korea|Sri Lanka|Syria|Taiwan|Tajikistan|Thailand|Timor-Leste|Turkey|Türkiye|Turkmenistan|United Arab Emirates|Uzbekistan|Vietnam|Yemen"),
            ("Europe", "Albania|Andorra|Austria|Belarus|Belgium|Bosnia and Herzegovina|Bulgaria|Croatia|Czechia|Czech Republic|Denmark|Estonia|Finland|France|Germany|Gibraltar|Greece|Hungary|Iceland|Ireland|Italy|Kosovo|Latvia|Liechtenstein|Lithuania|Luxembourg|Malta|Moldova|Monaco|Montenegro|Netherlands|North Macedonia|Norway|Poland|Portugal|Romania|Russia|San Marino|Serbia|Slovakia|Slovenia|Spain|Sweden|Switzerland|Ukraine|United Kingdom|Vatican City"),
            ("North America", "Anguilla|Antigua|Bahamas|Barbados|Belize|Bermuda|Canada|Cayman Islands|Costa Rica|Cuba|Dominica|Dominican Republic|El Salvador|Greenland|Grenada|Guatemala|Haiti|Honduras|Jamaica|Mexico|Nicaragua|Panama|Puerto Rico|Saint Kitts|Saint Lucia|Saint Vincent|Trinidad and Tobago|United States|US Virgin Islands"),
            ("South America", "Argentina|Bolivia|Brazil|Chile|Colombia|Ecuador|Falkland Islands|French Guiana|Guyana|Paraguay|Peru|Suriname|Uruguay|Venezuela"),
            ("Australia and New Zealand", "Australia|New Zealand|Papua New Guinea|Fiji|New Caledonia|Solomon Islands|Vanuatu|Samoa|Tonga|Kiribati|Tuvalu")
        };

        private static string AreaFolder(string area)
        {
            if (string.IsNullOrWhiteSpace(area) || area.Equals("World", StringComparison.OrdinalIgnoreCase))
                return "World";

            foreach (var (region, names) in CountryRegions)
            {
                foreach (var country in names.Split('|').OrderByDescending(x => x.Length))
                {
                    if (!area.StartsWith(country, StringComparison.OrdinalIgnoreCase)) continue;
                    if (area.Length == country.Length || !char.IsLetter(area[country.Length]))
                        return region + "/" + country;
                }
            }

            foreach (var heading in new[] { "Africa", "Asia", "Europe", "North America", "South America",
                "Australia", "Antarctica", "Arctic", "Pacific", "Oceania" })
            {
                if (area.Contains(heading, StringComparison.OrdinalIgnoreCase))
                    return heading;
            }

            return "Other Areas/" + (area.Split(" - ")[0].Trim().Replace("/", " – "));
        }

        private static string ClassifyArcGisCategory(bool isProjected, bool isVertical, string name, string area)
        {
            if (isVertical)
            {
                if (name.Contains("geoid", StringComparison.OrdinalIgnoreCase) || name.Contains("EGM", StringComparison.OrdinalIgnoreCase))
                    return "Vertical Coordinate Systems/Gravity-related";
                return "Vertical Coordinate Systems/Ellipsoidal-based";
            }

            if (!isProjected)
            {
                if (area.Equals("World", StringComparison.OrdinalIgnoreCase) || name.StartsWith("WGS 84") || name.StartsWith("GRS 1980") || name.StartsWith("WGS 72"))
                    return "Geographic Coordinate Systems/World";

                return "Geographic Coordinate Systems/" + AreaFolder(area);
            }

            // Projected Coordinate Systems
            // 1. UTM Zone Series
            if (name.Contains("UTM zone", StringComparison.OrdinalIgnoreCase) || name.Contains("UTM Zone", StringComparison.OrdinalIgnoreCase))
            {
                if (name.StartsWith("WGS 84", StringComparison.OrdinalIgnoreCase) || name.StartsWith("WGS_1984", StringComparison.OrdinalIgnoreCase))
                {
                    if (name.EndsWith("N", StringComparison.OrdinalIgnoreCase) || name.Contains("north", StringComparison.OrdinalIgnoreCase))
                        return "Projected Coordinate Systems/UTM/WGS 1984/Northern Hemisphere";
                    return "Projected Coordinate Systems/UTM/WGS 1984/Southern Hemisphere";
                }
                if (name.StartsWith("NAD83", StringComparison.OrdinalIgnoreCase) || name.StartsWith("NAD 1983", StringComparison.OrdinalIgnoreCase))
                    return "Projected Coordinate Systems/UTM/NAD 1983";
                if (name.StartsWith("ED50", StringComparison.OrdinalIgnoreCase) || name.StartsWith("European 1950", StringComparison.OrdinalIgnoreCase))
                    return "Projected Coordinate Systems/UTM/European Datum 1950 (ED50)";

                return "Projected Coordinate Systems/UTM/Other Datums";
            }

            // 2. National Grids
            if (name.Contains("Egypt 1907", StringComparison.OrdinalIgnoreCase) || area.StartsWith("Egypt", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/National Grids/Egypt";

            if (name.Contains("Ain el Abd", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Palestine", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Israel", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Jordan", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Qatar", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Kuwait", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Dubai", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Trucial", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Oman", StringComparison.OrdinalIgnoreCase) ||
                area.Contains("Saudi Arabia", StringComparison.OrdinalIgnoreCase))
            {
                return "Projected Coordinate Systems/National Grids/Arabian Peninsula & Middle East";
            }

            if (name.Contains("British National Grid", StringComparison.OrdinalIgnoreCase) || name.Contains("OSGB", StringComparison.OrdinalIgnoreCase) || area.Contains("United Kingdom", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/National Grids/United Kingdom";

            if (name.Contains("Lambert-93", StringComparison.OrdinalIgnoreCase) || name.Contains("RGF93", StringComparison.OrdinalIgnoreCase) || area.Contains("France", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/National Grids/France";

            if (name.Contains("DHDN", StringComparison.OrdinalIgnoreCase) || area.Contains("Germany", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/National Grids/Germany";

            if (name.Contains("ETRS89", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/National Grids/Europe";

            // 3. Gauss-Krüger
            if (name.Contains("Gauss-Kruger", StringComparison.OrdinalIgnoreCase) || name.Contains("Pulkovo 1942", StringComparison.OrdinalIgnoreCase))
                return "Projected Coordinate Systems/Gauss–Krüger/Pulkovo 1942";

            // 4. State Plane
            if (name.Contains("State Plane", StringComparison.OrdinalIgnoreCase) || (name.StartsWith("NAD83") && name.Contains("zone", StringComparison.OrdinalIgnoreCase)))
                return "Projected Coordinate Systems/State Plane/NAD 1983 (Meters)";

            // 5. World Projections
            if (name.Contains("Pseudo-Mercator", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Web Mercator", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Robinson", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Mollweide", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Miller", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sinusoidal", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Winkel", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Eckert", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Van der Grinten", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("World", StringComparison.OrdinalIgnoreCase))
            {
                return "Projected Coordinate Systems/World";
            }

            // 6. Polar Projections
            if (name.Contains("UPS", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Polar Stereographic", StringComparison.OrdinalIgnoreCase) ||
                area.Contains("Arctic", StringComparison.OrdinalIgnoreCase) ||
                area.Contains("Antarctic", StringComparison.OrdinalIgnoreCase))
            {
                return "Projected Coordinate Systems/Polar";
            }

            // 7. General Continental / Regional by Area
            return "Projected Coordinate Systems/" + AreaFolder(area);
        }

        private static IReadOnlyList<CrsDefinition> LoadProjCatalog()
        {
            var path = LocateProjDatabase();
            var results = new List<CrsDefinition>();
            if (path == null) return results;

            try
            {
                var cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly };
                using var connection = new SqliteConnection(cs.ToString());
                connection.Open();

                foreach (var table in new[] { "geodetic_crs", "projected_crs", "vertical_crs" })
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $@"SELECT CAST(t.code AS TEXT), t.name,
                        COALESCE(e.name,'World') AS area, COALESCE(un.name,'metre') AS unit,
                        ${(table == "vertical_crs" ? "''" : "COALESCE(t.text_definition,'')")} AS definition
                        FROM {table} t
                        LEFT JOIN usage u ON u.object_table_name='{table}'
                          AND u.object_auth_name=t.auth_name AND u.object_code=t.code
                        LEFT JOIN extent e ON e.auth_name=u.extent_auth_name AND e.code=u.extent_code
                        LEFT JOIN axis ax ON ax.coordinate_system_auth_name=t.coordinate_system_auth_name
                          AND ax.coordinate_system_code=t.coordinate_system_code AND ax.coordinate_system_order=1
                        LEFT JOIN unit_of_measure un ON un.auth_name=ax.uom_auth_name AND un.code=ax.uom_code
                        WHERE t.auth_name='EPSG' AND t.deprecated=0
                        {(table == "geodetic_crs" ? "AND t.type LIKE 'geographic %'" : "")}
                        ORDER BY CAST(t.code AS INTEGER)";

                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        if (!int.TryParse(rd.GetString(0), out var code) || code <= 0) continue;
                        var name = rd.GetString(1);
                        var area = rd.GetString(2);
                        var unit = rd.GetString(3);
                        var vertical = table == "vertical_crs";
                        var projected = table == "projected_crs";

                        var categoryPath = ClassifyArcGisCategory(projected, vertical, name, area);

                        var result = new CrsDefinition(code, name, area, projected, unit, categoryPath)
                        {
                            IsVerticalOnly = vertical,
                            Datum = "See EPSG definition",
                            Ellipsoid = "See EPSG definition",
                            Wkt = rd.GetString(4)
                        };
                        if (!results.Any(x => x.EpsgCode == code)) results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PROJ CRS catalog unavailable: " + ex.Message);
                results.Clear();
            }
            return results;
        }

        private static IReadOnlyList<CrsDefinition> LoadGdalCrsCatalog()
        {
            var results = new List<CrsDefinition>();
            try
            {
                GdalConfiguration.EnsureInitialized();
                if (!GdalConfiguration.IsAvailable) return results;
                int count;
                CRSInfoList catalog = Osr.GetCRSInfoListFromDatabase("EPSG", out count);
                if (catalog == null) return results;

                var seen = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    CRSInfo info = catalog[i];
                    if (info == null || !int.TryParse(info.code, out var code) || code <= 0 || !seen.Add(code))
                        continue;

                    var deprecated = info.deprecated.ToString() ?? "";
                    if (deprecated == "1" || deprecated.Equals("true", StringComparison.OrdinalIgnoreCase)) continue;

                    var kind = info.type.ToString() ?? "";
                    var projected = kind.Contains("PROJECTED", StringComparison.OrdinalIgnoreCase);
                    var vertical = kind.Contains("VERTICAL", StringComparison.OrdinalIgnoreCase);
                    var geographic = kind.Contains("GEOGRAPHIC", StringComparison.OrdinalIgnoreCase);
                    if (!projected && !vertical && !geographic) continue;

                    var name = info.name ?? "EPSG:" + code;
                    var area = info.area_name ?? "World";
                    var categoryPath = ClassifyArcGisCategory(projected, vertical, name, area);

                    results.Add(new CrsDefinition(code, name, area, projected,
                        geographic ? "degree" : "See EPSG definition", categoryPath)
                    {
                        IsVerticalOnly = vertical,
                        Datum = "See EPSG definition",
                        Ellipsoid = "See EPSG definition",
                        ProjectionMethod = info.projection_method ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GDAL CRS enumeration unavailable: " + ex.Message);
                results.Clear();
            }
            return results;
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
                return _crsList.Where(c => c.EpsgCode == 32636 || c.EpsgCode == 32635 || c.EpsgCode == 22992 || c.EpsgCode == 22993 || c.EpsgCode == 22994 || c.EpsgCode == 22995 || c.EpsgCode == 4229);
            }
            return _crsList;
        }
    }
}
