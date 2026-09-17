using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Coverage for <see cref="MarkdownPdfPlanBuilder"/> (reviewer pass1 P2: the previous PDF-composer tests
/// only proved "doesn't throw", never that bold/list/link formatting was actually produced). This plan
/// is the pure, QuestPDF-free step between a stored markdown value and its PDF rendering, so these tests
/// assert the actual formatting decisions directly instead of only end-to-end via a generated PDF's byte
/// count -- and, in particular, that an unsafe-scheme link never keeps its Href (reviewer pass1 P1: link
/// scheme bypass), which is the assertion <see cref="AuthoredDocumentPdfDocumentTests"/> cannot make
/// without a PDF text-extraction dependency.
/// </summary>
public sealed class MarkdownPdfPlanBuilderTests
{
    [Fact]
    public void Build_EmptyOrWhitespace_ReturnsNoBlocks()
    {
        Assert.Empty(MarkdownPdfPlanBuilder.Build(null).Blocks);
        Assert.Empty(MarkdownPdfPlanBuilder.Build("").Blocks);
        Assert.Empty(MarkdownPdfPlanBuilder.Build("   \n  ").Blocks);
    }

    [Fact]
    public void Build_Bold_ProducesRun_WithBoldFlag_AndNoAsterisks()
    {
        var plan = MarkdownPdfPlanBuilder.Build("This is **bold** text.");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var boldRun = Assert.Single(paragraph.Runs, r => r.Text == "bold");
        Assert.True(boldRun.Bold);
        Assert.False(boldRun.Italic);
        Assert.False(boldRun.Strike);
        Assert.All(paragraph.Runs, r => Assert.DoesNotContain("**", r.Text));
    }

    [Fact]
    public void Build_Italic_ProducesRun_WithItalicFlag_AndNoUnderscores()
    {
        var plan = MarkdownPdfPlanBuilder.Build("This is _italic_ text.");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "italic");
        Assert.True(run.Italic);
        Assert.False(run.Bold);
    }

    [Fact]
    public void Build_Strikethrough_ProducesRun_WithStrikeFlag_AndNoTildes()
    {
        var plan = MarkdownPdfPlanBuilder.Build("This is ~~struck~~ text.");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "struck");
        Assert.True(run.Strike);
    }

    [Fact]
    public void Build_BoldItalic_TripleDelimiter_SetsBothFlags()
    {
        var plan = MarkdownPdfPlanBuilder.Build("This is ***both*** text.");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "both");
        Assert.True(run.Bold);
        Assert.True(run.Italic);
    }

    [Fact]
    public void Build_SafeLink_SetsHref()
    {
        var plan = MarkdownPdfPlanBuilder.Build("[our site](https://a)");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "our site");
        Assert.Equal("https://a", run.Href);
    }

    [Theory]
    [InlineData("[click here](javascript:alert(1))")]
    [InlineData("[click here](data:text/html;base64,SGk=)")]
    public void Build_UnsafeSchemeLink_HrefIsNull_AndDisplayTextSurvives(string markdown)
    {
        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "click here");
        Assert.Null(run.Href);
        Assert.All(paragraph.Runs, r => Assert.DoesNotContain("javascript:", r.Text));
        Assert.All(paragraph.Runs, r => Assert.DoesNotContain("data:", r.Text));
    }

    [Fact]
    public void Build_UnsafeAutolink_HrefIsNull()
    {
        var plan = MarkdownPdfPlanBuilder.Build("Contact <javascript:alert(1)>.");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        Assert.All(paragraph.Runs, r => Assert.Null(r.Href));
    }

    [Fact]
    public void Build_NestedLists_TrackDepth()
    {
        var plan = MarkdownPdfPlanBuilder.Build("- top\n  - middle\n    - bottom");

        var items = plan.Blocks.OfType<MarkdownPdfListItem>().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal(0, items[0].Depth);
        Assert.Equal(1, items[1].Depth);
        Assert.Equal(2, items[2].Depth);
    }

    [Fact]
    public void Build_ListNestingBeyondCap_StopsIncreasingDepth()
    {
        var markdown = string.Join("\n", Enumerable.Range(0, 12).Select(i => new string(' ', i * 2) + "- item " + i));

        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        var items = plan.Blocks.OfType<MarkdownPdfListItem>().ToList();
        Assert.Equal(12, items.Count);
        Assert.All(items, item => Assert.InRange(item.Depth, 0, MarkdownPdfPlanBuilder.MaxNestingDepth));
        Assert.Equal(MarkdownPdfPlanBuilder.MaxNestingDepth, items[^1].Depth);
    }

    [Fact]
    public void Build_QuoteNestingBeyondCap_StopsIncreasingDepth()
    {
        var markdown = new string('>', 20) + " deep quote";

        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        // Only MaxNestingDepth quote wrappers are ever created; walk down to the innermost one.
        MarkdownPdfQuote? quote = Assert.IsType<MarkdownPdfQuote>(Assert.Single(plan.Blocks));
        var depths = new List<int> { quote.Depth };
        while (quote!.Blocks.Count == 1 && quote.Blocks[0] is MarkdownPdfQuote nested)
        {
            quote = nested;
            depths.Add(quote.Depth);
        }

        Assert.All(depths, d => Assert.InRange(d, 1, MarkdownPdfPlanBuilder.MaxNestingDepth));
        Assert.Equal(MarkdownPdfPlanBuilder.MaxNestingDepth, depths[^1]);

        // The innermost quote's own Blocks never contain another MarkdownPdfQuote wrapper -- deeper
        // source nesting flattened straight into it instead of compounding further.
        Assert.DoesNotContain(quote.Blocks, b => b is MarkdownPdfQuote);
    }

    [Fact]
    public void Build_PipeTable_DoesNotDropCellText()
    {
        var markdown = "| Name | Score |\n| --- | --- |\n| Alex | 92 |\n| Sam | 88 |";

        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        var allText = string.Join(" ", plan.Blocks
            .OfType<MarkdownPdfParagraph>()
            .SelectMany(p => p.Runs)
            .Select(r => r.Text));

        Assert.Contains("Name", allText);
        Assert.Contains("Score", allText);
        Assert.Contains("Alex", allText);
        Assert.Contains("92", allText);
        Assert.Contains("Sam", allText);
        Assert.Contains("88", allText);
    }

    [Fact]
    public void Build_NoRunTextEverContainsRawMarkdownSyntax()
    {
        var markdown = "# Heading\n\n" +
            "**bold** _italic_ ~~strike~~ [link](https://example.com) plain.\n\n" +
            "- item one\n- item two\n\n" +
            "> a quote";

        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        var allRuns = CollectAllRuns(plan.Blocks);
        Assert.NotEmpty(allRuns);
        Assert.All(allRuns, r =>
        {
            Assert.DoesNotContain("**", r.Text);
            Assert.DoesNotContain("##", r.Text);
            Assert.DoesNotContain("~~", r.Text);
            Assert.DoesNotContain("](", r.Text);
        });
    }

    // ------------------------------------------------------------ Reviewer pass2 P1: Markdig's own
    // internal depth-limit guard (default 128) throws inside Markdown.Parse itself, before this builder's
    // own MaxNestingDepth cap (which operates on an already-parsed AST) ever runs. MarkdownText.Parse
    // catches it and falls back to a single literal paragraph -- confirm Build never throws either, since
    // it shares that same Parse call.

    [Fact]
    public void Build_TwoHundredDeepBlockquote_DoesNotThrow_AndProducesNonEmptyBlocks()
    {
        var markdown = new string('>', 200) + " leafword";

        var plan = MarkdownPdfPlanBuilder.Build(markdown);

        Assert.NotEmpty(plan.Blocks);
        var allText = string.Join(" ", CollectAllRuns(plan.Blocks).Select(r => r.Text));
        Assert.Contains("leafword", allText);
    }

    [Fact]
    public void Build_OneHundredFiftyLevelAlternatingListAndQuote_DoesNotThrow()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 150; i++)
        {
            var indent = new string(' ', i * 2);
            sb.Append(indent).Append(i % 2 == 0 ? "- item" + i : "> quote" + i).Append('\n');
        }
        sb.Append(new string(' ', 150 * 2)).Append("leafword");

        var plan = MarkdownPdfPlanBuilder.Build(sb.ToString());

        Assert.NotEmpty(plan.Blocks);
    }

    // ------------------------------------------------------------ Reviewer pass2 P2/#3: a reference-style
    // link's URL is resolved entirely from a separate definition line -- confirm the render-time
    // Markdig-AST-based safety check (independent of RichTextSanitizer's persist-time regex/scanner) still
    // nulls an unsafe scheme reached this way.

    [Fact]
    public void Build_ReferenceStyleLink_UnsafeScheme_HrefIsNull()
    {
        var plan = MarkdownPdfPlanBuilder.Build("[click here][r]\n\n[r]: javascript:alert(1)");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "click here");
        Assert.Null(run.Href);
    }

    [Fact]
    public void Build_ReferenceStyleLink_SafeScheme_SetsHref()
    {
        var plan = MarkdownPdfPlanBuilder.Build("[our site][r]\n\n[r]: https://example.com");

        var paragraph = Assert.IsType<MarkdownPdfParagraph>(Assert.Single(plan.Blocks));
        var run = Assert.Single(paragraph.Runs, r => r.Text == "our site");
        Assert.Equal("https://example.com", run.Href);
    }

    private static List<MarkdownPdfRun> CollectAllRuns(IReadOnlyList<MarkdownPdfBlock> blocks)
    {
        var runs = new List<MarkdownPdfRun>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case MarkdownPdfParagraph p: runs.AddRange(p.Runs); break;
                case MarkdownPdfHeading h: runs.AddRange(h.Runs); break;
                case MarkdownPdfListItem li: runs.AddRange(li.Runs); break;
                case MarkdownPdfQuote q: runs.AddRange(CollectAllRuns(q.Blocks)); break;
            }
        }
        return runs;
    }
}
