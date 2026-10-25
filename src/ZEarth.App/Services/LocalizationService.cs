using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

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

        public void SetLanguage(string lang, Window? window = null)
        {
            _currentLanguage = (lang.ToLowerInvariant() == "ar") ? "ar" : "en";

            if (window != null)
            {
                window.FlowDirection = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }

            LanguageChanged?.Invoke(this, _currentLanguage);
        }

        public void ToggleLanguage(Window? window = null)
        {
            SetLanguage(IsArabic ? "en" : "ar", window);
        }
    }
}
