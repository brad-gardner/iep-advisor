using System.Text.RegularExpressions;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Conservative allowlist sanitizer for RichText field values (State Document Template Engine, Phase 3,
/// cross-cutting G-c.5). RichText is authored by educators and later rendered in the web viewer AND the
/// generated PDF, so it is sanitized to a safe formatting subset <em>before persisting</em>.
///
/// <para>Behavior: whole dangerous blocks (<c>script</c>/<c>style</c>/<c>iframe</c>/<c>object</c>/
/// <c>embed</c>/<c>noscript</c>/<c>template</c>) are removed <em>with</em> their content; HTML comments
/// are removed; any tag not on the formatting allowlist is stripped (its inner text is kept); allowed
/// tags keep <b>no</b> attributes (which removes <c>on*</c> handlers and inline styles) except a safe
/// <c>href</c> on <c>&lt;a&gt;</c> restricted to http/https/mailto/relative/anchor URLs.</para>
///
/// <para><b>Limitation (documented, defense-in-depth):</b> this is a regex allowlist, not a full HTML
/// parser. It is intentionally strict (strip-unknown) so malformed or novel markup degrades to plain
/// text rather than passing through. The web viewer and PDF composer should still treat the stored
/// value as untrusted and render it through their own safe pipeline.</para>
/// </summary>
public static class RichTextSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "b", "strong", "i", "em", "u", "s", "strike",
        "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "a", "span", "div", "pre", "code"
    };

    private static readonly Regex DangerousBlocks = new(
        @"<(script|style|iframe|object|embed|noscript|template)\b[^>]*>.*?</\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OrphanDangerousOpen = new(
        @"<\s*/?\s*(script|style|iframe|object|embed|noscript|template)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Comments = new(
        @"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Tag = new(
        @"<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b([^>]*)>", RegexOptions.Compiled);

    private static readonly Regex Href = new(
        "href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html ?? string.Empty;

        var s = DangerousBlocks.Replace(html, string.Empty);
        s = Comments.Replace(s, string.Empty);
        s = OrphanDangerousOpen.Replace(s, string.Empty);

        return Tag.Replace(s, static m =>
        {
            var closing = m.Groups[1].Value == "/";
            var tag = m.Groups[2].Value.ToLowerInvariant();

            if (!AllowedTags.Contains(tag))
                return string.Empty; // strip disallowed tag markup, keep any inner text

            if (closing)
                return $"</{tag}>";

            if (tag == "a")
            {
                var href = Href.Match(m.Groups[3].Value);
                if (href.Success)
                {
                    var url = (href.Groups[1].Success ? href.Groups[1].Value : href.Groups[2].Value).Trim();
                    if (IsSafeUrl(url))
                        return $"<a href=\"{EscapeAttribute(url)}\">";
                }
                return "<a>";
            }

            return $"<{tag}>"; // allowed formatting tag with all attributes stripped
        });
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var u = url.TrimStart();

        // Reject protocol-relative URLs ("//evil.com", or "/\evil.com" which some browsers treat
        // the same way): they resolve off-site under the current scheme, so they are link-injection
        // vectors even though they start with "/". Same-origin relative paths ("/foo", "/") are fine.
        if (u.StartsWith("//") || u.StartsWith("/\\"))
            return false;

        return u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || u.StartsWith("/")
            || u.StartsWith("#");
    }

    private static string EscapeAttribute(string value) => value
        .Replace("&", "&amp;")
        .Replace("\"", "&quot;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
