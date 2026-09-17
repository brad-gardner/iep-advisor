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
            edits.Add((link.Span.Start, link.Span.End, LabelText(link)));
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

        // Apply from the end so earlier offsets stay valid; drop any edit nested inside one already applied
        // (a link inside an image label, say) since the outer replacement already removed it.
        var sb = new StringBuilder(markdown);
        var appliedStart = int.MaxValue;
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.End))
        {
            if (end >= appliedStart)
                continue;
            sb.Remove(start, end - start + 1);
            sb.Insert(start, replacement);
            appliedStart = start;
        }
        return sb.ToString();
    }

    private static string LabelText(ContainerInline container)
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
                case LineBreakInline:
                    sb.Append(' ');
                    break;
                case ContainerInline nested:
                    sb.Append(LabelText(nested));
                    break;
            }
        }
        return sb.ToString();
    }
}
