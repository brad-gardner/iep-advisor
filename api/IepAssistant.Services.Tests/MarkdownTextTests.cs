using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Coverage for <see cref="MarkdownText"/>: flattening a stored RichText/markdown value (GFM subset
/// authored by the TipTap editors) to readable plain text via Markdig's AST rather than regex, so
/// structure (paragraph breaks, list items, headings, quotes) survives instead of collapsing into one
/// run-on line of "**"/"- " syntax.
/// </summary>
public sealed class MarkdownTextTests
{
    [Fact]
    public void ToPlainText_SeparatesParagraphsWithABlankLine()
    {
        var result = MarkdownText.ToPlainText("First paragraph.\n\nSecond paragraph.");

        Assert.Equal("First paragraph.\n\nSecond paragraph.", result);
    }

    [Fact]
    public void ToPlainText_RendersBulletList_AsBulletLines()
    {
        var result = MarkdownText.ToPlainText("- item one\n- item two");

        Assert.Equal("• item one\n• item two", result);
    }

    [Fact]
    public void ToPlainText_RendersNestedBulletList_Indented()
    {
        var result = MarkdownText.ToPlainText("- item one\n  - nested item");

        Assert.Equal("• item one\n  • nested item", result);
    }

    [Fact]
    public void ToPlainText_RendersOrderedList_Numbered()
    {
        var result = MarkdownText.ToPlainText("1. first\n2. second");

        Assert.Equal("1. first\n2. second", result);
    }

    [Fact]
    public void ToPlainText_RendersHeading_OnItsOwnLine_WithoutHashes()
    {
        var result = MarkdownText.ToPlainText("## Heading Two\n\nBody text.");

        Assert.Equal("Heading Two\n\nBody text.", result);
    }

    [Fact]
    public void ToPlainText_PrefixesBlockquoteLines()
    {
        var result = MarkdownText.ToPlainText("> quoted line one\n>\n> quoted line two");

        Assert.Equal("> quoted line one\n> quoted line two", result);
    }

    [Fact]
    public void ToPlainText_RendersLink_AsTextAndUrl_WhenTextDiffersFromUrl()
    {
        var result = MarkdownText.ToPlainText("See [our site](https://example.com) for more.");

        Assert.Equal("See our site (https://example.com) for more.", result);
    }

    [Fact]
    public void ToPlainText_RendersLink_WithoutRepeatingUrl_WhenTextIsTheUrl()
    {
        var result = MarkdownText.ToPlainText("See [https://example.com](https://example.com) for more.");

        Assert.Equal("See https://example.com for more.", result);
        Assert.DoesNotContain("(https://example.com)", result);
    }

    [Theory]
    [InlineData("This is **bold** text.", "bold")]
    [InlineData("This is _italic_ text.", "italic")]
    [InlineData("This is ~~struck~~ text.", "struck")]
    public void ToPlainText_StripsEmphasisMarkers_KeepsInnerText(string markdown, string innerText)
    {
        var result = MarkdownText.ToPlainText(markdown);

        Assert.Contains(innerText, result);
        Assert.DoesNotContain("*", result);
        Assert.DoesNotContain("_", result);
        Assert.DoesNotContain("~", result);
    }

    [Fact]
    public void ToPlainText_StripsHtmlTags_KeepsInnerText_AndDecodesEntities()
    {
        var result = MarkdownText.ToPlainText("Hello <b>world</b> &amp; friends");

        Assert.Equal("Hello world & friends", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void ToPlainText_ReturnsEmptyString_ForEmptyOrWhitespaceInput(string? markdown)
    {
        var result = MarkdownText.ToPlainText(markdown);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_TrimsAndConvertsCrlfToLf_WithoutTouchingMarkdownSyntax()
    {
        var result = MarkdownText.Normalize("  \r\n**Bold**\r\n\r\n- item\r\n  ");

        Assert.Equal("**Bold**\n\n- item", result);
    }

    [Fact]
    public void Normalize_ReturnsEmptyString_ForNullOrEmptyInput()
    {
        Assert.Equal(string.Empty, MarkdownText.Normalize(null));
        Assert.Equal(string.Empty, MarkdownText.Normalize(""));
    }

    // ------------------------------------------------------------ Reviewer pass1 P2 gaps: tables,
    // thematic breaks, fenced code blocks, inline code, hard line breaks, deep nesting -- exactly the
    // inputs where the AST walk previously turned out to be incomplete (a GFM Table is a ContainerBlock,
    // not a LeafBlock, so it matched none of the switch's cases and silently vanished).

    [Fact]
    public void ToPlainText_PipeTable_DoesNotDropCellText()
    {
        var markdown = "| Name | Score |\n| --- | --- |\n| Alex | 92 |\n| Sam | 88 |";

        var result = MarkdownText.ToPlainText(markdown);

        Assert.Contains("Name", result);
        Assert.Contains("Score", result);
        Assert.Contains("Alex", result);
        Assert.Contains("92", result);
        Assert.Contains("Sam", result);
        Assert.Contains("88", result);
    }

    [Fact]
    public void ToPlainText_PipeTableBetweenParagraphs_KeepsSurroundingText_AndTableText()
    {
        var markdown = "Before the table.\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\nAfter the table.";

        var result = MarkdownText.ToPlainText(markdown);

        Assert.Contains("Before the table.", result);
        Assert.Contains("1", result);
        Assert.Contains("2", result);
        Assert.Contains("After the table.", result);
    }

    [Fact]
    public void ToPlainText_ThematicBreak_DoesNotThrow_AndKeepsSurroundingParagraphs()
    {
        var result = MarkdownText.ToPlainText("Before.\n\n---\n\nAfter.");

        Assert.Contains("Before.", result);
        Assert.Contains("After.", result);
    }

    [Fact]
    public void ToPlainText_FencedCodeBlock_KeepsCodeText()
    {
        var markdown = "Before.\n\n```\nvar x = 1;\n```\n\nAfter.";

        var result = MarkdownText.ToPlainText(markdown);

        Assert.Contains("Before.", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("After.", result);
    }

    [Fact]
    public void ToPlainText_InlineCode_KeepsCodeText()
    {
        var result = MarkdownText.ToPlainText("Run `npm install` first.");

        Assert.Contains("npm install", result);
    }

    [Fact]
    public void ToPlainText_HardLineBreak_KeepsBothLinesOfText()
    {
        // Two trailing spaces before the newline is CommonMark's hard line break syntax.
        var result = MarkdownText.ToPlainText("Line one.  \nLine two.");

        Assert.Contains("Line one.", result);
        Assert.Contains("Line two.", result);
    }

    [Fact]
    public void ToPlainText_DeeplyNestedList_DoesNotThrow_AndKeepsAllItemText()
    {
        var markdown = string.Join("\n", Enumerable.Range(0, 20).Select(i => new string(' ', i * 2) + "- item " + i));

        var result = MarkdownText.ToPlainText(markdown);

        for (var i = 0; i < 20; i++)
            Assert.Contains("item " + i, result);
    }

    [Fact]
    public void ToPlainText_DeeplyNestedBlockquote_DoesNotThrow_AndKeepsText()
    {
        var markdown = new string('>', 30) + " deep quote text";

        var result = MarkdownText.ToPlainText(markdown);

        Assert.Contains("deep quote text", result);
    }
}
