using System.Text;
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
/// <para><b>Nested parens and reference-style links (reviewer pass2 P2):</b> an inline link's destination
/// is no longer matched by a fixed-depth regex — <see cref="NeutralizeUnsafeMarkdownLinks"/> instead scans
/// forward from each <c>](</c> counting <c>(</c>/<c>)</c> depth to find the actual matching close paren
/// (bailing at a newline, same as CommonMark's own bare-destination rule), so arbitrarily deep *balanced*
/// nesting — not just one level — resolves to the same URL Markdig itself would resolve. A separate pass,
/// <see cref="NeutralizeUnsafeReferenceDefinitions"/>, does the same for GFM reference-style links
/// (<c>[text][label]</c>, shortcut <c>[label]</c>, collapsed <c>[label][]</c>): none of those *usage*
/// forms carry a URL themselves — only the separate <c>[label]: url</c> *definition* line does, which
/// neither the inline-link nor the autolink pass ever looked at — so an unsafe-scheme definition is
/// removed entirely, leaving Markdig nothing to resolve any usage of that label to (it falls back to the
/// usage's literal bracket text instead of a link).</para>
///
/// <para><b>Limitation (documented, defense-in-depth):</b> this is a regex/scanner allowlist, not a full
/// HTML or CommonMark parser. It is intentionally strict (strip-unknown) so malformed or novel markup
/// degrades to plain text rather than passing through. The web viewer and PDF composer should still treat
/// the stored value as untrusted and render it through their own safe pipeline — which is why
/// <see cref="MarkdownPdfPlanBuilder"/>/<c>AuthoredDocumentPdfDocument</c> re-check every link's
/// Markdig-resolved URL against <see cref="IsSafeUrl"/> again at render time, regardless of what syntax
/// form produced it.</para>
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

    // Markdown `[text](` opening only -- the destination (and any title) that follows is located and
    // validated by NeutralizeUnsafeMarkdownLinks's own paren-depth scanner below, not by this regex, so
    // nesting is bounded only by the actual matching close paren rather than a fixed pattern depth
    // (reviewer pass2 P2: a fixed "one balanced pair" regex missed deeper nesting that Markdig itself
    // resolves just fine). The label group intentionally allows any non-`]` character including a
    // newline (CommonMark link text may soft-wrap across lines); only the destination scan bails at one.
    // A backslash escapes the next character (CommonMark), so `\]` inside the label does not end it.
    private static readonly Regex MarkdownLinkOpen = new("\\[((?:\\\\[\\s\\S]|[^\\]\\\\])*)\\]\\(", RegexOptions.Compiled);

    // An optional trailing `"title"`/`'title'` at the very end of an already-extracted destination+title
    // span, preceded by whitespace -- used to split a link/reference destination from its title.
    private static readonly Regex TrailingQuotedTitle = new(
        "\\s(?:\"[^\"]*\"|'[^']*')$", RegexOptions.Compiled);

    // Markdown/CommonMark autolink `<scheme:...>` — e.g. `<https://example.com>` or `<javascript:...>`.
    // Requires a URI-scheme-shaped prefix (letter, then letters/digits/+/./-, then `:`) so this never
    // matches an ordinary HTML tag like `<b>` or `<a href="...">` (no colon before the first `>`/space).
    private static readonly Regex MarkdownAutolink = new(
        "<([a-zA-Z][a-zA-Z0-9+.\\-]*:[^\\s<>]*)>", RegexOptions.Compiled);

    // Opening of a GFM/CommonMark link reference definition: up to 3 leading spaces, `[label]:`, then
    // inline whitespace optionally spanning one line ending (CommonMark explicitly allows the destination
    // to start on the next line -- confirmed against the installed Markdig 1.3.2 that `[r]:\n  url`
    // resolves the same as `[r]: url`) and more inline whitespace, landing right at the destination.
    private static readonly Regex ReferenceDefinitionOpen = new(
        "^[ ]{0,3}\\[(?:\\\\[^\r\n]|[^\\]\\\\\r\n]){1,999}\\]:[ \\t]*(?:\\r\\n|\\n)?[ \\t]*",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html ?? string.Empty;

        var s = DangerousBlocks.Replace(html, string.Empty);
        s = Comments.Replace(s, string.Empty);
        s = OrphanDangerousOpen.Replace(s, string.Empty);

        // Neutralize unsafe-scheme markdown link/autolink/reference-definition syntax before the HTML tag
        // pass below, which has no awareness of markdown syntax and would otherwise let e.g.
        // `[click](javascript:...)` pass through untouched (reviewer pass1 P1).
        s = MarkdownAutolink.Replace(s, static m =>
        {
            var url = m.Groups[1].Value;
            return IsSafeUrl(url) ? m.Value : url; // unsafe scheme -> plain url text, no angle brackets
        });
        s = NeutralizeUnsafeMarkdownLinks(s);
        s = NeutralizeUnsafeReferenceDefinitions(s);

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

    /// <summary>Neutralizes unsafe-scheme inline markdown links (<c>[text](url)</c>). Finds each
    /// <c>](</c> opening via <see cref="MarkdownLinkOpen"/>, then scans forward counting <c>(</c>/<c>)</c>
    /// depth to find the actual matching close paren (bailing — treating the span as not a link — at a
    /// newline or unbalanced end of input, matching CommonMark's own bare-destination rule), so nesting of
    /// any depth resolves the same destination Markdig itself would resolve (reviewer pass2 P2). A safe
    /// destination's whole matched span is preserved byte-for-byte; an unsafe one collapses to its plain
    /// link text.</summary>
    private static string NeutralizeUnsafeMarkdownLinks(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pos = 0;

        while (true)
        {
            var open = MarkdownLinkOpen.Match(s, pos);
            if (!open.Success)
            {
                sb.Append(s, pos, s.Length - pos);
                break;
            }

            sb.Append(s, pos, open.Index - pos);

            var label = open.Groups[1].Value;
            var destStart = open.Index + open.Length; // just after "[label]("
            var depth = 1;
            var cursor = destStart;
            while (cursor < s.Length && depth > 0)
            {
                var c = s[cursor];
                if (c is '\n' or '\r')
                    break;
                if (c == '\\' && cursor + 1 < s.Length && s[cursor + 1] is not ('\n' or '\r'))
                {
                    cursor += 2; // an escaped character never opens or closes the destination
                    continue;
                }
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                cursor++;
            }

            if (depth != 0)
            {
                // No matching close paren before a newline/end of input -- not a well-formed inline link;
                // put the opening back unchanged and resume scanning right after it (never re-matching
                // the same "[label](" span, so this always terminates).
                sb.Append(s, open.Index, destStart - open.Index);
                pos = destStart;
                continue;
            }

            var closeParenIndex = cursor - 1;
            var url = ExtractDestination(s.Substring(destStart, closeParenIndex - destStart));

            sb.Append(IsSafeUrl(url) ? s.Substring(open.Index, closeParenIndex - open.Index + 1) : label);
            pos = closeParenIndex + 1;
        }

        return sb.ToString();
    }

    /// <summary>Splits an already-depth-balanced <c>destination</c> or <c>destination "title"</c> span
    /// into just its destination: strips an optional trailing quoted title, then unwraps
    /// <c>&lt;...&gt;</c> destination wrapping if present.</summary>
    private static string ExtractDestination(string destinationAndTitle)
    {
        var s = destinationAndTitle.Trim();

        var title = TrailingQuotedTitle.Match(s);
        if (title.Success)
            s = s[..title.Index].TrimEnd();

        if (s.Length >= 2 && s[0] == '<' && s[^1] == '>')
            s = s[1..^1];

        return s;
    }

    /// <summary>Neutralizes unsafe-scheme GFM reference-style link definitions (<c>[label]: url</c>).
    /// Neither <see cref="NeutralizeUnsafeMarkdownLinks"/> nor <see cref="MarkdownAutolink"/> ever look at
    /// this syntax -- a <c>[text][label]</c>, shortcut <c>[label]</c>, or collapsed <c>[label][]</c> usage
    /// elsewhere in the document carries no URL itself; Markdig resolves it entirely from this separate
    /// definition line (reviewer pass2 P2). Rather than finding and rewriting every possible usage form,
    /// this removes the whole *definition* line when its scheme is unsafe: with nothing left for Markdig
    /// to resolve, every usage of that label falls back to its literal bracket text instead of a link. A
    /// safe definition's whole line (including its line ending) is preserved byte-for-byte.</summary>
    private static string NeutralizeUnsafeReferenceDefinitions(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pos = 0;

        while (pos <= s.Length)
        {
            var open = ReferenceDefinitionOpen.Match(s, pos);
            if (!open.Success)
            {
                sb.Append(s, pos, s.Length - pos);
                break;
            }

            sb.Append(s, pos, open.Index - pos);

            var definition = TryParseReferenceDefinition(s, open.Index, open.Length);
            if (definition is not { } parsed)
            {
                // Looked like a "[label]:" opening but didn't resolve to a well-formed destination
                // Markdig would actually parse as a definition -- copy the opening text unchanged and
                // resume scanning right after it (never re-matching the same span).
                sb.Append(s, open.Index, open.Length);
                pos = open.Index + open.Length;
                continue;
            }

            if (IsSafeUrl(parsed.Url))
                sb.Append(s, open.Index, parsed.Length);
            // else: unsafe scheme -- drop the whole definition line, appending nothing for it.

            pos = open.Index + parsed.Length;
        }

        return sb.ToString();
    }

    private readonly record struct ReferenceDefinition(string Url, int Length);

    /// <summary>Parses a candidate reference definition starting at <paramref name="start"/> (a
    /// <see cref="ReferenceDefinitionOpen"/> match already consumed <paramref name="openLength"/>
    /// characters of "[label]:" + whitespace/one line ending). Returns null when what follows isn't a
    /// well-formed destination/title Markdig would actually resolve as a definition (e.g. an empty or
    /// unbalanced-paren destination, or trailing non-whitespace content on the line) -- the caller then
    /// leaves that text alone rather than risk removing content that was never really a link reference.
    /// </summary>
    private static ReferenceDefinition? TryParseReferenceDefinition(string s, int start, int openLength)
    {
        var cursor = start + openLength;
        string url;

        if (cursor < s.Length && s[cursor] == '<')
        {
            var close = s.IndexOf('>', cursor + 1);
            if (close < 0 || s.IndexOfAny(NewlineChars, cursor + 1, close - cursor - 1) >= 0)
                return null;
            url = s.Substring(cursor + 1, close - cursor - 1);
            cursor = close + 1;
        }
        else
        {
            var destStart = cursor;
            var depth = 0;
            while (cursor < s.Length)
            {
                var c = s[cursor];
                if (c == '\\' && cursor + 1 < s.Length && !char.IsWhiteSpace(s[cursor + 1]))
                {
                    cursor += 2; // escaped character: part of the destination, never a paren
                    continue;
                }
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    if (depth == 0)
                        break;
                    depth--;
                }
                else if (char.IsWhiteSpace(c))
                {
                    break;
                }
                cursor++;
            }

            if (cursor == destStart || depth != 0)
                return null; // empty or unbalanced-paren destination -- not a real link destination

            url = s.Substring(destStart, cursor - destStart);
        }

        cursor = SkipInlineWhitespaceAndOneLineEnding(s, cursor);
        if (cursor < s.Length && s[cursor] is '"' or '\'')
        {
            var quote = s[cursor];
            var close = s.IndexOf(quote, cursor + 1);
            if (close < 0)
                return null;
            cursor = close + 1;
        }

        // Whatever remains on this line must be blank for Markdig to accept the definition.
        var lineEnd = cursor;
        while (lineEnd < s.Length && (s[lineEnd] == ' ' || s[lineEnd] == '\t'))
            lineEnd++;
        if (lineEnd < s.Length && s[lineEnd] != '\n' && s[lineEnd] != '\r')
            return null;

        if (lineEnd < s.Length)
            lineEnd += s[lineEnd] == '\r' && lineEnd + 1 < s.Length && s[lineEnd + 1] == '\n' ? 2 : 1;

        return new ReferenceDefinition(url, lineEnd - start);
    }

    private static readonly char[] NewlineChars = { '\r', '\n' };

    /// <summary>Advances past inline whitespace, then — CommonMark explicitly allows the destination
    /// and title of a reference definition to each start on the next line — at most one line ending
    /// followed by more inline whitespace.</summary>
    private static int SkipInlineWhitespaceAndOneLineEnding(string s, int cursor)
    {
        while (cursor < s.Length && (s[cursor] == ' ' || s[cursor] == '\t'))
            cursor++;

        if (cursor < s.Length && s[cursor] == '\r' && cursor + 1 < s.Length && s[cursor + 1] == '\n')
            cursor += 2;
        else if (cursor < s.Length && s[cursor] == '\n')
            cursor += 1;
        else
            return cursor;

        while (cursor < s.Length && (s[cursor] == ' ' || s[cursor] == '\t'))
            cursor++;
        return cursor;
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
