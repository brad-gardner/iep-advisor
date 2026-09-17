using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Coverage for <see cref="RichTextSanitizer"/> (State Document Template Engine, cross-cutting G-c.5):
/// the conservative allowlist that runs before a RichText field value is persisted. Each vector is a
/// separate InlineData case: whole dangerous blocks are removed WITH their content, comments are
/// removed, disallowed tags are stripped but their inner text survives, on*/style attributes are
/// dropped from allowed tags, allowed formatting tags pass through, and only safe &lt;a href&gt;
/// schemes are retained (javascript:/data:/protocol-relative are rejected to a bare &lt;a&gt;).
/// </summary>
public sealed class RichTextSanitizerTests
{
    [Theory]
    [InlineData("script")]
    [InlineData("style")]
    [InlineData("iframe")]
    [InlineData("object")]
    [InlineData("embed")]
    [InlineData("noscript")]
    [InlineData("template")]
    public void Sanitize_RemovesDangerousBlock_WithItsContent(string tag)
    {
        var input = $"<p>before</p><{tag}>DANGER-payload</{tag}><p>after</p>";

        var result = RichTextSanitizer.Sanitize(input);

        Assert.DoesNotContain("DANGER-payload", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"<{tag}", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("<p>before</p><p>after</p>", result);
    }

    [Fact]
    public void Sanitize_RemovesHtmlComments_WithTheirContent()
    {
        var result = RichTextSanitizer.Sanitize("<p>a</p><!-- secret <b>x</b> note -->tail");

        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("<!--", result);
        Assert.Equal("<p>a</p>tail", result);
    }

    [Fact]
    public void Sanitize_StripsDisallowedTag_ButKeepsInnerText()
    {
        var result = RichTextSanitizer.Sanitize("<marquee>scrolling text</marquee>");

        Assert.DoesNotContain("<marquee", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("scrolling text", result);
    }

    [Fact]
    public void Sanitize_DropsEventHandlerAndStyleAttributes_OnAllowedTag()
    {
        var result = RichTextSanitizer.Sanitize("<p onclick=\"evil()\" style=\"color:red\">hi</p>");

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("<p>hi</p>", result);
    }

    [Theory]
    [InlineData("<p>x</p>")]
    [InlineData("<b>x</b>")]
    [InlineData("<strong>x</strong>")]
    [InlineData("<em>x</em>")]
    [InlineData("<u>x</u>")]
    [InlineData("<ul><li>x</li></ul>")]
    [InlineData("<ol><li>x</li></ol>")]
    [InlineData("<h1>x</h1>")]
    [InlineData("<blockquote>x</blockquote>")]
    [InlineData("<pre><code>x</code></pre>")]
    public void Sanitize_KeepsAllowedFormattingTags_Unchanged(string input)
    {
        // Attribute-free allowed markup round-trips verbatim.
        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?q=1")]
    [InlineData("mailto:teacher@example.com")]
    [InlineData("/foo/bar")]
    [InlineData("/")]
    [InlineData("#section")]
    public void Sanitize_Anchor_KeepsSafeHref(string url)
    {
        var result = RichTextSanitizer.Sanitize($"<a href=\"{url}\">link</a>");

        Assert.Equal($"<a href=\"{url}\">link</a>", result);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,SGk=")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    public void Sanitize_Anchor_RejectsUnsafeHref_RendersBareAnchor(string url)
    {
        var result = RichTextSanitizer.Sanitize($"<a href=\"{url}\">link</a>");

        Assert.DoesNotContain("href", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("<a>link</a>", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_NullOrEmpty_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, RichTextSanitizer.Sanitize(input));
    }

    // ------------------------------------------------------------ Markdown link/autolink scheme gate
    // (reviewer pass1 P1: the persisted RichText format is now GFM markdown, not HTML, so
    // "[text](url)" and "<url>" carry no angle brackets around the url and never hit the <a href> path
    // above -- an unsafe scheme there would otherwise reach AuthoredDocumentPdfDocument's Hyperlink()
    // sink unvalidated.)

    [Fact]
    public void Sanitize_MarkdownLink_UnsafeJavascriptScheme_RewritesToPlainText()
    {
        var result = RichTextSanitizer.Sanitize("See [x](javascript:alert(1)) for detail.");

        Assert.Equal("See x for detail.", result);
        Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public void Sanitize_MarkdownLink_UnsafeDataScheme_RewritesToPlainText()
    {
        var result = RichTextSanitizer.Sanitize("See [x](data:text/html;base64,SGk=) for detail.");

        Assert.Equal("See x for detail.", result);
        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Sanitize_Autolink_UnsafeJavascriptScheme_RewritesToPlainText()
    {
        var result = RichTextSanitizer.Sanitize("Contact <javascript:alert(1)> now.");

        Assert.Equal("Contact javascript:alert(1) now.", result);
    }

    [Theory]
    [InlineData("[x](https://ok)")]
    [InlineData("[x](mailto:a@b)")]
    [InlineData("[x](/relative)")]
    public void Sanitize_MarkdownLink_SafeScheme_SurvivesUnchanged(string input)
    {
        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }

    // ------------------------------------------------------------ Reviewer pass2 P2: nested parens and
    // reference-style links. The old MarkdownLink regex only matched one balanced `(...)` pair inside a
    // bare destination and had no awareness of `[label]: url` definitions at all, so both forms persisted
    // completely unmodified even though Markdig resolves both to a real, hyperlink-able URL.

    [Fact]
    public void Sanitize_MarkdownLink_UnsafeScheme_NestedParens_RewritesToPlainText()
    {
        var result = RichTextSanitizer.Sanitize("[x](javascript:alert((1)))");

        Assert.Equal("x", result);
        Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public void Sanitize_MarkdownLink_UnsafeScheme_WithTitle_RewritesToPlainText()
    {
        var result = RichTextSanitizer.Sanitize("[x](javascript:a \"t\")");

        Assert.Equal("x", result);
        Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public void Sanitize_ReferenceStyleLink_UnsafeDefinition_RemovesDefinitionLine()
    {
        var result = RichTextSanitizer.Sanitize("[x][r]\n\n[r]: javascript:alert(1)");

        Assert.DoesNotContain("javascript:", result);
        Assert.Contains("[x][r]", result); // usage left untouched -- Markdig can no longer resolve it
    }

    [Fact]
    public void Sanitize_ReferenceDefinition_UppercaseUnsafeScheme_IsRemoved()
    {
        var result = RichTextSanitizer.Sanitize("[r]: JAVASCRIPT:x");

        Assert.DoesNotContain("JAVASCRIPT:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_ReferenceDefinition_DestinationOnNextLine_UnsafeScheme_IsRemoved()
    {
        // CommonMark (confirmed against the installed Markdig 1.3.2) allows the destination to start on
        // the line after the colon.
        var result = RichTextSanitizer.Sanitize("[r]:\n  javascript:x");

        Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public void Sanitize_ReferenceDefinition_SafeScheme_SurvivesByteForByte()
    {
        var input = "[x][r]\n\n[r]: https://example.com";

        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_MixedSafeAndUnsafeReferenceDefinitions_KeepsSafeOne_RemovesUnsafeOne()
    {
        var input = "[a][s]\n[b][u]\n\n[s]: https://example.com\n[u]: javascript:alert(1)";

        var result = RichTextSanitizer.Sanitize(input);

        Assert.Contains("[s]: https://example.com", result);
        Assert.DoesNotContain("javascript:", result);
    }

    // CommonMark backslash escapes: `\]` inside a label does not end it, and `\(`/`\)` inside a
    // destination are literal characters -- Markdig resolves all of these as real links, so the gate
    // must see through them (review pass 3, agent-smith + test-reviewer).
    [Theory]
    [InlineData(@"[x la\]bel](javascript:alert(1))", "javascript:")]
    [InlineData(@"[x](javascript:a\(b)", "javascript:")]
    [InlineData(@"[x](javascript:a\)b)", "javascript:")]
    [InlineData(@"[x](javascript:alert(1) ""ti(tle"")", "javascript:")]
    [InlineData(@"[x](javascript:alert(1) 'ti)tle')", "javascript:")]
    [InlineData(@"![x](javascript:alert(1) ""t(t"")", "javascript:")]
    [InlineData("[x](javascript:alert(1) \"a\nb\")", "javascript:")]
    [InlineData("[x](javascript:alert(1)\n\"t\")", "javascript:")]
    [InlineData("[x](javascript:alert(1)\r\n  't')", "javascript:")]
    public void Sanitize_MarkdownLink_UnsafeScheme_WithBackslashEscapes_IsNeutralized(string input, string forbidden)
    {
        var result = RichTextSanitizer.Sanitize(input);

        Assert.DoesNotContain(forbidden, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x", result);
    }

    [Theory]
    [InlineData("[la\\]bel]: javascript:alert(1)\n\n[x][la\\]bel]")]
    [InlineData("[r]: javascript:a\\(b\n\n[x][r]")]
    [InlineData("[r]: javascript:alert(1) \"a\nb\"\n\n[x][r]")]
    public void Sanitize_ReferenceDefinition_UnsafeScheme_WithBackslashEscapes_IsRemoved(string input)
    {
        var result = RichTextSanitizer.Sanitize(input);

        Assert.DoesNotContain("javascript:", result);
        Assert.Contains("[x]", result);
    }

    [Fact]
    public void Sanitize_MarkdownLink_UnsafeScheme_TitleInterruptedByBlankLine_IsNotALinkAndIsLeftAlone()
    {
        // Markdig does not resolve a title across a blank line, so this is not a link; the text is
        // left as-is and the sanitizer must not throw or loop.
        var input = "[x](javascript:alert(1) \"a\n\nb\")";

        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_MarkdownLink_SafeScheme_WithParensInTitle_SurvivesByteForByte()
    {
        var input = @"[x](https://example.com ""a (b) title"")";

        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_MarkdownLink_SafeScheme_WithEscapedParens_SurvivesByteForByte()
    {
        var input = @"[wiki](https://example.com/a\(b\))";

        Assert.Equal(input, RichTextSanitizer.Sanitize(input));
    }
}
