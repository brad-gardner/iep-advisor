using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Flattens a stored RichText/markdown value (GFM subset authored by the TipTap editors: paragraphs,
/// <c>**bold**</c>, <c>_italic_</c>, <c>~~strike~~</c>, <c>##</c>/<c>###</c> headings, <c>-</c> and
/// <c>1.</c> lists, <c>&gt;</c> quotes, <c>[text](url)</c> links) into readable plain text, and provides
/// the trivial normalization used before hashing/prompting a value.
///
/// <para>Parses via Markdig's AST rather than regex so structural breaks (paragraphs, list items,
/// headings, quotes) survive the flattening instead of collapsing into one run-on line, and any raw HTML
/// that slipped past <see cref="RichTextSanitizer"/> (defense in depth — sanitized values should never
/// contain it) degrades to plain text/nothing rather than passing through.</para>
///
/// <para><see cref="ParsePipeline"/>/<see cref="InlineToPlainText"/> are shared with
/// <c>AuthoredDocumentPdfDocument</c>'s markdown block renderer so both places parse with the exact same
/// GFM configuration and flatten inline runs (e.g. a link's label) the exact same way.</para>
/// </summary>
public static class MarkdownText
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ExcessBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>The shared Markdig pipeline (GFM basics + advanced extensions) used to parse a stored
    /// markdown value — exposed so the PDF composer parses identically instead of re-declaring a pipeline.
    ///
    /// <para><b>Never throws (reviewer pass2 P1):</b> Markdig's own parser enforces an internal
    /// <c>MarkdownPipeline.MaximumNestingDepth</c> guard (default 128, decompiled/confirmed against the
    /// installed Markdig 1.3.2 — <c>MarkdownPipelineBuilder.MaximumNestingDepth</c> is a public settable
    /// property, but lowering it only moves the threshold earlier; it does not remove the guard, so it
    /// cannot eliminate the throw for arbitrarily deep input) and throws a bare <see cref="ArgumentException"/>
    /// from deep inside <c>Markdig.Parsers.MarkdownParser.ProcessInlines</c> once parsed nesting exceeds
    /// it — reachable via pure blockquote/list nesting or, at an even shallower depth, alternating
    /// list/quote nesting (e.g. a pasted long reply chain that also has bullet points). This runs entirely
    /// inside <c>Markdown.Parse</c>, before <see cref="MarkdownPdfPlanBuilder"/>'s own
    /// <c>MaxNestingDepth</c> AST-level cap ever gets a chance to run (that cap only bounds an
    /// already-successfully-parsed tree). Because the frozen document version this is called for never
    /// changes, an uncaught throw here means the PDF for that version can never be generated, on any
    /// retry — so this method degrades instead: on the depth-limit exception, it falls back to a
    /// single-paragraph document whose one <see cref="LiteralInline"/> is the raw, unparsed text (markdown
    /// markers rendered as literal characters). Degraded rendering beats a permanently failed PDF.</para>
    /// </summary>
    public static MarkdownDocument Parse(string markdown)
    {
        try
        {
            return Markdown.Parse(markdown, Pipeline);
        }
        catch (ArgumentException)
        {
            // Markdig's internal depth-limit guard (see remarks above) — degrade to the raw text as a
            // single literal paragraph rather than let this propagate to the PDF services' generic catch
            // and permanently strand that document version in a retryable-but-never-succeeding Error state.
            return BuildFallbackDocument(markdown);
        }
    }

    private static MarkdownDocument BuildFallbackDocument(string markdown)
    {
        var inline = new ContainerInline();
        inline.AppendChild(new LiteralInline(markdown));

        var document = new MarkdownDocument();
        document.Add(new ParagraphBlock { Inline = inline });
        return document;
    }

    /// <summary>
    /// Markdown → readable plain text: paragraphs separated by a blank line; list items as "• item" /
    /// "1. item" lines (nested lists indented); headings on their own line; blockquote lines prefixed
    /// "&gt; "; a link renders as "text (url)" only when the link text differs from the url; emphasis
    /// markers are removed (bold/italic/strikethrough all flatten to their inner text); any raw HTML is
    /// stripped and entities are decoded. Empty/whitespace input returns "".
    /// </summary>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var document = Parse(markdown);
        var blocks = new List<string>();
        AppendBlocks(document, blocks, indent: 0);

        var joined = string.Join("\n\n", blocks.Where(b => !string.IsNullOrEmpty(b)));

        // Defense in depth: a sanitized value should never carry raw HTML, but if any literal tag text
        // reached this far (e.g. inside an HtmlBlock/HtmlInline node), strip it before decoding entities.
        var stripped = HtmlTag.Replace(joined, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped);
        return ExcessBlankLines.Replace(decoded, "\n\n").Trim();
    }

    /// <summary>
    /// Trims the value and normalizes line endings (<c>\r\n</c> → <c>\n</c>) — used before hashing or
    /// prompting so equivalent content is byte-identical regardless of the authoring client's line-ending
    /// convention. Deliberately does not touch markdown syntax.
    /// </summary>
    public static string Normalize(string? markdown)
        => string.IsNullOrEmpty(markdown) ? string.Empty : markdown.Replace("\r\n", "\n").Trim();

    // ---------------------------------------------------------------- AST walking (blocks)

    private static void AppendBlocks(ContainerBlock container, List<string> output, int indent)
    {
        var prefix = indent > 0 ? new string(' ', indent * 2) : string.Empty;

        foreach (var block in container)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                {
                    var text = InlineToPlainText(paragraph.Inline);
                    if (!string.IsNullOrWhiteSpace(text))
                        output.Add(prefix + text);
                    break;
                }

                case HeadingBlock heading:
                {
                    var text = InlineToPlainText(heading.Inline);
                    if (!string.IsNullOrWhiteSpace(text))
                        output.Add(prefix + text);
                    break;
                }

                case QuoteBlock quote:
                {
                    var quoteBlocks = new List<string>();
                    AppendBlocks(quote, quoteBlocks, indent: 0);
                    var quotedLines = quoteBlocks
                        .SelectMany(b => b.Split('\n'))
                        .Select(line => "> " + line);
                    var quoted = string.Join("\n" + prefix, quotedLines);
                    if (!string.IsNullOrWhiteSpace(quoted))
                        output.Add(prefix + quoted);
                    break;
                }

                case ListBlock list:
                {
                    var listLines = new List<string>();
                    AppendList(list, listLines, indent);
                    if (listLines.Count > 0)
                        output.Add(string.Join("\n", listLines));
                    break;
                }

                case LeafBlock leafWithInline when leafWithInline.Inline != null:
                {
                    var text = InlineToPlainText(leafWithInline.Inline);
                    if (!string.IsNullOrWhiteSpace(text))
                        output.Add(prefix + text);
                    break;
                }

                case LeafBlock rawLeaf:
                {
                    // Unknown/HTML leaf block (e.g. a stray HtmlBlock or code block) — fall back to its
                    // raw text; the caller's final HTML-strip/entity-decode pass cleans up any markup.
                    var raw = rawLeaf.Lines.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                        output.Add(prefix + raw.Trim());
                    break;
                }

                case ContainerBlock unknownContainer:
                    // A GFM Table (or any other advanced-extension ContainerBlock: footnote, definition
                    // list, custom container) isn't a LeafBlock, so it matched none of the cases above —
                    // recurse into its children instead of silently dropping the block (reviewer pass1
                    // P1: GFM tables dropped). A Table's TableRow/TableCell children are themselves
                    // ContainerBlocks, so this recurses down to their paragraph content generically.
                    AppendBlocks(unknownContainer, output, indent);
                    break;
            }
        }
    }

    private static void AppendList(ListBlock list, List<string> output, int indent)
    {
        var prefix = new string(' ', indent * 2);
        var counter = int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
                continue;

            var marker = list.IsOrdered ? $"{counter}. " : "• ";
            counter++;

            var itemTextParts = new List<string>();
            var nestedListLines = new List<string>();

            foreach (var itemChild in item)
            {
                if (itemChild is ListBlock nested)
                {
                    AppendList(nested, nestedListLines, indent + 1);
                }
                else if (itemChild is LeafBlock leaf && leaf.Inline != null)
                {
                    var text = InlineToPlainText(leaf.Inline);
                    if (!string.IsNullOrWhiteSpace(text))
                        itemTextParts.Add(text);
                }
            }

            output.Add(prefix + marker + string.Join(" ", itemTextParts));
            output.AddRange(nestedListLines);
        }
    }

    // ---------------------------------------------------------------- AST walking (inlines)

    /// <summary>Flattens an inline run (a paragraph/heading/list-item's content) to plain text: emphasis
    /// markers are dropped (bold/italic/strikethrough all just descend into their inner text), a link
    /// becomes "text (url)" only when the text differs from the url, and raw HTML inlines are dropped.
    /// Shared with the PDF composer for flattening a link's label the same way.</summary>
    internal static string InlineToPlainText(Inline? inline)
    {
        if (inline == null)
            return string.Empty;
        var sb = new StringBuilder();
        AppendInlinePlainText(inline, sb);
        return sb.ToString();
    }

    private static void AppendInlinePlainText(Inline inline, StringBuilder sb)
    {
        for (var current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;

                case LineBreakInline:
                    sb.Append(' ');
                    break;

                case CodeInline code:
                    sb.Append(code.Content);
                    break;

                case AutolinkInline autolink:
                    sb.Append(autolink.Url ?? string.Empty);
                    break;

                case LinkInline { IsImage: true } image:
                {
                    var alt = InlineToPlainText(image.FirstChild);
                    if (!string.IsNullOrWhiteSpace(alt))
                        sb.Append(alt);
                    break;
                }

                case LinkInline link:
                {
                    var label = InlineToPlainText(link.FirstChild);
                    var url = link.Url ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(label))
                        sb.Append(url);
                    else if (!string.IsNullOrEmpty(url) && !string.Equals(label.Trim(), url.Trim(), StringComparison.Ordinal))
                        sb.Append(label).Append(" (").Append(url).Append(')');
                    else
                        sb.Append(label);
                    break;
                }

                case HtmlEntityInline entity:
                    sb.Append(entity.Transcoded.ToString());
                    break;

                case HtmlInline:
                    // Raw inline markup slipped past the sanitizer — drop it (defense in depth); the
                    // caller's final tag-strip pass also cleans up anything not caught here.
                    break;

                case ContainerInline container:
                    if (container.FirstChild != null)
                        AppendInlinePlainText(container.FirstChild, sb);
                    break;
            }
        }
    }
}
