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
/// <para><b>Markdown link/autolink syntax (reviewer pass1 P1):</b> the field's persisted format is now
/// GFM markdown, not HTML, so a <c>[text](url)</c> link or a <c>&lt;url&gt;</c> autolink carries no angle
/// brackets around its URL and never matched the HTML <c>&lt;a href&gt;</c> path above — an unsafe scheme
/// (<c>javascript:</c>, <c>data:</c>, …) would otherwise survive sanitization untouched and reach
/// <see cref="AuthoredDocumentPdfDocument"/>'s <c>Hyperlink()</c> sink unvalidated. <see cref="Sanitize"/>
/// now also walks these two syntaxes with the same <see cref="IsSafeUrl"/> allowlist before the HTML tag
/// pass runs: a disallowed-scheme markdown link is rewritten to its plain link text, and a
/// disallowed-scheme autolink to its plain URL text.</para>
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

    // Markdown `[text](url)` / `[text](url "title")` / `[text](<url with spaces>)` — the destination is
    // either angle-bracket-wrapped (group 2, used by the editor's serializer when the url contains
    // whitespace/control characters) or bare (group 3). The bare form allows one level of *balanced*
    // parens inside the url (CommonMark itself allows this without angle-bracket-wrapping), because the
    // canonical injection payload is exactly this shape -- `javascript:alert(1)` -- one matched `(...)`
    // pair; deeper nesting would need angle-bracket wrapping to round-trip through a real markdown
    // renderer anyway. Deliberately a simple, non-backtracking-prone regex rather than a full CommonMark
    // parser, matching this sanitizer's existing philosophy: it is not a formatter, only a scheme gate,
    // so it need not reproduce every edge case of link syntax.
    private static readonly Regex MarkdownLink = new(
        "\\[([^\\]]*)\\]\\(\\s*(?:<([^<>]*)>|((?:[^()\\s]|\\([^()]*\\))+))(?:\\s+(?:\"[^\"]*\"|'[^']*'))?\\s*\\)",
        RegexOptions.Compiled);

    // Markdown/CommonMark autolink `<scheme:...>` — e.g. `<https://example.com>` or `<javascript:...>`.
    // Requires a URI-scheme-shaped prefix (letter, then letters/digits/+/./-, then `:`) so this never
    // matches an ordinary HTML tag like `<b>` or `<a href="...">` (no colon before the first `>`/space).
    private static readonly Regex MarkdownAutolink = new(
        "<([a-zA-Z][a-zA-Z0-9+.\\-]*:[^\\s<>]*)>", RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html ?? string.Empty;

        var s = DangerousBlocks.Replace(html, string.Empty);
        s = Comments.Replace(s, string.Empty);
        s = OrphanDangerousOpen.Replace(s, string.Empty);

        // Neutralize unsafe-scheme markdown link/autolink syntax before the HTML tag pass below, which
        // has no awareness of markdown syntax and would otherwise let e.g. `[click](javascript:...)`
        // pass through untouched (reviewer pass1 P1).
        s = MarkdownAutolink.Replace(s, static m =>
        {
            var url = m.Groups[1].Value;
            return IsSafeUrl(url) ? m.Value : url; // unsafe scheme -> plain url text, no angle brackets
        });
        s = MarkdownLink.Replace(s, static m =>
        {
            var label = m.Groups[1].Value;
            var url = (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).Trim();
            return IsSafeUrl(url) ? m.Value : label; // unsafe scheme -> plain link text, no url
        });

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

    /// <summary>Shared scheme allowlist (http/https/mailto/relative/anchor) — <see cref="Sanitize"/> uses
    /// it for HTML <c>&lt;a href&gt;</c> and markdown link/autolink syntax alike, and
    /// <see cref="AuthoredDocumentPdfDocument"/>'s <c>MarkdownPdfPlanBuilder</c>/<c>EmitMarkdownLink</c>
    /// reuse it (defense in depth) so the PDF's <c>Hyperlink()</c> sink can never emit a scheme this
    /// sanitizer would have stripped at persist time.</summary>
    internal static bool IsSafeUrl(string url)
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
