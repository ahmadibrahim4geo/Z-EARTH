using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Data.Sqlite;
using ZEarth.Domain.Gis;

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
                if (!_byEpsg.ContainsKey(item.EpsgCode)) Register(item);
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

        // PROJ is shipped by the GDAL runtime in supported Windows builds. The
        // catalog is read offline and cached; if missing, the caller can visibly
        // identify the limited curated fallback rather than fabricating CRS entries.
        private static readonly Lazy<IReadOnlyList<CrsDefinition>> ProjCatalog = new(LoadProjCatalog);
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
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "share", "proj", "proj.db"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "share", "proj", "proj.db"));
            foreach (var file in candidates)
                if (File.Exists(file)) return file;
            try
            {
                var deployed = Directory.EnumerateFiles(AppContext.BaseDirectory, "proj.db", SearchOption.AllDirectories).FirstOrDefault();
                if (deployed != null) return deployed;
                // NuGet development/test fallback only; deployed EXEs must carry their own proj.db.
                var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
                if (string.IsNullOrWhiteSpace(nugetRoot))
                    nugetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
                var corePackage = Path.Combine(nugetRoot, "maxrev.gdal.core");
                return Directory.Exists(corePackage)
                    ? Directory.EnumerateFiles(corePackage, "proj.db", SearchOption.AllDirectories).FirstOrDefault()
                    : null;
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
            // A country/region without verified classification is kept searchable
            // without falsely assigning it to a continent or an official Esri path.
            return "Other Areas/" + (area.Split(" - ")[0].Trim().Replace("/", " – "));
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
                    // All table identifiers come from our fixed internal allowlist.
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
                        var prefix = vertical ? "Vertical Coordinate Systems" :
                            projected ? "Projected Coordinate Systems" : "Geographic Coordinate Systems";
                        var group = projected && name.Contains("UTM zone", StringComparison.OrdinalIgnoreCase)
                            ? "World/UTM Zone Series" : AreaFolder(area);
                        var result = new CrsDefinition(code, name, area, projected, unit, prefix + "/" + group)
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
                results.Clear(); // never pass off partial data as a full catalog
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
                return _crsList.Where(c => c.EpsgCode == 32636 || c.EpsgCode == 32635 || c.EpsgCode == 22992 || c.EpsgCode == 22993 || c.EpsgCode == 22994 || c.EpsgCode == 4229);
            }
            return _crsList;
        }
    }
}
