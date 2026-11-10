using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ZEarth.Domain.Kml
{
    /// <summary>
    /// Safely sanitizes KML HTML descriptions to prevent XSS, script injection, iframe execution,
    /// and local filesystem access (SIDEBAR-01 Scope, Section 2 & SB-12).
    /// </summary>
    public static class HtmlSanitizer
    {
        private static readonly HashSet<string> PreviewTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "p", "br", "hr", "div", "span", "strong", "b", "em", "i", "u",
            "ul", "ol", "li", "table", "thead", "tbody", "tfoot", "tr", "th", "td",
            "h1", "h2", "h3", "h4", "pre", "code", "blockquote"
        };
        private static readonly Regex PreviewTokenRegex = new(@"<[^>]*>", RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
        private static readonly Regex PreviewTagRegex = new(@"^<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>$",
            RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex ScriptTagRegex = new(@"<script[\s\S]*?>[\s\S]*?<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IframeTagRegex = new(@"<iframe[\s\S]*?>[\s\S]*?<\/iframe>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ObjectTagRegex = new(@"<object[\s\S]*?>[\s\S]*?<\/object>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EmbedTagRegex = new(@"<embed[\s\S]*?>[\s\S]*?<\/embed>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EventAttributesRegex = new(@"\s+on\w+\s*=\s*(?:'[^']*'|""[^""]*""|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JavascriptUriRegex = new(@"href\s*=\s*(['""])javascript:[^'""]*\1", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FileUriRegex = new(@"href\s*=\s*(['""])file:[^'""]*\1", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SrcJavascriptRegex = new(@"src\s*=\s*(['""])javascript:[^'""]*\1", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Sanitize(string? rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml)) return string.Empty;

            string sanitized = rawHtml;

            // Strip active executable tags
            sanitized = ScriptTagRegex.Replace(sanitized, string.Empty);
            sanitized = IframeTagRegex.Replace(sanitized, string.Empty);
            sanitized = ObjectTagRegex.Replace(sanitized, string.Empty);
            sanitized = EmbedTagRegex.Replace(sanitized, string.Empty);

            // Strip inline JS event handlers (onload, onclick, onerror, etc.)
            sanitized = EventAttributesRegex.Replace(sanitized, string.Empty);

            // Strip dangerous URIs
            sanitized = JavascriptUriRegex.Replace(sanitized, "href=\"#\"");
            sanitized = FileUriRegex.Replace(sanitized, "href=\"#blocked-local-file\"");
            sanitized = SrcJavascriptRegex.Replace(sanitized, string.Empty);

            return sanitized;
        }

        /// <summary>Render only passive structural HTML; imported attributes, CSS,
        /// links, scripts and external resources never enter the browser preview.
        /// The original description is preserved independently for saving/export.</summary>
        public static string SanitizeForPreview(string? rawHtml)
        {
            if (string.IsNullOrEmpty(rawHtml)) return string.Empty;
            var source = ScriptTagRegex.Replace(rawHtml, string.Empty);
            source = IframeTagRegex.Replace(source, string.Empty);
            source = ObjectTagRegex.Replace(source, string.Empty);
            var result = new StringBuilder();
            var offset = 0;
            foreach (Match token in PreviewTokenRegex.Matches(source))
            {
                result.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(source.Substring(offset, token.Index - offset))));
                var tag = PreviewTagRegex.Match(token.Value);
                if (tag.Success && PreviewTags.Contains(tag.Groups[2].Value))
                {
                    result.Append('<').Append(tag.Groups[1].Value).Append(tag.Groups[2].Value.ToLowerInvariant()).Append('>');
                }
                offset = token.Index + token.Length;
            }
            result.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(source.Substring(offset))));
            return result.ToString();
        }

        public static string WrapInSafeDocument(string sanitizedContent, bool isDarkTheme = true)
        {
            string bg = isDarkTheme ? "#1a1f2c" : "#ffffff";
            string fg = isDarkTheme ? "#e2e8f0" : "#1e293b";
            string linkColor = isDarkTheme ? "#38bdf8" : "#0284c7";
            string borderColor = isDarkTheme ? "#334155" : "#cbd5e1";

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
    <base target=""_blank"">
    <style>
        body {{
            font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
            font-size: 13px;
            line-height: 1.5;
            background-color: {bg};
            color: {fg};
            margin: 12px;
            padding: 0;
            word-wrap: break-word;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 8px 0;
        }}
        th, td {{
            border: 1px solid {borderColor};
            padding: 6px 10px;
            text-align: left;
        }}
        th {{
            background: {(isDarkTheme ? "#242e42" : "#f1f5f9")};
        }}
        a {{
            color: {linkColor};
            text-decoration: underline;
        }}
        img {{
            max-width: 100%;
            height: auto;
            border-radius: 4px;
        }}
    </style>
</head>
<body>
    {sanitizedContent}
</body>
</html>";
        }
    }
}
