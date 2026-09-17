using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// AST-driven neutralisation of unsafe-scheme markdown links, used by <see cref="RichTextSanitizer"/> as
/// the final word after its regex/scanner passes. Markdig (with precise source locations) tells us exactly
/// which spans of the input resolve to links, autolinks and link reference definitions, and what URL each
/// resolves to — entity decoding, escapes, multi-line titles and reference resolution included — so the
/// rewrite matches the parser the PDF composer and prompts use instead of approximating it.
/// </summary>
internal static class MarkdownLinkAst
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// Rewrites <paramref name="markdown"/> so that no link, image, autolink or reference definition whose
    /// resolved URL fails <paramref name="isSafeUrl"/> survives: inline/reference links and images collapse
    /// to their label text, autolinks to their bare URL text, and definitions are removed. Safe content is
    /// returned byte-for-byte. Input Markdig cannot parse (its nesting limit) is returned unchanged — the
    /// render sinks re-check resolved URLs independently.
    /// </summary>
    public static string NeutralizeUnsafeLinks(string markdown, Func<string, bool> isSafeUrl)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown;

        // CommonMark forbids a link inside a link label, so `[[z](javascript:a)](javascript:b)` parses
        // as ONE link (the inner) and literal brackets; collapsing it exposes a fresh outer link. Run to a
        // fixed point (each pass strictly shortens the text, so this terminates; the cap is belt-and-braces).
        var current = markdown;
        for (var i = 0; i < 16; i++)
        {
            var next = NeutralizeOnce(current, isSafeUrl);
            if (next == current)
                return current;
            current = next;
        }
        return current;
    }

    private static string NeutralizeOnce(string markdown, Func<string, bool> isSafeUrl)
    {

        MarkdownDocument doc;
        try
        {
            doc = Markdown.Parse(markdown, Pipeline);
        }
        catch (ArgumentException)
        {
            return markdown;
        }

        var edits = new List<(int Start, int End, string Replacement)>();

        foreach (var link in doc.Descendants<LinkInline>())
        {
            if (isSafeUrl(link.Url ?? string.Empty))
                continue;
            if (link.Span.Start < 0 || link.Span.End < link.Span.Start || link.Span.End >= markdown.Length)
                continue;
            edits.Add((link.Span.Start, link.Span.End, LabelText(link, markdown)));
        }

        foreach (var auto in doc.Descendants<AutolinkInline>())
        {
            if (isSafeUrl(auto.Url ?? string.Empty))
                continue;
            if (auto.Span.Start < 0 || auto.Span.End < auto.Span.Start || auto.Span.End >= markdown.Length)
                continue;
            edits.Add((auto.Span.Start, auto.Span.End, auto.Url ?? string.Empty));
        }

        foreach (var def in doc.Descendants<LinkReferenceDefinition>())
        {
            if (isSafeUrl(def.Url ?? string.Empty))
                continue;
            if (def.Span.Start < 0 || def.Span.End < def.Span.Start || def.Span.End >= markdown.Length)
                continue;
            edits.Add((def.Span.Start, def.Span.End, string.Empty));
        }

        if (edits.Count == 0)
            return markdown;

        // Keep only outermost edits: walk left-to-right, widest first, and drop any edit that lies
        // inside a kept one (a link inside an image label, say) — the outer replacement removes it.
        var kept = new List<(int Start, int End, string Replacement)>();
        foreach (var edit in edits.OrderBy(e => e.Start).ThenByDescending(e => e.End))
        {
            if (kept.Count > 0 && edit.Start <= kept[^1].End)
                continue;
            kept.Add(edit);
        }

        // Apply from the end so earlier offsets stay valid.
        var sb = new StringBuilder(markdown);
        for (var i = kept.Count - 1; i >= 0; i--)
        {
            var (start, end, replacement) = kept[i];
            sb.Remove(start, end - start + 1);
            sb.Insert(start, replacement);
        }
        return sb.ToString();
    }

    /// <summary>The label exactly as written in the source (entities, inline HTML and nested marks
    /// intact — the sanitizer's HTML pass still runs over it afterwards), falling back to a plain-text
    /// walk when Markdig gives no usable child spans.</summary>
    private static string LabelText(LinkInline link, string markdown)
    {
        var first = link.FirstChild;
        var last = link.LastChild;
        if (first is not null && last is not null
            && first.Span.Start >= 0 && last.Span.End >= first.Span.Start && last.Span.End < markdown.Length)
        {
            return markdown.Substring(first.Span.Start, last.Span.End - first.Span.Start + 1);
        }
        return PlainText(link);
    }

    private static string PlainText(ContainerInline container)
    {
        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    sb.Append(lit.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    sb.Append(entity.Transcoded.ToString());
                    break;
                case AutolinkInline auto:
                    sb.Append(auto.Url);
                    break;
                case LineBreakInline:
                    sb.Append(' ');
                    break;
                case ContainerInline nested:
                    sb.Append(PlainText(nested));
                    break;
            }
        }
        return sb.ToString();
    }
}
