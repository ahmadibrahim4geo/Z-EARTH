using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZEarth.App.Services
{
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event EventHandler<string>? LanguageChanged;

        private string _currentLanguage = "en";
        public string CurrentLanguage => _currentLanguage;
        public bool IsArabic => _currentLanguage == "ar";

        private readonly Dictionary<string, string> _arStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Title"] = "زد إيرث لنظم المعلومات الجغرافية - م/ أحمد إبراهيم",
            ["Tab.Project"] = "المشروع",
            ["Tab.View"] = "العرض",
            ["Tab.Drawing"] = "الرسم",
            ["Tab.Measurement"] = "القياس",
            ["Tab.Layers"] = "الطبقات",
            ["Tab.Terrain"] = "التضاريس",
            ["Tab.Tools"] = "الأدوات",
            ["Tab.Help"] = "مساعدة",

            ["Nav.Home"] = "الرئيسية",
            ["Nav.FullExtent"] = "الامتداد الكامل",
            ["Nav.ZoomIn"] = "تكبير",
            ["Nav.ZoomOut"] = "تصغير",
            ["Nav.NorthUp"] = "شمال للأعلى",
            ["Nav.ResetPitch"] = "إعادة ضبط الميل",
            ["Nav.3DGlobe"] = "كرة أرضية 3D",
            ["Nav.2DMap"] = "خريطة مستوية 2D",

            ["Places.MyPlaces"] = "📁 أماكني (My Places)",
            ["Places.Temporary"] = "📁 أماكن مؤقتة (Temporary)",
            ["Places.SavedScenes"] = "🎬 المشاهد المحفوظة (Scenes)",
            ["Places.AddPlacemark"] = "إضافة علامة موضع",
            ["Places.AddFolder"] = "إضافة مجلد",
            ["Places.AddPath"] = "إضافة مسار",
            ["Places.AddPolygon"] = "إضافة مضلع",
            ["Places.Delete"] = "حذف العنصر",

            ["Measure.Distance"] = "قياس المسافة",
            ["Measure.Area"] = "قياس المساحة",
            ["Measure.Clear"] = "مسح القياسات",
            ["Measure.Snapping"] = "تفعيل المحاذاة (Snapping)",

            ["Layers.Basemap"] = "خريطة الأساس",
            ["Layers.Offline"] = "متجهات غير متصلة (Natural Earth)",
            ["Layers.OSM"] = "أوبن ستريت ماب القياسية",
            ["Layers.OpenTopo"] = "تضاريس طبوغرافية (OpenTopo)",
            ["Layers.Dark"] = "كارتو دي بي الداكنة (Dark Matter)",
            ["Layers.AddLayer"] = "إضافة طبقة خرائط",

            ["Terrain.Model"] = "نموذج الارتفاع الرقمي",
            ["Terrain.Exaggeration"] = "مبالغة التضاريس الرأسية",
            ["Terrain.Reset"] = "إعادة ضبط (1x)",

            ["Telemetry.Coordinates"] = "الإحداثيات",
            ["Telemetry.Altitude"] = "ارتفاع العين",
            ["Telemetry.Heading"] = "الاتجاه",
            ["Telemetry.CRS"] = "نظام الإسناد الجغرافي"
        };

        private readonly Dictionary<string, string> _enStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Title"] = "Z EARTH Desktop GIS - Developed by Ahmad Ibrahim",
            ["Tab.Project"] = "Project",
            ["Tab.View"] = "View",
            ["Tab.Drawing"] = "Drawing",
            ["Tab.Measurement"] = "Measurement",
            ["Tab.Layers"] = "Layers",
            ["Tab.Terrain"] = "Terrain",
            ["Tab.Tools"] = "Tools",
            ["Tab.Help"] = "Help",

            ["Nav.Home"] = "Home",
            ["Nav.FullExtent"] = "Full Extent",
            ["Nav.ZoomIn"] = "Zoom In",
            ["Nav.ZoomOut"] = "Zoom Out",
            ["Nav.NorthUp"] = "North Up",
            ["Nav.ResetPitch"] = "Reset Pitch",
            ["Nav.3DGlobe"] = "3D Globe",
            ["Nav.2DMap"] = "2D Map",

            ["Places.MyPlaces"] = "📁 My Places",
            ["Places.Temporary"] = "📁 Temporary Places",
            ["Places.SavedScenes"] = "🎬 Saved Scenes",
            ["Places.AddPlacemark"] = "Add Placemark",
            ["Places.AddFolder"] = "Add Folder",
            ["Places.AddPath"] = "Add Path",
            ["Places.AddPolygon"] = "Add Polygon",
            ["Places.Delete"] = "Delete Item",

            ["Measure.Distance"] = "Measure Distance",
            ["Measure.Area"] = "Measure Area",
            ["Measure.Clear"] = "Clear Measurements",
            ["Measure.Snapping"] = "Enable Snapping",

            ["Layers.Basemap"] = "Basemap Gallery",
            ["Layers.Offline"] = "Natural Earth Vector (Offline)",
            ["Layers.OSM"] = "OpenStreetMap Standard",
            ["Layers.OpenTopo"] = "OpenTopoMap Topographic Relief",
            ["Layers.Dark"] = "CartoDB Dark Matter",
            ["Layers.AddLayer"] = "Add Imagery Layer",

            ["Terrain.Model"] = "Elevation Model",
            ["Terrain.Exaggeration"] = "Vertical Exaggeration",
            ["Terrain.Reset"] = "Reset (1.0x)",

            ["Telemetry.Coordinates"] = "Coordinates",
            ["Telemetry.Altitude"] = "Eye Alt",
            ["Telemetry.Heading"] = "Head",
            ["Telemetry.CRS"] = "CRS"
        };

        private readonly Dictionary<string, string> _arByEnglish = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, object>> _originalValues = new();

        private LocalizationService()
        {
            foreach (var (key, english) in _enStrings)
                if (_arStrings.TryGetValue(key, out var arabic)) _arByEnglish[english] = arabic;

            AddTranslations(
                ("Navigation", "التنقل"), ("Basemap", "خريطة الأساس"),
                ("Historical Imagery", "الصور التاريخية"), ("Project File", "ملف المشروع"),
                ("New", "جديد"), ("Open", "فتح"), ("Save", "حفظ"), ("Save As", "حفظ باسم"),
                ("History", "السجل"), ("Undo", "تراجع"), ("Redo", "إعادة"),
                ("Interchange", "تبادل البيانات"), ("Import KML", "استيراد KML"),
                ("Export KML", "تصدير KML"), ("Export KMZ", "تصدير KMZ"),
                ("Extent History", "سجل النطاق"), ("Previous", "السابق"), ("Next", "التالي"),
                ("Fit View", "ملاءمة العرض"), ("Camera Controls", "تحكم الكاميرا"),
                ("North Up", "الشمال لأعلى"), ("Nadir Pitch", "منظور رأسي"),
                ("Projection Mode", "نمط الإسقاط"), ("Reference System", "النظام المرجعي"),
                ("Map CRS", "نظام إسناد الخريطة"), ("Map Sources", "مصادر الخريطة"),
                ("Base Map", "خريطة الأساس"), ("Political Borders", "الحدود السياسية"),
                ("Appearance", "المظهر"), ("Theme", "السمة"), ("Panels", "اللوحات"),
                ("Sidebar", "الشريط الجانبي"), ("Dock", "موضع التثبيت"),
                ("Coordinate Grid", "شبكة الإحداثيات"), ("Show Grid", "إظهار الشبكة"),
                ("Grid Options", "خيارات الشبكة"), ("Format Units", "تنسيق الوحدات"),
                ("Lighting & Atmosphere", "الإضاءة والغلاف الجوي"), ("Sunlight", "ضوء الشمس"),
                ("Atmosphere", "الغلاف الجوي"), ("Time of Day", "وقت اليوم"),
                ("Compass Mode", "نمط البوصلة"), ("Cardinal", "الاتجاهات الأصلية"),
                ("Heading", "الاتجاه"), ("Qibla", "القبلة"),
                ("Geometry", "الأشكال الهندسية"), ("Placemark", "علامة موضع"),
                ("Path", "مسار"), ("Polygon", "مضلع"), ("Rectangle", "مستطيل"),
                ("Circle", "دائرة"), ("Overlay", "صورة متراكبة"),
                ("3D Entities", "عناصر ثلاثية الأبعاد"), ("3D Model", "نموذج ثلاثي الأبعاد"),
                ("Geodesic Tools", "أدوات جيوديسية"), ("Distance", "المسافة"),
                ("Area", "المساحة"), ("Clear", "مسح"), ("Profile", "القطاع الرأسي"),
                ("Snapping Engine", "محرك الالتقاط"), ("Enable Snapping", "تفعيل الالتقاط"),
                ("Vertex / Endpoints", "الرؤوس ونقاط النهاية"),
                ("Segment Midpoints", "منتصف المقاطع"), ("Edge Nearest Projection", "أقرب إسقاط على الحافة"),
                ("Ground Tol:", "سماحية الأرض:"), ("Telemetry", "بيانات القياس"),
                ("GEODESIC METRICS (WGS 84)", "قياسات جيوديسية (WGS 84)"),
                ("Basemap Gallery", "معرض خرائط الأساس"), ("Browse All", "استعراض الكل"),
                ("Custom Services", "خدمات مخصصة"), ("Add Service", "إضافة خدمة"),
                ("Esri Wayback Release Timeline", "الخط الزمني لإصدارات Esri Wayback"),
                ("Show Timeline", "إظهار الخط الزمني"),
                ("Satellite Observation Dates (NASA)", "تواريخ الرصد الفضائي (NASA)"),
                ("Dated MODIS", "صور MODIS المؤرخة"), ("Hide NASA", "إخفاء طبقة NASA"),
                ("Elevation Model", "نموذج الارتفاع"), ("Selected Elevation Model:", "نموذج الارتفاع المحدد:"),
                ("Exaggeration:", "المبالغة الرأسية:"), ("Reset", "إعادة الضبط"),
                ("Vertical Exaggeration (AC-019)", "المبالغة الرأسية للتضاريس"),
                ("Vector Data", "بيانات متجهة"), ("GPS", "نظام GPS"),
                ("Utilities", "أدوات مساعدة"), ("Coord Conv", "تحويل الإحداثيات"),
                ("Snapshot", "لقطة خريطة"), ("Options", "الخيارات"),
                ("Information", "معلومات"), ("Diagnostics", "التشخيصات"),
                ("About", "حول البرنامج"), ("EXPLORER", "المستكشف"),
                ("Search", "البحث"), ("By Name", "بالاسم"), ("By Coordinates", "بالإحداثيات"),
                ("Target CRS / Projection:", "نظام الإسناد / الإسقاط الهدف:"),
                ("Clear results", "مسح النتائج"), ("My Places", "أماكني"),
                ("[Persistent]", "[دائمة]"), ("Explorer Places", "أماكن المستكشف"),
                ("[Saved and session]", "[محفوظة وللجلسة]"),
                ("Import KML / KMZ as an Explorer root", "استيراد KML / KMZ كعنصر رئيسي في المستكشف"),
                ("Add Folder", "إضافة مجلد"),
                ("Delete", "حذف"), ("Fly To", "انتقال إلى"),
                ("Save to My Places", "حفظ في أماكني"), ("Expand All Sections", "توسيع كل الأقسام"),
                ("Collapse All Sections", "طي كل الأقسام"), ("Active Basemap", "خريطة الأساس النشطة"),
                ("Working…", "جارٍ العمل…"), ("Developed by ", "طوّره "),
                ("Appearance", "المظهر"), ("Zoom In", "تكبير"), ("Zoom Out", "تصغير"),
                ("Full Extent", "الامتداد الكامل"), ("3D Globe", "كرة أرضية ثلاثية الأبعاد"),
                ("2D Map", "خريطة ثنائية الأبعاد"), ("Save Active Project", "حفظ المشروع النشط"),
                ("Import KML/KMZ...", "استيراد KML/KMZ..."),
                ("Export All My Places...", "تصدير كل أماكني..."),
                ("Export Selected Item...", "تصدير العنصر المحدد..."),
                ("Add Saved Scene", "إضافة مشهد محفوظ"), ("Properties...", "الخصائص..."),
                ("Set as Startup Scene", "تعيين كمشهد بدء التشغيل"),
                ("Move Saved Scene Up", "نقل المشهد المحفوظ لأعلى"),
                ("Move Saved Scene Down", "نقل المشهد المحفوظ لأسفل"),
                ("Play GPS", "تشغيل مسار GPS"), ("Stop GPS", "إيقاف مسار GPS"),
                ("My Device Location (permission required)", "موقع جهازي (يتطلب إذنًا)"),
                ("Locate / Fly To", "تحديد الموقع والانتقال"),
                ("Searches My Places + Temporary first, then online Photon.", "يبحث أولًا في أماكني والمؤقتة، ثم يستخدم Photon عبر الإنترنت."),
                ("CRS:", "نظام الإسناد:"), ("Eye:", "ارتفاع العين:"), ("Head:", "الاتجاه:")
            );

            // Options & GIS Settings workspace. Product/provider names and CRS identifiers
            // remain stable; descriptive UI text is fully localized and follows RTL layout.
            AddTranslations(
                ("Options & GIS Settings", "الخيارات وإعدادات نظم المعلومات الجغرافية"),
                ("Z EARTH Options & GIS Configuration", "خيارات Z EARTH وإعدادات نظم المعلومات الجغرافية"),
                ("Configure measurement units, spatial snapping, basemaps, terrain, grid, and display preferences.", "اضبط وحدات القياس والالتقاط المكاني وخرائط الأساس والتضاريس والشبكة وتفضيلات العرض."),
                ("Measurement Units", "وحدات القياس"), ("Spatial Snapping", "الالتقاط المكاني"),
                ("Basemap & Display", "خريطة الأساس والعرض"), ("Terrain Models", "نماذج التضاريس"),
                ("Map Grid", "شبكة الخريطة"), ("Save Settings", "حفظ الإعدادات"), ("Cancel", "إلغاء"),
                ("Changes are applied after Save Settings. Terrain and Grid visibility remain controlled from the ribbon.", "تُطبّق التغييرات بعد حفظ الإعدادات. ويظل إظهار التضاريس والشبكة تحت تحكم الشريط الرئيسي."),
                ("Choose how geodesic distance, surface area, and elevation values are displayed throughout Z EARTH.", "اختر طريقة عرض المسافات الجيوديسية والمساحات والارتفاعات في جميع أجزاء Z EARTH."),
                ("Distance", "المسافة"), ("Area", "المساحة"), ("Elevation", "الارتفاع"),
                ("Geodesic distance units", "وحدات المسافة الجيوديسية"), ("Surface area units", "وحدات المساحة السطحية"),
                ("Elevation units", "وحدات الارتفاع"),
                ("Used by ruler, path length, camera altitude and navigation readouts.", "تُستخدم في المسطرة وطول المسار وارتفاع الكاميرا وقراءات التنقل."),
                ("Used by polygon measurement and saved area results.", "تُستخدم في قياس المضلعات ونتائج المساحات المحفوظة."),
                ("Used by terrain samples, profiles, placemark altitude and eye height.", "تُستخدم في عينات التضاريس والقطاعات الرأسية وارتفاع العلامات وارتفاع العين."),
                ("Measurements keep their geodesic WGS 84 values internally. These choices change display and export formatting; they do not redefine the map CRS or alter stored geometry.", "تحتفظ القياسات داخليًا بقيم WGS 84 الجيوديسية. تغيّر هذه الخيارات تنسيق العرض والتصدير ولا تعيد تعريف نظام إسناد الخريطة أو تعدّل الأشكال المحفوظة."),
                ("Auto Metric (m / km)", "متري تلقائي (م / كم)"), ("Meters (m)", "متر (م)"),
                ("Kilometers (km)", "كيلومتر (كم)"), ("Auto Imperial (ft / mi)", "إمبراطوري تلقائي (قدم / ميل)"),
                ("Feet (ft)", "قدم"), ("Miles (mi)", "ميل"), ("Nautical Miles (nmi)", "ميل بحري"),
                ("Centimeters (cm)", "سنتيمتر"), ("Millimeters (mm)", "ملليمتر"),
                ("Auto Metric (m² / ha / km²)", "متري تلقائي (م² / هكتار / كم²)"),
                ("Square Meters (m²)", "متر مربع (م²)"), ("Square Kilometers (km²)", "كيلومتر مربع (كم²)"),
                ("Hectares (ha)", "هكتار"), ("Egyptian Feddan (4,200.83 m²)", "فدان مصري (4,200.83 م²)"),
                ("Auto Imperial (ft² / ac / mi²)", "إمبراطوري تلقائي (قدم² / فدان / ميل²)"),
                ("Acres (ac)", "فدان إنجليزي"), ("Square Feet (ft²)", "قدم مربع"), ("Square Miles (mi²)", "ميل مربع"),
                ("Control the real ground-distance snapping used by drawing and measurement tools.", "تحكم في الالتقاط وفق المسافة الأرضية الحقيقية المستخدم في أدوات الرسم والقياس."),
                ("Enable snapping", "تفعيل الالتقاط"), ("Snap Behavior", "سلوك الالتقاط"),
                ("Ground tolerance", "سماحية الأرض"), ("Active Snap Targets", "أهداف الالتقاط النشطة"),
                ("Choose which existing geometry may attract the pointer.", "اختر الأشكال الموجودة التي يمكن أن تجذب المؤشر."),
                ("Vertices and endpoints", "الرؤوس ونقاط النهاية"), ("Segment midpoints", "منتصف المقاطع"),
                ("Nearest edge or boundary", "أقرب حافة أو حد"), ("Intersections", "التقاطعات"),
                ("Tolerance is measured geodesically on the WGS 84 surface and remains stable as screen DPI and zoom change.", "تُقاس السماحية جيوديسيًا على سطح WGS 84 وتبقى ثابتة عند تغير دقة الشاشة ومستوى التكبير."),
                ("How snapping behaves", "كيفية عمل الالتقاط"),
                ("When enabled, drawing and measurement tools evaluate the selected targets and use the closest eligible ground position within the configured tolerance. Unsupported layers are ignored rather than reported as snapped.", "عند التفعيل، تقيّم أدوات الرسم والقياس الأهداف المحددة وتستخدم أقرب موضع أرضي مؤهل ضمن السماحية. تُتجاهل الطبقات غير المدعومة بدلًا من الإبلاغ الخاطئ عن التقاطها."),
                ("Set the startup map, interface appearance, attribution, screenshots, and navigation response.", "اضبط خريطة البدء ومظهر الواجهة ونسب المصادر ولقطات الشاشة واستجابة التنقل."),
                ("Startup Basemap", "خريطة الأساس عند البدء"), ("Default basemap", "خريطة الأساس الافتراضية"),
                ("Natural Earth is bundled for offline startup. Online providers remain subject to their availability, terms, and attribution.", "تأتي Natural Earth مضمّنة للبدء دون إنترنت. وتخضع المصادر المتصلة لتوفرها وشروطها ونسبها."),
                ("Interface Appearance", "مظهر الواجهة"), ("Theme", "السمة"), ("Language", "اللغة"),
                ("Dark Charcoal Theme (GIS Optimized)", "سمة فحمية داكنة محسّنة لنظم المعلومات الجغرافية"),
                ("Light Theme (Cartographic High-Contrast)", "سمة فاتحة عالية التباين للخرائط"),
                ("English (US/UK)", "الإنجليزية"), ("العربية (Arabic - RTL)", "العربية"),
                ("Show saved scene locations as map ground markers", "إظهار مواقع المشاهد المحفوظة كعلامات أرضية"),
                ("Credits & Provider Identity", "نسب المصادر وهوية المزود"), ("Attribution display", "عرض نسب المصادر"),
                ("Compact (Horizontal Provider Row + Lightbox Modal)", "مضغوط (صف أفقي للمزودين ونافذة تفاصيل)"),
                ("Expanded (Full In-Viewport Text Attribution)", "موسّع (نص كامل داخل العرض)"),
                ("Show optional provider logos", "إظهار شعارات المزودين الاختيارية"),
                ("Required text credits remain visible even when optional logos are hidden.", "تظل نسب المصادر النصية المطلوبة ظاهرة حتى عند إخفاء الشعارات الاختيارية."),
                ("Output & Navigation", "الإخراج والتنقل"), ("Screenshot folder", "مجلد لقطات الشاشة"),
                ("Browse…", "استعراض…"), ("Navigation zoom step", "خطوة تكبير التنقل"),
                ("Enter an absolute local folder path for saved map screenshots", "أدخل مسارًا محليًا مطلقًا لحفظ لقطات الخريطة"),
                ("Terrain Models & Bathymetry", "نماذج التضاريس وقياسات الأعماق"),
                ("Choose, configure, and verify the elevation source used when Terrain is enabled from the ribbon.", "اختر مصدر الارتفاع المستخدم عند تفعيل التضاريس من الشريط واضبطه وتحقق منه."),
                ("Not tested", "لم يُختبر"), ("Available Elevation Models", "نماذج الارتفاع المتاحة"),
                ("Default elevation model", "نموذج الارتفاع الافتراضي"),
                ("AWS Terrarium DEM — free, no key", "AWS Terrarium DEM — مجاني، بلا مفتاح"),
                ("Cesium World Terrain — ion token", "Cesium World Terrain — رمز ion"),
                ("Cesium World Bathymetry — ion token", "Cesium World Bathymetry — رمز ion"),
                ("WGS84 Ellipsoid — offline, flat", "مجسم WGS84 — دون إنترنت، مسطح"),
                ("Custom Quantized-Mesh Service", "خدمة Quantized-Mesh مخصصة"),
                ("Global terrain from AWS Open Data; no account or key required.", "تضاريس عالمية من بيانات AWS المفتوحة؛ لا يلزم حساب أو مفتاح."),
                ("Saving this selection does not turn terrain on. The ribbon remains the authoritative Terrain ON/OFF control.", "حفظ هذا الاختيار لا يشغّل التضاريس؛ يظل زر التضاريس في الشريط هو مفتاح التشغيل والإيقاف المعتمد."),
                ("Dataset paths that require preparation", "مسارات بيانات تتطلب تجهيزًا"),
                ("Browse Local DEM / Bathymetry…", "استعراض DEM / أعماق محلية…"),
                ("GEBCO 2026 download guidance", "إرشادات تنزيل GEBCO 2026"),
                ("GeoTIFF and GEBCO grids are numeric elevation data, but Cesium cannot use a raw file as terrain. Prepare and publish terrain tiles, then configure the resulting quantized-mesh URL.", "شبكات GeoTIFF وGEBCO بيانات ارتفاع رقمية، لكن Cesium لا يستخدم الملف الخام كتضاريس. جهّز بلاطات التضاريس وانشرها ثم أدخل عنوان Quantized-Mesh الناتج."),
                ("Source Configuration", "إعداد المصدر"), ("No credentials are required for AWS Terrarium DEM.", "لا يلزم أي رمز وصول لاستخدام AWS Terrarium DEM."),
                ("Cesium ion access token", "رمز وصول Cesium ion"),
                ("Session only: the token is excluded from settings.json. For managed startup, use the ZEARTH_CESIUM_ION_TOKEN environment variable.", "للجلسة الحالية فقط: لا يُحفظ الرمز في settings.json. وللبدء المُدار استخدم متغير البيئة ZEARTH_CESIUM_ION_TOKEN."),
                ("Custom quantized-mesh service URL", "عنوان خدمة Quantized-Mesh المخصصة"),
                ("The HTTPS service root must expose layer.json and Cesium quantized-mesh tiles. Arbitrary file and private-network URLs are rejected.", "يجب أن يعرض جذر خدمة HTTPS ملف layer.json وبلاطات Quantized-Mesh. تُرفض عناوين الملفات والشبكات الخاصة."),
                ("Test Connection", "اختبار الاتصال"), ("Get Cesium ion Token ↗", "الحصول على رمز Cesium ion ↗"),
                ("A connection test uses the embedded Cesium engine and may temporarily apply the selected source to the current globe.", "يستخدم اختبار الاتصال محرك Cesium المضمّن وقد يطبّق المصدر المحدد مؤقتًا على الكرة الحالية."),
                ("Source Help", "مساعدة المصدر"), ("GIS Source Truth", "حقيقة مصدر البيانات"),
                ("• Terrain tiles contain numeric heights and can shape the globe.", "• تحتوي بلاطات التضاريس على ارتفاعات رقمية ويمكنها تشكيل الكرة."),
                ("• WMS and shaded-relief images are 2D imagery, not elevation.", "• صور WMS والتظليل التضاريسي صور ثنائية الأبعاد وليست ارتفاعات."),
                ("• A raw DEM/GeoTIFF needs tiling or conversion before Cesium can render it as terrain.", "• يحتاج DEM/GeoTIFF الخام إلى تبليط أو تحويل قبل أن يعرضه Cesium كتضاريس."),
                ("• GEBCO 2026 is a dataset, not a ready TerrainServer endpoint.", "• GEBCO 2026 مجموعة بيانات وليست نقطة TerrainServer جاهزة."),
                ("Geographic Coordinate Grid", "شبكة الإحداثيات الجغرافية"),
                ("Geographic reference lines, grid lines, coordinate labels and UTM zone boundaries.", "خطوط مرجعية جغرافية وخطوط شبكة وتسميات إحداثيات وحدود نطاقات UTM."),
                ("Reset to defaults", "استعادة الافتراضيات"), ("Grid OFF", "الشبكة متوقفة"),
                ("Grid Visibility", "إظهار الشبكة"), ("Major Geographic Reference Lines & Points", "الخطوط والنقاط الجغرافية الرئيسية"),
                ("Show longitude lines (meridians)", "إظهار خطوط الطول"), ("Show latitude lines (parallels)", "إظهار دوائر العرض"),
                ("Grid Colors", "ألوان الشبكة"), ("Major reference lines & points", "الخطوط والنقاط المرجعية الرئيسية"),
                ("Longitude lines (meridians)", "خطوط الطول"), ("Latitude lines (parallels)", "دوائر العرض"),
                ("Coordinate labels", "تسميات الإحداثيات"), ("UTM zone boundaries", "حدود نطاقات UTM"),
                ("Label Format & Appearance", "تنسيق التسميات ومظهرها"), ("Coordinate label format", "تنسيق تسمية الإحداثيات"),
                ("Label size", "حجم التسمية"), ("Small", "صغير"), ("Medium", "متوسط"), ("Large", "كبير"), ("Extra Large", "كبير جدًا"),
                ("Longitude interval (0 = auto)", "فاصل خطوط الطول (0 = تلقائي)"), ("Latitude interval (0 = auto)", "فاصل دوائر العرض (0 = تلقائي)"),
                ("UTM Zone Boundaries & Labels", "حدود نطاقات UTM وتسمياتها"),
                ("Show UTM zone boundaries & labels", "إظهار حدود نطاقات UTM وتسمياتها"),
                ("Boundary color", "لون الحدود"), ("Label placement", "موضع التسمية"), ("Center of each zone", "وسط كل نطاق"),
                ("Label type", "نوع التسمية"), ("Grid zone designation (e.g. 36R)", "تعيين نطاق الشبكة (مثل 36R)"),
                ("UTM hemisphere (e.g. UTM 36N)", "نصف الكرة UTM (مثل UTM 36N)"),
                ("Hemisphere + grid zone designation", "نصف الكرة + تعيين نطاق الشبكة")
            );

            AddTranslations(
                ("Choose the units used for distance, area, and elevation display. Calculation values remain in canonical SI units internally.", "اختر وحدات عرض المسافة والمساحة والارتفاع. تبقى قيم الحساب الداخلية بوحدات النظام الدولي القياسية."),
                ("Distance unit", "وحدة المسافة"), ("Area unit", "وحدة المساحة"), ("Elevation unit", "وحدة الارتفاع"),
                ("Horizontal distances, paths, ruler results and navigation readouts.", "المسافات الأفقية والمسارات ونتائج المسطرة وقراءات التنقل."),
                ("Polygon measurements, regions, coverage and saved area results.", "قياسات المضلعات والمناطق والتغطية ونتائج المساحات المحفوظة."),
                ("Terrain samples, profiles, placemark altitude and eye height.", "عينات التضاريس والقطاعات وارتفاع العلامات وارتفاع العين."),
                ("Example", "مثال"), ("About Measurement Units", "حول وحدات القياس"),
                ("A distance of 1,234.56 metres is displayed as:", "تُعرض مسافة قدرها 1,234.56 متر كما يلي:"),
                ("An area of 1,250,000 square metres is displayed as:", "تُعرض مساحة قدرها 1,250,000 متر مربع كما يلي:"),
                ("An elevation of 987.65 metres is displayed as:", "يُعرض ارتفاع قدره 987.65 متر كما يلي:"),
                ("Z EARTH stores length and elevation in metres and area in square metres; geodesic measurements use WGS 84. Unit choices affect display and supported export formatting only—they never redefine the map CRS or modify stored geometry.", "يخزّن Z EARTH الطول والارتفاع بالمتر والمساحة بالمتر المربع؛ وتستخدم القياسات الجيوديسية WGS 84. تؤثر اختيارات الوحدات في العرض وتنسيق التصدير المدعوم فقط ولا تعيد تعريف نظام الإسناد أو تعدّل الأشكال المحفوظة."),
                ("Control how Z EARTH snaps to geographic features when drawing and measuring.", "تحكم في كيفية التقاط Z EARTH للمعالم الجغرافية أثناء الرسم والقياس."),
                ("Snap to features while drawing and measuring.", "التقاط المعالم أثناء الرسم والقياس."),
                ("Configure the ground tolerance used by all supported tools.", "اضبط سماحية الأرض التي تستخدمها كل الأدوات المدعومة."),
                ("Maximum geodesic ground distance from a candidate to snap, in metres.", "أقصى مسافة أرضية جيوديسية لالتقاط الهدف، بالمتر."),
                ("Existing points, nodes, and line or polygon endpoints.", "النقاط والعقد ونهايات الخطوط أو المضلعات الموجودة."),
                ("The calculated midpoint of existing line and polygon segments.", "منتصف المقاطع المحسوب للخطوط والمضلعات الموجودة."),
                ("The closest projected position along an eligible segment.", "أقرب موضع مسقط على مقطع مؤهل."),
                ("True crossings between eligible segments in the cursor's local tangent plane.", "التقاطعات الحقيقية بين المقاطع المؤهلة في المستوى المماسي المحلي للمؤشر."),
                ("How Spatial Snapping Works", "كيفية عمل الالتقاط المكاني"),
                ("Startup Basemap", "خريطة الأساس عند البدء"),
                ("Select the default source used when Z EARTH starts. Hosted maps require internet and provider attribution.", "اختر المصدر الافتراضي عند بدء Z EARTH. تتطلب الخرائط المستضافة اتصالًا بالإنترنت ونسب المصدر."),
                ("No app key", "بلا مفتاح تطبيق"), ("No key", "بلا مفتاح"), ("Online", "متصل"), ("Offline", "غير متصل"), ("Bundled", "مضمّن"),
                ("Satellite and aerial imagery without reference labels.", "صور أقمار صناعية وجوية بلا تسميات مرجعية."),
                ("Imagery with roads, boundaries and place-name overlay.", "صور مع طبقة طرق وحدود وأسماء أماكن."),
                ("Community street map; OSM attribution required.", "خريطة شوارع مجتمعية؛ يلزم نسب OpenStreetMap."),
                ("Topographic raster with contours and relief.", "خريطة طبوغرافية نقطية بخطوط كنتور وتظليل."),
                ("Low-contrast reference map for thematic overlays.", "خريطة مرجعية منخفضة التباين للطبقات الموضوعية."),
                ("Dark low-contrast map for bright GIS overlays.", "خريطة داكنة منخفضة التباين للطبقات المضيئة."),
                ("Bundled cartographic raster; zero network required.", "خريطة نقطية مضمّنة لا تتطلب اتصالًا بالشبكة."),
                ("Output, Appearance & Navigation", "الإخراج والمظهر والتنقل"), ("Dark Charcoal", "فحمي داكن"), ("Light High-Contrast", "فاتح عالي التباين"),
                ("Show optional provider identity marks", "إظهار علامات هوية المزود الاختيارية"),
                ("Basemap Information", "معلومات خريطة الأساس"),
                ("Choose, connect, and verify numeric elevation sources for land and seafloor visualization.", "اختر مصادر ارتفاع رقمية لعرض اليابسة وقاع البحر واتصل بها وتحقق منها."),
                ("Global numeric land elevation", "ارتفاعات أرضية رقمية عالمية"), ("Global land terrain; ion asset 1", "تضاريس يابسة عالمية؛ أصل ion رقم 1"),
                ("Land + seabed relief; asset 2426648", "تضاريس يابسة وقاع بحر؛ الأصل 2426648"),
                ("Official CDSE Process API • 30 m DSM", "واجهة CDSE الرسمية • نموذج سطح رقمي بدقة 30 م"),
                ("AWS Terrain Tiles • ~30 m source regions", "بلاطات تضاريس AWS • مناطق مصدر بدقة تقارب 30 م"),
                ("Official JAXA Earth API • ~30 m DSM", "واجهة JAXA Earth الرسمية • نموذج سطح رقمي بدقة تقارب 30 م"),
                ("Australian Antarctic Division mirror • 15 arc-second", "نسخة القسم الأسترالي لأبحاث القطب الجنوبي • 15 ثانية قوسية"),
                ("Use 0 for automatic spacing at every zoom level. A fixed interval may be outside the current view.", "استخدم 0 للمسافات التلقائية عند كل مستوى تكبير. قد تقع الشاشة بين خطين عند اختيار مسافة ثابتة."),
                ("Preview of the selected grid colors and layers. Use automatic spacing to adapt the grid when zooming.", "معاينة ألوان الشبكة وطبقاتها المحددة. استخدم المسافات التلقائية لتتغير الشبكة مع التكبير."),
                ("Select a georeferenced GeoTIFF, IMG, or DEM. Outside its coverage or in NoData gaps, the globe displays the ellipsoid and elevation readings are unavailable.", "اختر ملف GeoTIFF أو IMG أو DEM ذا مرجع مكاني. خارج تغطيته أو في فجوات البيانات تظهر الكرة المرجعية، ولا تتوفر قراءات ارتفاع."),
                ("Free API", "واجهة مجانية"),
                ("Dataset workflow; preparation required", "مسار بيانات يتطلب التجهيز"), ("Free data", "بيانات مجانية"),
                ("Smooth globe; no elevation", "كرة ملساء بلا ارتفاعات"), ("Inspect raster, then prepare terrain tiles", "افحص البيانات النقطية ثم جهّز بلاطات التضاريس"),
                ("Authorized HTTPS terrain endpoint", "نقطة تضاريس HTTPS مخوّلة"),
                ("Copernicus Data Space OAuth access token", "رمز وصول OAuth لمساحة بيانات كوبرنيكوس"),
                ("Session only: excluded from settings.json and logs. Managed deployments may use ZEARTH_COPERNICUS_ACCESS_TOKEN.", "للجلسة الحالية فقط: يُستبعد من settings.json والسجلات. يمكن للنشر المُدار استخدام ZEARTH_COPERNICUS_ACCESS_TOKEN."),
                ("Local numeric DEM / bathymetry raster", "ملف ارتفاعات أو أعماق رقمي محلي"),
                ("Browse GeoTIFF / VRT…", "استعراض GeoTIFF / VRT…"),
                ("Vertical unit", "الوحدة الرأسية"),
                ("Meters", "متر"), ("Feet", "قدم"),
                ("The token is kept in process memory only and is excluded from settings.json and logs. Managed deployments may use ZEARTH_CESIUM_ION_TOKEN.", "يُحتفظ بالرمز في ذاكرة العملية فقط ويُستبعد من settings.json والسجلات. يمكن لعمليات النشر المُدارة استخدام ZEARTH_CESIUM_ION_TOKEN."),
                ("Open official GEBCO guidance ↗", "فتح إرشادات GEBCO الرسمية ↗"),
                ("Default Elevation Model", "نموذج الارتفاع الافتراضي"),
                ("Coordinate format", "تنسيق الإحداثيات"), ("Decimal Degrees", "درجات عشرية"), ("Degrees Minutes", "درجات ودقائق"), ("Whole Degrees", "درجات صحيحة")
            );

            AddTranslations(
                ("AWS Terrarium DEM", "نموذج ارتفاع AWS Terrarium"),
                ("Global numeric land elevation from AWS Open Data; no key required.", "ارتفاعات أرضية رقمية عالمية من بيانات AWS المفتوحة؛ لا يلزم مفتاح."),
                ("Using AWS Terrarium DEM", "استخدام نموذج AWS Terrarium"),
                ("A public heightmap terrain source that supplies numeric land elevation to Cesium. Coverage and source quality vary by region; it is not guaranteed bare-earth terrain everywhere.", "مصدر تضاريس عام يزوّد Cesium بارتفاعات أرضية رقمية. تختلف التغطية والجودة حسب المنطقة ولا يُضمن أنه يمثل سطح الأرض العاري في كل مكان."),
                ("Cesium World Terrain", "تضاريس Cesium العالمية"),
                ("High-resolution global land terrain.", "تضاريس أرضية عالمية عالية الدقة."),
                ("Requires a valid Cesium ion token with access to asset 1.", "يتطلب رمز Cesium ion صالحًا يسمح بالوصول إلى الأصل 1."),
                ("Using Cesium World Terrain", "استخدام تضاريس Cesium العالمية"),
                ("Create a Cesium ion account, generate an access token, paste it here, and run Test Connection. Provider access and quotas are governed by Cesium ion.", "أنشئ حساب Cesium ion ورمز وصول والصقه هنا ثم نفّذ اختبار الاتصال. يخضع الوصول والحصص لشروط Cesium ion."),
                ("Cesium World Bathymetry", "قياسات أعماق Cesium العالمية"),
                ("Combined land and seabed relief using Cesium ion asset 2426648.", "تضاريس مدمجة لليابسة وقاع البحر باستخدام أصل Cesium ion رقم 2426648."),
                ("Requires a valid Cesium ion token and access to the bathymetry asset.", "يتطلب رمز Cesium ion صالحًا وإتاحة أصل قياسات الأعماق."),
                ("Using World Bathymetry", "استخدام قياسات الأعماق العالمية"),
                ("This is numeric 3D terrain that includes ocean bathymetry. It is distinct from an ocean shaded-relief imagery layer and requires authorized Cesium ion access.", "هذه تضاريس رقمية ثلاثية الأبعاد تشمل أعماق المحيطات، وهي تختلف عن طبقة صور التظليل البحري وتتطلب وصولًا مصرحًا إلى Cesium ion."),
                ("Copernicus DEM GLO-30", "نموذج كوبرنيكوس GLO-30"),
                ("Official 30 m global DSM through the Copernicus Data Space Process API.", "نموذج سطح رقمي عالمي رسمي بدقة 30 م عبر واجهة Process في مساحة بيانات كوبرنيكوس."),
                ("Requires a free Copernicus Data Space account and a short-lived OAuth access token. It does not use Cesium ion.", "يتطلب حسابًا مجانيًا في مساحة بيانات كوبرنيكوس ورمز وصول OAuth قصير العمر، ولا يستخدم Cesium ion."),
                ("Using Copernicus GLO-30", "استخدام كوبرنيكوس GLO-30"),
                ("Z EARTH requests numeric FLOAT32 DEM samples from the official CDSE service. The token remains in process memory and is never written to settings or logs.", "يطلب Z EARTH عينات ارتفاع رقمية FLOAT32 من خدمة CDSE الرسمية. يبقى الرمز في ذاكرة العملية ولا يُكتب في الإعدادات أو السجلات."),
                ("SRTM-backed Terrain", "تضاريس مدعومة ببيانات SRTM"),
                ("Online numeric height tiles hosted by AWS Open Data.", "بلاطات ارتفاع رقمية متصلة تستضيفها AWS Open Data."),
                ("No API key is required. The global Mapzen/Tilezen mosaic uses SRTM in covered source regions and other public DEMs elsewhere.", "لا يلزم مفتاح API. تستخدم فسيفساء Mapzen/Tilezen العالمية بيانات SRTM في مناطق تغطيتها وبيانات ارتفاع عامة أخرى في بقية المناطق."),
                ("Using SRTM-backed Terrain", "استخدام التضاريس المدعومة بـSRTM"),
                ("This is not relabelled as pure SRTM worldwide. Z EARTH preserves the provider's blended lineage and required attribution.", "لا يُقدَّم هذا المصدر على أنه SRTM خالص عالميًا؛ يحافظ Z EARTH على بيان المصادر المختلطة ونسبها المطلوبة."),
                ("JAXA AW3D30", "JAXA AW3D30"),
                ("Official online ~30 m DSM through JAXA Earth API.", "نموذج سطح رقمي رسمي متصل بدقة تقارب 30 م عبر واجهة JAXA Earth."),
                ("No account or API key is required by JAXA Earth API.", "لا تتطلب واجهة JAXA Earth حسابًا أو مفتاح API."),
                ("Using JAXA AW3D30", "استخدام JAXA AW3D30"),
                ("Z EARTH retrieves numeric AW3D30 COG/STAC tiles through the official JAXA JavaScript API and reports missing sea/no-data honestly.", "يجلب Z EARTH بلاطات AW3D30 الرقمية COG/STAC عبر واجهة JAXA JavaScript الرسمية ويعرض مناطق البحر أو القيم المفقودة بوضوح."),
                ("GEBCO 2026 Bathymetry", "أعماق GEBCO 2026"),
                ("Official global numeric land and seafloor grid at 15 arc-seconds.", "شبكة رقمية عالمية رسمية لليابسة وقاع البحر بدقة 15 ثانية قوسية."),
                ("No API key is required. Z EARTH reads the GEBCO 2026 numeric grid from the Australian Antarctic Division mirror on Source Cooperative; the GEBCO WMS remains imagery only.", "لا يلزم مفتاح API. يقرأ Z EARTH شبكة GEBCO 2026 الرقمية من نسخة القسم الأسترالي لأبحاث القطب الجنوبي على Source Cooperative، بينما تظل خدمة GEBCO WMS صورًا فقط."),
                ("Using GEBCO 2026", "استخدام GEBCO 2026"),
                ("GEBCO values are metres with heterogeneous mean-sea-level assumptions. They are not for navigation, and service/network failure falls back visibly to the ellipsoid.", "قيم GEBCO بالمتر وبمراجع متفاوتة لمتوسط سطح البحر. ليست للملاحة، وعند فشل الخدمة أو الشبكة يظهر الرجوع إلى المجسم الإهليلجي بوضوح."),
                ("WGS84 Ellipsoid", "مجسم WGS84 الإهليلجي"),
                ("Smooth offline globe with no elevation or bathymetry.", "كرة ملساء تعمل دون إنترنت ولا تحتوي على ارتفاعات أو أعماق."),
                ("No network or credentials are required.", "لا يلزم اتصال بالشبكة أو بيانات اعتماد."),
                ("Using the Ellipsoid", "استخدام المجسم الإهليلجي"),
                ("Use this offline fallback when relief is not needed. It must never be described as a DEM or as containing terrain elevations.", "استخدم هذا البديل دون إنترنت عندما لا تحتاج إلى التضاريس. لا يجوز وصفه بأنه DEM أو أنه يحتوي على ارتفاعات."),
                ("Custom Quantized-Mesh Service", "خدمة Quantized-Mesh مخصصة"),
                ("Connect to an HTTPS Cesium terrain service you control or are authorized to use.", "اتصل بخدمة تضاريس Cesium عبر HTTPS تملكها أو لديك تصريح لاستخدامها."),
                ("Enter an HTTPS service root that exposes layer.json and quantized-mesh tiles.", "أدخل جذر خدمة HTTPS يعرض layer.json وبلاطات Quantized-Mesh."),
                ("Using a Custom Terrain Service", "استخدام خدمة تضاريس مخصصة"),
                ("A compatible service must publish Cesium quantized-mesh metadata and tiles. A WMS, XYZ image layer, local GeoTIFF path, or arbitrary URL is not a terrain service.", "يجب أن تنشر الخدمة المتوافقة بيانات Cesium Quantized-Mesh وبلاطاتها. لا تُعد WMS أو صور XYZ أو مسار GeoTIFF محلي أو عنوان عشوائي خدمة تضاريس.")
            );

            AddTranslations(
                ("Globe unavailable", "الكرة غير متاحة"), ("Token required", "الرمز مطلوب"),
                ("Invalid URL", "عنوان غير صالح"), ("Testing…", "جارٍ الاختبار…"),
                ("Connected", "متصل"), ("Failed", "فشل"),
                ("The embedded globe is not available. Open Options from the running main window and try again.", "الكرة المضمّنة غير متاحة. افتح الخيارات من النافذة الرئيسية العاملة ثم أعد المحاولة."),
                ("Paste a valid Cesium ion token before testing this source.", "الصق رمز Cesium ion صالحًا قبل اختبار هذا المصدر."),
                ("Enter an absolute HTTPS terrain service URL.", "أدخل عنوان HTTPS مطلقًا لخدمة التضاريس."),
                ("Localhost terrain services are not accepted in this configuration field.", "لا يقبل هذا الحقل خدمات التضاريس على localhost."),
                ("Private, loopback, and link-local terrain service addresses are not accepted.", "لا تُقبل عناوين خدمات التضاريس الخاصة أو المحلية أو Link-local."),
                ("Waiting for the Cesium engine to load and validate the selected source…", "في انتظار تحميل محرك Cesium والتحقق من المصدر المحدد…"),
                ("The embedded Cesium engine acknowledged this terrain source. Visible relief still requires native GPU and tile acceptance.", "أقر محرك Cesium المضمّن بمصدر التضاريس. ما زال ظهور التضاريس يتطلب اختبارًا أصليًا لوحدة الرسومات والبلاطات."),
                ("The terrain source failed. The globe falls back to the WGS84 ellipsoid; check credentials, URL, network, and provider access.", "فشل مصدر التضاريس فعادت الكرة إلى مجسم WGS84. تحقق من بيانات الاعتماد والعنوان والشبكة وإتاحة المزود."),
                ("Connection failed. See the application log for the provider error.", "فشل الاتصال. راجع سجل التطبيق لمعرفة خطأ المزود.")
            );
        }

        private void AddTranslations(params (string English, string Arabic)[] values)
        {
            foreach (var (english, arabic) in values) _arByEnglish[english] = arabic;
        }

        public string GetString(string key, string? fallback = null)
        {
            if (_currentLanguage == "ar" && _arStrings.TryGetValue(key, out var arVal))
            {
                return arVal;
            }
            if (_enStrings.TryGetValue(key, out var enVal))
            {
                return enVal;
            }
            return fallback ?? key;
        }

        public bool HasArabicTranslation(string english) =>
            !string.IsNullOrWhiteSpace(english) && _arByEnglish.ContainsKey(english.Trim());

        public string TranslateLiteral(string value)
        {
            if (!IsArabic || string.IsNullOrWhiteSpace(value)) return value;
            var trimmed = value.Trim();
            if (!_arByEnglish.TryGetValue(trimmed, out var translated)) return value;
            var start = value.IndexOf(trimmed, StringComparison.Ordinal);
            return value[..start] + translated + value[(start + trimmed.Length)..];
        }

        public void ApplyWindow(Window window)
        {
            window.FlowDirection = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            var visited = new HashSet<DependencyObject>();
            ApplyElement(window, visited);
        }

        private void ApplyElement(DependencyObject element, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(element)) return;

            if (element is TextBlock)
                ApplyLiteral(element, TextBlock.TextProperty);
            if (element is ContentControl)
                ApplyLiteral(element, ContentControl.ContentProperty);
            if (element is HeaderedContentControl)
                ApplyLiteral(element, HeaderedContentControl.HeaderProperty);
            if (element is HeaderedItemsControl)
                ApplyLiteral(element, HeaderedItemsControl.HeaderProperty);
            if (element is FrameworkElement frameworkElement)
            {
                ApplyLiteral(element, FrameworkElement.ToolTipProperty);
                ApplyLiteral(element, AutomationProperties.NameProperty);
                if (frameworkElement.ContextMenu != null) ApplyElement(frameworkElement.ContextMenu, visited);
            }

            foreach (var child in LogicalTreeHelper.GetChildren(element))
                if (child is DependencyObject dependencyChild) ApplyElement(dependencyChild, visited);

            try
            {
                var childCount = VisualTreeHelper.GetChildrenCount(element);
                for (var i = 0; i < childCount; i++) ApplyElement(VisualTreeHelper.GetChild(element, i), visited);
            }
            catch (InvalidOperationException)
            {
                // Content elements can be logical-only and are already covered above.
            }
        }

        private void ApplyLiteral(DependencyObject element, DependencyProperty property)
        {
            var localValue = element.ReadLocalValue(property);
            if (localValue is not string current) return; // bindings/templates remain owned by their source

            var originals = _originalValues.GetValue(element, _ => new Dictionary<DependencyProperty, object>());
            if (!originals.TryGetValue(property, out var original))
            {
                original = current;
                originals[property] = original;
            }
            var english = (string)original;
            element.SetValue(property, IsArabic ? TranslateLiteral(english) : english);
        }

        public void SetLanguage(string lang, Window? window = null)
        {
            _currentLanguage = (lang.ToLowerInvariant() == "ar") ? "ar" : "en";

            if (window != null)
            {
                ApplyWindow(window);
            }

            LanguageChanged?.Invoke(this, _currentLanguage);
        }

        public void ToggleLanguage(Window? window = null)
        {
            SetLanguage(IsArabic ? "en" : "ar", window);
        }
    }
}
