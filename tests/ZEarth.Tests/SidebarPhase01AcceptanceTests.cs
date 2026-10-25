using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Domain.Kml;
using ZEarth.Domain.Layers;
using ZEarth.Domain.Imagery;
using ZEarth.Domain.Search;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class SidebarPhase01AcceptanceTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZEarth_SidebarTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void SB01_Sidebar_Hierarchy_ThreeTopLevelSections_AreConfigured()
        {
            // Search, My Places and Reference Layers are top-level. Saved Scenes and
            // Temporary Places are protected, initially empty folders under My Places in the UI.
            var project = ProjectDocument.CreateDefault();
            var scenesFolder = project.MyPlaces.FirstOrDefault(p => p.Name == "Saved Scenes" || p.Id == "folder_saved_scenes");
            var temporaryFolder = project.MyPlaces.FirstOrDefault(p => p.Name == "Temporary Places" || p.Id == "folder_temporary_places");

            Assert.IsNotNull(scenesFolder, "Saved Scenes must be a folder inside My Places");
            Assert.IsTrue(scenesFolder.IsProtected, "Saved Scenes folder must be protected from deletion");
            Assert.AreEqual(project.MyPlaces[0], scenesFolder, "Saved Scenes folder must be the first child in My Places");

            Assert.AreEqual(0, scenesFolder.Children.Count, "A clean project must not contain seeded scenes.");
            Assert.IsNotNull(temporaryFolder, "Temporary Places must be a root folder inside My Places");
            Assert.IsTrue(temporaryFolder.IsProtected, "Temporary Places folder must be protected from deletion");
            Assert.AreEqual(project.MyPlaces[1], temporaryFolder, "Temporary Places must be directly below Saved Scenes");
            Assert.AreEqual(0, temporaryFolder.Children.Count, "Temporary Places must start empty.");
            Assert.AreEqual(0, project.TemporaryPlaces.Count, "No separate Temporary Places collection should be used by the UI.");
        }

        [TestMethod]
        public async Task SB02_SearchByName_LocalFeaturesAndArabic_MatchedFirst()
        {
            // Requirement 2 & SB-02, SB-03, SB-04: By Name search with Arabic support and local priority
            var project = ProjectDocument.CreateDefault();
            var arabicPlacemark = new PlaceItem("القاهرة - برج القاهرة", PlaceItemType.Placemark, 30.0459, 31.2243, 187);
            project.MyPlaces.Add(arabicPlacemark);

            var catalog = new ReferenceLayerCatalog();
            var searchService = new SearchService();

            // 1. Search in Arabic
            var results = await searchService.SearchByNameAsync("القاهرة", project.MyPlaces, catalog.Layers);
            Assert.IsTrue(results.Count > 0, "Arabic search query must return results");
            Assert.IsTrue(results.Any(r => r.Title.Contains("القاهرة")), "Result list must include Arabic placemark");
            var firstResult = results.First();
            Assert.IsTrue(firstResult.IsLocal, "Local My Places items must appear with priority");
            Assert.AreEqual("Local", firstResult.SourceBadge);

            // 2. Search local Pyramids
            var pyramidResults = await searchService.SearchByNameAsync("Pyramids", project.MyPlaces, catalog.Layers);
            Assert.IsTrue(pyramidResults.Count > 0);
            Assert.IsTrue(pyramidResults.Any(r => r.Title.Contains("Pyramids")));
        }

        [TestMethod]
        public void SB05_SearchByCoordinates_DdAndDms_ParsesCorrectly()
        {
            // Requirement 2 & SB-05: Coordinate parsing in DD and DMS
            var searchService = new SearchService();

            // 1. Decimal Degrees (X = Lon 31.2357, Y = Lat 30.0444)
            var ddResult = searchService.SearchByCoordinates("31.2357", "30.0444", 4326);
            Assert.IsNotNull(ddResult);
            Assert.AreEqual(31.2357, ddResult.Longitude, 0.0001);
            Assert.AreEqual(30.0444, ddResult.Latitude, 0.0001);

            // 2. DMS Strings (31° 14' 8.52" E, 30° 2' 39.84" N)
            var dmsResult = searchService.SearchByCoordinates("31° 14' 8.52\" E", "30° 2' 39.84\" N", 4326);
            Assert.IsNotNull(dmsResult);
            Assert.AreEqual(31.2357, dmsResult.Longitude, 0.001);
            Assert.AreEqual(30.0444, dmsResult.Latitude, 0.001);
        }

        [TestMethod]
        public void SB06_SearchByCoordinates_ProjectedCrs_ReprojectsCorrectly()
        {
            // Requirement 2 & SB-06: Projected Easting/Northing (X, Y) transformed via PROJ
            var searchService = new SearchService();

            // 1. EPSG:32636 (UTM Zone 36N, Cairo Citadel: Easting 328754.21, Northing 3325189.62)
            var utmResult = searchService.SearchByCoordinates("328754.21", "3325189.62", 32636);
            Assert.IsNotNull(utmResult);
            Assert.AreEqual(31.2238, utmResult.Longitude, 0.001);
            Assert.AreEqual(30.0458, utmResult.Latitude, 0.001);

            // 2. EPSG:22992 (Egypt Red Belt: Easting 636580, Northing 815120)
            var redBeltResult = searchService.SearchByCoordinates("636580.0", "815120.0", 22992);
            Assert.IsNotNull(redBeltResult);
            Assert.AreEqual(31.2254, redBeltResult.Longitude, 0.001);
            Assert.AreEqual(30.0462, redBeltResult.Latitude, 0.001);
        }

        [TestMethod]
        public void SB07_SearchByCoordinates_Validation_EnforcesXAboveYAndValidRanges()
        {
            // Requirement 2 & SB-07, SB-08: Validation of out of bounds and inverted coordinates
            var searchService = new SearchService();

            // Out of bounds latitude (> 90)
            try
            {
                searchService.SearchByCoordinates("31.2357", "95.0000", 4326);
                Assert.Fail("Latitude > 90 must throw CoordinateValidationException");
            }
            catch (CoordinateValidationException ex)
            {
                StringAssert.Contains(ex.Message, "Latitude");
            }

            // Out of bounds longitude (> 180)
            try
            {
                searchService.SearchByCoordinates("195.0000", "30.0444", 4326);
                Assert.Fail("Longitude > 180 must throw CoordinateValidationException");
            }
            catch (CoordinateValidationException ex)
            {
                StringAssert.Contains(ex.Message, "Longitude");
            }

            // Empty inputs
            try
            {
                searchService.SearchByCoordinates("", "", 4326);
                Assert.Fail("Empty inputs must throw CoordinateValidationException");
            }
            catch (CoordinateValidationException ex)
            {
                StringAssert.Contains(ex.Message, "cannot be empty");
            }
        }

        [TestMethod]
        public void SB09_MyPlaces_StartupScene_ContextCommand_PromotesToStartup()
        {
            // Requirement 3 & SB-09, SB-10: Set as Startup Scene moves scene to top and marks it
            var project = ProjectDocument.CreateDefault();
            var scenesFolder = project.EnsureSavedScenesFolder();

            var oldScene = new PlaceItem("Old Scene", new CameraState(30, 31, 10000, 0, -45, 0));
            scenesFolder.AddChild(oldScene);

            var newScene = new PlaceItem("New Alex Coast Scene", PlaceItemType.Placemark, 31.2001, 29.9187, 10000)
            {
                Camera = new CameraState(31.2001, 29.9187, 10000, 0, -45, 0)
            };
            scenesFolder.AddChild(newScene);

            // Simulate SetAsStartupSceneCommand logic
            int idx = scenesFolder.Children.IndexOf(newScene);
            if (idx > 0)
            {
                scenesFolder.Children.Move(idx, 0);
            }
            project.SyncSavedScenes();

            Assert.AreEqual(newScene, scenesFolder.Children[0]);
            Assert.IsTrue(newScene.IsStartupScene);
            Assert.IsFalse(scenesFolder.Children[1].IsStartupScene);
        }

        [TestMethod]
        public void SB11_TemporaryPlaces_DurableSessionStore_PersistsWithoutProjectDirtying()
        {
            // Requirement 4 & SB-11, SB-12, SB-13: Durable temporary session persistence
            var sessionFile = Path.Combine(_tempDir, "temporary_session.json");
            var sessionService = new TemporarySessionService(sessionFile);

            var tempCollection = new List<PlaceItem>
            {
                new PlaceItem("Imported Flight Path.kml", PlaceItemType.Folder)
                {
                    Children =
                    {
                        new PlaceItem("Waypoint Alpha", PlaceItemType.Placemark, 30.1, 31.2, 500)
                    }
                }
            };

            // Save session
            sessionService.SaveSession(tempCollection);
            Assert.IsTrue(File.Exists(sessionFile));

            // Load session
            var loaded = sessionService.LoadSession();
            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("Imported Flight Path.kml", loaded[0].Name);
            Assert.AreEqual(1, loaded[0].Children.Count);
            Assert.AreEqual("Waypoint Alpha", loaded[0].Children[0].Name);

            // Clean session removal
            sessionService.ClearSession();
            Assert.IsFalse(File.Exists(sessionFile));
        }

        [TestMethod]
        public void SB14_ReferenceLayers_NineMandatoryCategories_AndRealLayersExist()
        {
            // Current nine categories after intentional 2.1 reference layer taxonomy cleanup.
            var catalog = new ReferenceLayerCatalog();
            var grouped = catalog.GetGroupedCategories();

            var expectedCategories = new[]
            {
                "Borders & Labels",
                "Places & Geographic Names",
                "Roads & Transportation",
                "Weather",
                "Gallery & Knowledge",
                "Water & Hydrology",
                "Parks & Recreation",
                "Buildings & Infrastructure",
                "Natural Hazards"
            };

            Assert.AreEqual(9, grouped.Count);
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(expectedCategories[i], grouped[i].CategoryName, $"Category {i} mismatch");
            }

            // Real Natural Earth Layers check
            var borders = catalog.FindById("ref_borders_coastlines");
            Assert.IsNotNull(borders);
            Assert.IsTrue(borders.IsConfigured);
            Assert.IsFalse(string.IsNullOrEmpty(borders.Url));

            var places = catalog.FindById("ref_places_names");
            Assert.IsNotNull(places);
            Assert.IsTrue(places.IsConfigured);

            // Real NASA GIBS Earth Observation Layer check
            var gibs = catalog.FindById("ref_earth_observation_gibs");
            Assert.IsNotNull(gibs);
            Assert.IsTrue(gibs.IsConfigured);
            StringAssert.Contains(gibs.Url, "gibs.earthdata.nasa.gov");
            Assert.IsFalse(string.IsNullOrEmpty(gibs.Attribution));

            // All 9 categories now have active online providers
            var weather = grouped[3].Layers.First();
            Assert.IsTrue(weather.IsConfigured);
            Assert.AreEqual(ReferenceLayerStatus.Active, weather.Status);
            StringAssert.Contains(weather.StatusMessage, "Online");
        }

        [TestMethod]
        public void SB17_HtmlSanitizer_StripsMaliciousPayloads_PreservesSafeContent()
        {
            // Requirement 6 & SB-17: HTML sanitization in Description tab
            string dirtyHtml = @"
                <div>
                    <h3>Cairo Historical Landmark</h3>
                    <p>Built in <b>1869</b> with <i>distinctive architecture</i>.</p>
                    <script>alert('XSS Attack!');</script>
                    <iframe src='http://malicious.example.com'></iframe>
                    <object data='exploit.swf'></object>
                    <a href='http://example.com' onclick='stealCookies()'>Official Site</a>
                    <a href='javascript:alert(1)'>Click me</a>
                    <a href='file:///C:/Windows/System32/cmd.exe'>Run Cmd</a>
                </div>
            ";

            string clean = HtmlSanitizer.Sanitize(dirtyHtml);

            // Assert malicious elements stripped
            Assert.IsFalse(clean.Contains("<script"));
            Assert.IsFalse(clean.Contains("<iframe"));
            Assert.IsFalse(clean.Contains("<object"));
            Assert.IsFalse(clean.Contains("onclick"));
            Assert.IsFalse(clean.Contains("javascript:"));
            Assert.IsFalse(clean.Contains("file:"));

            // Assert safe elements preserved
            StringAssert.Contains(clean, "<h3>Cairo Historical Landmark</h3>");
            StringAssert.Contains(clean, "<b>1869</b>");
            StringAssert.Contains(clean, "<i>distinctive architecture</i>");
            StringAssert.Contains(clean, "<a href='http://example.com'>Official Site</a>");

            // Test safe wrapping
            string wrapped = HtmlSanitizer.WrapInSafeDocument(clean);
            StringAssert.Contains(wrapped, "<base target=\"_blank\">");
        }

        [TestMethod]
        public void SB18_ExtendedData_AttributesPreservation_AndUnicode()
        {
            // Requirement 6 & SB-18: ExtendedData / SchemaData structured editing
            var item = new PlaceItem("Test Parcel", PlaceItemType.Polygon);
            item.ExtendedData["ParcelID"] = "EG-CAI-2026-99";
            item.ExtendedData["Owner_Arabic"] = "وزارة الإسكان والمرافق والمجتمعات العمرانية";
            item.ExtendedData["Area_Sqm"] = "1450.75";

            Assert.AreEqual(3, item.ExtendedData.Count);
            Assert.AreEqual("EG-CAI-2026-99", item.ExtendedData["ParcelID"]);
            Assert.AreEqual("وزارة الإسكان والمرافق والمجتمعات العمرانية", item.ExtendedData["Owner_Arabic"]);

            var clone = item.Clone();
            Assert.AreEqual(item.ExtendedData["Owner_Arabic"], clone.ExtendedData["Owner_Arabic"]);
        }

        [TestMethod]
        public void SB19_PlaceItem_GlyphAndEffectiveColorHex_WorkAccurately()
        {
            var pMark = new PlaceItem("Point 1", PlaceItemType.Placemark) { ColorHex = "#EF4444" };
            Assert.AreEqual("●", pMark.Glyph);
            Assert.AreEqual("#EF4444", pMark.EffectiveColorHex);

            var pPath = new PlaceItem("Line 1", PlaceItemType.Path) { ColorHex = "#3B82F6" };
            Assert.AreEqual("〰", pPath.Glyph);
            Assert.AreEqual("#3B82F6", pPath.EffectiveColorHex);

            var pPoly = new PlaceItem("Polygon 1", PlaceItemType.Polygon) { ColorHex = "#10B981" };
            Assert.AreEqual("⬡", pPoly.Glyph);
            Assert.AreEqual("#10B981", pPoly.EffectiveColorHex);

            var pFolder = new PlaceItem("Folder 1", PlaceItemType.Folder);
            Assert.AreEqual("📁", pFolder.Glyph);
            Assert.AreEqual("#EAB308", pFolder.EffectiveColorHex);

            var pStartup = new PlaceItem("Startup Scene", PlaceItemType.Placemark) { IsStartupScene = true };
            Assert.AreEqual("★", pStartup.Glyph);
            Assert.AreEqual("#F59E0B", pStartup.EffectiveColorHex);
        }

        [TestMethod]
        public void SB20_ReferenceLayerCatalog_AllNineCategories_ActiveAndConfigured()
        {
            var catalog = new ReferenceLayerCatalog();
            var grouped = catalog.GetGroupedCategories();
            Assert.AreEqual(9, grouped.Count);

            foreach (var group in grouped)
            {
                Assert.IsTrue(group.Layers.Count > 0, $"Category {group.CategoryName} must have layers");
                foreach (var layer in group.Layers)
                {
                    Assert.IsTrue(layer.IsConfigured, $"Layer {layer.Name} in {group.CategoryName} must be configured");
                    Assert.AreEqual(ReferenceLayerStatus.Active, layer.Status, $"Layer {layer.Name} must be active");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(layer.Url), $"Layer {layer.Name} must have a non-empty URL");
                }
            }
        }

        [TestMethod]
        public void SB21_ImageryRegistry_HasCategorizedGlobalBasemapsAndNoUnofficialGoogleTiles()
        {
            var registry = new ImageryRegistry();
            var basemaps = registry.Basemaps.ToList();

            Assert.IsTrue(basemaps.Any(b => b.Id == ImageryRegistry.ProviderOsmHumanitarian));
            Assert.IsTrue(basemaps.Any(b => b.Id == ImageryRegistry.ProviderCyclOsm));
            Assert.IsTrue(basemaps.Any(b => b.Id == ImageryRegistry.ProviderEsriPhysical && b.Category == BasemapCategory.Geology));
            Assert.IsTrue(basemaps.Any(b => b.Id == ImageryRegistry.ProviderEsriOcean && b.Category == BasemapCategory.Ocean));
            Assert.IsFalse(basemaps.Any(b => b.Id.Contains("google", StringComparison.OrdinalIgnoreCase)));

            var esriHybrid = basemaps.FirstOrDefault(b => b.Id == ImageryRegistry.ProviderEsriHybrid);
            Assert.IsNotNull(esriHybrid, "Esri Hybrid basemap must be registered");
            Assert.AreEqual(BasemapCategory.Hybrid, esriHybrid.Category);
        }
    }
}
