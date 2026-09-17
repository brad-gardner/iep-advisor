using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The AST pass alone (no regex scanners in front of it) must neutralise every shape review passes 2–4
/// found the hand-written scanners missing, because it follows Markdig's own resolution.
/// </summary>
public sealed class MarkdownLinkAstTests
{
    private static string Run(string input) => MarkdownLinkAst.NeutralizeUnsafeLinks(input, RichTextSanitizer.IsSafeUrl);

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](javascript:alert((1)))")]
    [InlineData(@"[x](javascript:a\(b)")]
    [InlineData(@"[x la\]bel](javascript:alert(1))")]
    [InlineData("[x](javascript:alert(1) \"ti(tle\")")]
    [InlineData("[x](javascript:alert(1) \"a\nb\")")]
    [InlineData("[x](javascript:alert(1)\n\"t\")")]
    [InlineData("[x](<javascript:alert(1)>)")]
    [InlineData("[x](&#106;avascript:alert(1))")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("[x][r]\n\n[r]: javascript:alert(1)")]
    [InlineData("[x][r]\n\n[r]: javascript:alert(1) \"a\nb\"")]
    [InlineData("[x]\n\n[x]: JAVASCRIPT:alert(1)")]
    public void UnsafeLink_CollapsesToLabelAndSchemeDisappears(string input)
    {
        var result = Run(input);

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&#106;", result);
        Assert.Contains("x", result);
    }

    [Fact]
    public void UnsafeAutolink_BecomesBareText()
    {
        Assert.Equal("see javascript:alert(1) now", Run("see <javascript:alert(1)> now"));
    }

    [Theory]
    [InlineData("[x](https://example.com/a(b) \"t\")")]
    [InlineData("[x](mailto:a@b.example)")]
    [InlineData("[x](/relative/path)")]
    [InlineData("[x](#anchor)")]
    [InlineData("<https://example.com>")]
    [InlineData("[x][r]\n\n[r]: https://example.com \"a\nb\"")]
    [InlineData("**bold** and _em_ and `code` and\n\n- a list\n> quote")]
    public void SafeContent_IsByteForByteUnchanged(string input)
    {
        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void MixedDocument_RemovesOnlyTheUnsafeParts()
    {
        var input = "Intro [ok](https://example.com) and [bad](javascript:x) plus [ref][u].\n\n[u]: javascript:y\n[s]: https://safe.example";

        var result = Run(input);

        Assert.Contains("[ok](https://example.com)", result);
        Assert.Contains("[s]: https://safe.example", result);
        Assert.Contains(" bad ", result);
        Assert.DoesNotContain("javascript:", result);
    }

    // CommonMark: a link label cannot contain a link, so chained brackets expose a new link each time
    // the inner one collapses — the pass runs to a fixed point (review pass 5, agent-smith).
    [Theory]
    [InlineData("[[in](javascript:a)](javascript:b)", "in")]
    [InlineData("[[[z](javascript:e3)](javascript:e2)](javascript:e1)", "z")]
    [InlineData("[[[[w](javascript:e4)](javascript:e3)](javascript:e2)](javascript:e1)", "w")]
    [InlineData("[outer [in](javascript:a) text](javascript:b)", "outer in text")]
    [InlineData("[a *b* c &amp; <b>d</b>](javascript:x)", "a *b* c &amp; <b>d</b>")]
    [InlineData("[see <https://ok.example>](javascript:x)", "see <https://ok.example>")]
    public void NestedAndRichLabels_CollapseUntilNoLinkRemains(string input, string expected)
    {
        var result = Run(input);

        Assert.Equal(expected, result);
        Assert.DoesNotContain("javascript:", result);
    }

    [Fact]
    public void BracketsAroundASafeLink_AreNotALinkAndStayUnchanged()
    {
        // The outer brackets never form a link (a label cannot contain a link), so Markdig resolves
        // only the safe inner link; the trailing `(javascript:b)` is plain text and stays as written.
        var input = "[outer [in](https://ok.example) text](javascript:b)";

        Assert.Equal(input, Run(input));
    }

    [Fact]
    public void ImageInsideUnsafeLink_CollapsesOnce()
    {
        var result = Run("[![alt](https://img.example/a.png)](javascript:x)");

        Assert.DoesNotContain("javascript:", result);
        Assert.Contains("alt", result);
    }

    [Fact]
    public void TooDeeplyNested_IsReturnedUnchangedRatherThanThrowing()
    {
        var input = string.Concat(Enumerable.Repeat("> ", 200)) + "[x](javascript:alert(1))";

        Assert.Equal(input, Run(input)); // sinks re-check the resolved URL; the gate must not throw
    }
}
