using System;

namespace ZEarth.App.Services;

/// <summary>
/// Protects the WPF command bridge. The only page allowed to issue desktop
/// commands is the application's locally packaged Cesium document.
/// Subresources and provider tiles are NOT trusted command issuers.
/// </summary>
public static class WebViewTrustPolicy
{
    public static bool IsTrustedGlobeDocument(string? source)
    {
        return Uri.TryCreate(source, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && uri.Host.Equals("cesium.local", StringComparison.OrdinalIgnoreCase)
               && uri.IsDefaultPort
               && string.IsNullOrEmpty(uri.UserInfo)
               && (uri.AbsolutePath.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath == "/");
    }
}
