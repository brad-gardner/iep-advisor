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
}
