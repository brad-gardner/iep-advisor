using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// A small, pure, testable intermediate representation between a stored RichText/markdown value and its
/// QuestPDF rendering in <see cref="AuthoredDocumentPdfDocument"/> — separates "what does this markdown
/// mean" (testable without QuestPDF: bold/italic/strike flags, resolved+validated link hrefs, list
/// marker/depth, quote depth) from "how does QuestPDF lay it out" (only provable by generating a PDF).
///
/// <para><see cref="MarkdownPdfPlanBuilder.Build"/> walks the same Markdig AST
/// (<see cref="MarkdownText.Parse"/>) as <see cref="MarkdownText.ToPlainText"/>, but instead of flattening
/// to a string it produces this plan, which <see cref="AuthoredDocumentPdfDocument"/> renders directly —
/// so "does bold turn into a Bold run" or "does an unsafe-scheme link keep its Href" can be asserted
/// directly against the plan instead of only indirectly via "the PDF didn't throw" (test-reviewer finding,
/// pass1).</para>
///
/// <para><b>Nesting cap (reviewer pass1 P1s: exponential nested lists, deep-blockquote layout exception):
/// </b> blockquote and list nesting both cap their effective rendered depth at
/// <see cref="MarkdownPdfPlanBuilder.MaxNestingDepth"/> — content past that depth still renders (never
/// dropped) but stops adding further indentation/padding/border. For blockquotes this bounds the number
/// of nested, padded/bordered Columns actually created (deeper quote levels flatten straight into the
/// depth-capped one instead of wrapping again), which is what removes the cumulative left-padding that
/// otherwise produced an impossible QuestPDF layout constraint at ~45 levels. For lists this bounds the
/// rendered indent only — see the "flat list" note on <see cref="MarkdownPdfListItem"/> for the structural
/// fix to the exponential-time defect.</para>
/// </summary>
internal sealed record MarkdownPdfPlan(IReadOnlyList<MarkdownPdfBlock> Blocks);

/// <summary>One styled run of text within a paragraph/heading/list-item, already fully resolved: a link's
/// <see cref="Href"/> is set only when <see cref="RichTextSanitizer.IsSafeUrl"/> accepted its scheme — an
/// unsafe scheme (e.g. <c>javascript:</c>) still renders its display text but with a null Href, i.e. plain
/// text rather than a link (reviewer pass1 P1: link scheme bypass).</summary>
internal sealed record MarkdownPdfRun(string Text, bool Bold, bool Italic, bool Strike, string? Href);

internal abstract record MarkdownPdfBlock;

internal sealed record MarkdownPdfParagraph(IReadOnlyList<MarkdownPdfRun> Runs) : MarkdownPdfBlock;

internal sealed record MarkdownPdfHeading(int Level, IReadOnlyList<MarkdownPdfRun> Runs) : MarkdownPdfBlock;

/// <summary><see cref="Depth"/> is the capped (1..<see cref="MarkdownPdfPlanBuilder.MaxNestingDepth"/>)
/// nesting level actually wrapped with a padded/bordered Column; a source blockquote nested deeper than
/// the cap has its remaining content flattened straight into <see cref="Blocks"/> at the cap's depth
/// instead of creating another wrapper (see the class doc on <see cref="MarkdownPdfPlan"/>).</summary>
internal sealed record MarkdownPdfQuote(int Depth, IReadOnlyList<MarkdownPdfBlock> Blocks) : MarkdownPdfBlock;

/// <summary><see cref="Depth"/> is the capped (0..<see cref="MarkdownPdfPlanBuilder.MaxNestingDepth"/>)
/// nesting level, used only to compute the rendered indent (<c>Depth * indent-width</c>). Unlike the
/// original Row-per-level/ConstantItem-marker composition, list items render as a flat sequence with no
/// container nested per depth level — see <see cref="MarkdownPdfPlanBuilder"/>'s remarks on the
/// exponential-time root cause this replaces.</summary>
internal sealed record MarkdownPdfListItem(int Depth, string Marker, IReadOnlyList<MarkdownPdfRun> Runs) : MarkdownPdfBlock;

/// <summary>
/// Builds a <see cref="MarkdownPdfPlan"/> from a stored markdown value by walking Markdig's AST.
///
/// <para><b>Root cause of the exponential nested-list rendering (reviewer pass1 P1):</b> the previous
/// composer built one QuestPDF <c>Row</c> (a <c>ConstantItem</c> marker column + a
/// <c>RelativeItem().Column(...)</c> content column) per nesting level, and recursed into a deeper list
/// <em>inside</em> that inner Column — i.e. Row-in-Column-in-Row-in-Column, once per level. Markdig's own
/// parse of that input is flat (~17ms regardless of depth), so the blowup was confirmed to be in QuestPDF's
/// layout/measurement pass: each Row forces its own size-constraint solve against the parent's available
/// space, and nesting a Row inside a Column inside a Row compounds that solve with every level below it
/// (measured ~14x for 5 more levels, consistent with the measurement cost multiplying per level rather
/// than adding). This builder removes the recursive Row/Column boundary entirely — a list item is a single
/// flat block (<see cref="MarkdownPdfListItem"/>) carrying its own depth, and every item at every nesting
/// level lands in the same flat block list — so <see cref="AuthoredDocumentPdfDocument"/> renders each one
/// as one <c>PaddingLeft(depth * width).Text(...)</c> item in a single Column, with no container nested
/// per level. Layout cost is then linear in total item count, not exponential in nesting depth.</para>
/// </summary>
internal static class MarkdownPdfPlanBuilder
{
    /// <summary>Nesting depth beyond which blockquotes stop wrapping in another padded Column and lists
    /// stop increasing their rendered indent — see the <see cref="MarkdownPdfPlan"/> class doc.</summary>
    internal const int MaxNestingDepth = 8;

    public static MarkdownPdfPlan Build(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkdownPdfPlan(Array.Empty<MarkdownPdfBlock>());

        var document = MarkdownText.Parse(markdown);
        var blocks = new List<MarkdownPdfBlock>();
        AppendBlocks(document, blocks, quoteDepth: 0);
        return new MarkdownPdfPlan(blocks);
    }

    private static void AppendBlocks(ContainerBlock container, List<MarkdownPdfBlock> output, int quoteDepth)
    {
        foreach (var block in container)
        {
            switch (block)
            {
                case ParagraphBlock paragraph when paragraph.Inline != null:
                    output.Add(new MarkdownPdfParagraph(BuildRuns(paragraph.Inline)));
                    break;

                case HeadingBlock heading when heading.Inline != null:
                    output.Add(new MarkdownPdfHeading(heading.Level, BuildRuns(heading.Inline)));
                    break;

                case QuoteBlock quote when quoteDepth >= MaxNestingDepth:
                    // Cap reached: flatten instead of adding another indent/border layer — bounds the
                    // cumulative left padding that otherwise produces a QuestPDF DocumentLayoutException
                    // at deep nesting, and the number of Column wrappers actually created.
                    AppendBlocks(quote, output, quoteDepth);
                    break;

                case QuoteBlock quote:
                {
                    var innerBlocks = new List<MarkdownPdfBlock>();
                    AppendBlocks(quote, innerBlocks, quoteDepth + 1);
                    output.Add(new MarkdownPdfQuote(quoteDepth + 1, innerBlocks));
                    break;
                }

                case ListBlock list:
                    AppendList(list, output, depth: 0);
                    break;

                case LeafBlock leafWithInline when leafWithInline.Inline != null:
                    output.Add(new MarkdownPdfParagraph(BuildRuns(leafWithInline.Inline)));
                    break;

                case LeafBlock rawLeaf:
                {
                    // Unknown/HTML leaf block (e.g. a stray HtmlBlock, thematic break, or a code block
                    // with no parsed inline content) — fall back to its raw text rather than dropping it.
                    var raw = rawLeaf.Lines.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                        output.Add(new MarkdownPdfParagraph(new[] { new MarkdownPdfRun(raw.Trim(), false, false, false, null) }));
                    break;
                }

                case ContainerBlock unknownContainer:
                    // A GFM Table (or any other advanced-extension ContainerBlock: footnote, definition
                    // list, custom container) isn't a LeafBlock, so it matches none of the cases above —
                    // recurse into its children instead of silently dropping the block (reviewer pass1
                    // P1: GFM tables dropped). A Table's TableRow/TableCell children are themselves
                    // ContainerBlocks, so this recurses down to their paragraph content generically.
                    AppendBlocks(unknownContainer, output, quoteDepth);
                    break;
            }
        }
    }

    private static void AppendList(ListBlock list, List<MarkdownPdfBlock> output, int depth)
    {
        var effectiveDepth = Math.Min(depth, MaxNestingDepth);
        var counter = int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
                continue;

            var marker = list.IsOrdered ? $"{counter}." : "•";
            counter++;

            var runs = new List<MarkdownPdfRun>();
            var trailing = new List<MarkdownPdfBlock>();

            foreach (var itemChild in item)
            {
                switch (itemChild)
                {
                    case ListBlock nested:
                        AppendList(nested, trailing, depth + 1);
                        break;

                    case LeafBlock { Inline: { } inline }:
                        runs.AddRange(BuildRuns(inline));
                        break;

                    case ContainerBlock otherContainer:
                        // e.g. a blockquote or table nested inside a list item — degrade via the generic
                        // block walker rather than dropping it; it loses the list marker/indent but its
                        // content is never silently discarded.
                        AppendBlocks(otherContainer, trailing, quoteDepth: 0);
                        break;

                    case LeafBlock otherLeaf:
                    {
                        var raw = otherLeaf.Lines.ToString();
                        if (!string.IsNullOrWhiteSpace(raw))
                            trailing.Add(new MarkdownPdfParagraph(new[] { new MarkdownPdfRun(raw.Trim(), false, false, false, null) }));
                        break;
                    }
                }
            }

            output.Add(new MarkdownPdfListItem(effectiveDepth, marker, runs));
            output.AddRange(trailing);
        }
    }

    // ---------------------------------------------------------------- inline runs

    private static List<MarkdownPdfRun> BuildRuns(Inline? inline)
    {
        var runs = new List<MarkdownPdfRun>();
        AppendRuns(inline, runs, bold: false, italic: false, strike: false);
        return runs;
    }

    private static void AppendRuns(Inline? inline, List<MarkdownPdfRun> runs, bool bold, bool italic, bool strike)
    {
        for (var current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    AddRun(runs, literal.Content.ToString(), bold, italic, strike, null);
                    break;

                case LineBreakInline:
                    AddRun(runs, " ", bold, italic, strike, null);
                    break;

                case CodeInline code:
                    AddRun(runs, code.Content, bold, italic, strike, null);
                    break;

                case AutolinkInline autolink:
                {
                    var url = autolink.Url ?? string.Empty;
                    AddRun(runs, url, bold, italic, strike, SafeHrefOrNull(url));
                    break;
                }

                case LinkInline { IsImage: true } image:
                {
                    var alt = MarkdownText.InlineToPlainText(image.FirstChild);
                    if (!string.IsNullOrWhiteSpace(alt))
                        AddRun(runs, alt, bold, italic, strike, null);
                    break;
                }

                case LinkInline link:
                {
                    var label = MarkdownText.InlineToPlainText(link.FirstChild);
                    var url = link.Url ?? string.Empty;
                    var display = string.IsNullOrWhiteSpace(label) ? url : label;
                    var href = SafeHrefOrNull(url);
                    AddRun(runs, display, bold, italic, strike, href);

                    // Only spell out the url when it adds information and it was accepted as a safe
                    // link — an unsafe scheme's raw url is never echoed as visible text either.
                    if (href != null && !string.IsNullOrEmpty(url) && !string.Equals(display.Trim(), url.Trim(), StringComparison.Ordinal))
                        AddRun(runs, $" ({url})", bold, italic, false, null);
                    break;
                }

                case EmphasisInline { DelimiterChar: '~' } strikethrough:
                    AppendRuns(strikethrough.FirstChild, runs, bold, italic, strike: true);
                    break;

                case EmphasisInline { DelimiterCount: >= 2 } strongEmphasis:
                    AppendRuns(strongEmphasis.FirstChild, runs, true, italic || strongEmphasis.DelimiterCount == 3, strike);
                    break;

                case EmphasisInline emphasis:
                    AppendRuns(emphasis.FirstChild, runs, bold, true, strike);
                    break;

                case HtmlEntityInline entity:
                    AddRun(runs, entity.Transcoded.ToString(), bold, italic, strike, null);
                    break;

                case HtmlInline:
                    // Raw markup that slipped past RichTextSanitizer — dropped (defense in depth).
                    break;

                case ContainerInline container:
                    if (container.FirstChild != null)
                        AppendRuns(container.FirstChild, runs, bold, italic, strike);
                    break;
            }
        }
    }

    private static void AddRun(List<MarkdownPdfRun> runs, string text, bool bold, bool italic, bool strike, string? href)
    {
        if (string.IsNullOrEmpty(text))
            return;
        runs.Add(new MarkdownPdfRun(text, bold, italic, strike, href));
    }

    private static string? SafeHrefOrNull(string url)
        => !string.IsNullOrWhiteSpace(url) && RichTextSanitizer.IsSafeUrl(url) ? url : null;
}
