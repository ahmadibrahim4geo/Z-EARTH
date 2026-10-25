using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ZEarth.App.Services
{
    public enum AppTheme
    {
        Dark,
        Light
    }

    public sealed class ThemeService
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        // Resource-dictionary URIs use the "/Assembly;component/..." form, which resolves to the
        // pack resource in this assembly and avoids the relative-URI "URI prefix is not recognized"
        // failure seen previously (RC-6).
        private static readonly Uri VectorIconsUri = new("/Z EARTH;component/Themes/VectorIcons.xaml", UriKind.Relative);
        private static readonly Uri DarkThemeUri = new("/Z EARTH;component/Themes/DarkTheme.xaml", UriKind.Relative);
        private static readonly Uri LightThemeUri = new("/Z EARTH;component/Themes/LightTheme.xaml", UriKind.Relative);

        private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
        public static ThemeService Instance => _instance.Value;

        public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
        public event EventHandler<AppTheme>? ThemeChanged;

        private ThemeService() { }

        public void SetTheme(AppTheme theme, Window? window = null)
        {
            CurrentTheme = theme;

            try
            {
                if (Application.Current != null)
                {
                    var dictionaries = Application.Current.Resources.MergedDictionaries;

                    // Replace only the theme dictionary rather than clearing all merged resources,
                    // which tore down unrelated resources and caused visible re-layout churn (RC-6).
                    var targetName = theme == AppTheme.Light ? "LightTheme.xaml" : "DarkTheme.xaml";
                    var otherName = theme == AppTheme.Light ? "DarkTheme.xaml" : "LightTheme.xaml";
                    var targetUri = theme == AppTheme.Light ? LightThemeUri : DarkThemeUri;

                    RemoveByFileName(dictionaries, otherName);
                    UpsertByFileName(dictionaries, targetName, targetUri);
                    UpsertByFileName(dictionaries, "VectorIcons.xaml", VectorIconsUri);
                }

                if (Application.Current != null)
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        ApplyWindowTheme(w, theme);
                    }
                }
                else if (window != null)
                {
                    ApplyWindowTheme(window, theme);
                }

                ThemeChanged?.Invoke(this, theme);
                LoggerService.Instance.LogInfo($"Application theme switched to {theme}");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to switch theme to {theme}", ex);
            }
        }

        private static bool MatchesSource(ResourceDictionary dictionary, string fileName)
            => dictionary.Source != null
               && dictionary.Source.OriginalString.EndsWith(fileName, StringComparison.OrdinalIgnoreCase);

        private static void RemoveByFileName(Collection<ResourceDictionary> dictionaries, string fileName)
        {
            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                if (MatchesSource(dictionaries[i], fileName))
                {
                    dictionaries.RemoveAt(i);
                }
            }
        }

        private static void UpsertByFileName(Collection<ResourceDictionary> dictionaries, string fileName, Uri uri)
        {
            for (int i = 0; i < dictionaries.Count; i++)
            {
                if (MatchesSource(dictionaries[i], fileName))
                {
                    if (dictionaries[i].Source != uri)
                    {
                        dictionaries[i] = new ResourceDictionary { Source = uri };
                    }
                    return;
                }
            }

            dictionaries.Add(new ResourceDictionary { Source = uri });
        }

        public void ToggleTheme(Window? window = null)
        {
            var nextTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            SetTheme(nextTheme, window);
        }

        public void ApplyWindowTheme(Window window, AppTheme theme)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    // Dialog constructors run before an HWND exists. Re-apply once Windows has
                    // created the native window so every secondary title bar follows the theme.
                    EventHandler? sourceInitialized = null;
                    sourceInitialized = (_, _) =>
                    {
                        window.SourceInitialized -= sourceInitialized;
                        ApplyWindowTheme(window, theme);
                    };
                    window.SourceInitialized += sourceInitialized;
                    return;
                }

                int useDarkMode = theme == AppTheme.Dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }

                // Window title bar background color:
                // Dark: #252629 -> COLORREF 0x00292625 (BGR)
                // Light: #F3F3F5 -> COLORREF 0x00F5F3F3 (BGR)
                int bgrColor = theme == AppTheme.Dark ? 0x00292625 : 0x00F5F3F3;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref bgrColor, sizeof(int));

                // Explicit caption text/border colors prevent Windows from leaving a white
                // caption on dark secondary windows when immersive dark mode is applied late.
                int textColor = theme == AppTheme.Dark ? 0x00FFFFFF : 0x00111827;
                int borderColor = theme == AppTheme.Dark ? 0x00403F3D : 0x00D8D6D2;
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
            }
            catch
            {
                // Fallback gracefully on older OS versions
            }
        }
    }
}
