using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The citation/suggestion contract at the tail of a Virtual Advocate answer. Tolerant on shape, strict on
/// provenance: only refs the toolset returned this turn survive, and malformed input never throws.
/// </summary>
public class AdvocateAnswerParserTests
{
    private static readonly HashSet<string> Returned = new(StringComparer.Ordinal) { "kb:12", "kb:40", "child:3", "goal:340" };
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal) { ["kb:12"] = "Prior written notice" };

    // ------------------------------------------------------------------ <sources>

    [Theory]
    [InlineData("<sources>kb:12; kb:40</sources>")]
    [InlineData("<sources>kb:12, kb:40</sources>")]
    [InlineData("<sources>kb:12 kb:40</sources>")]
    [InlineData("<sources>\n kb:12 ;\n kb:40 \n</sources>")]
    [InlineData("<SOURCES>kb:12;kb:40</SOURCES>")]
    public void Parse_AcceptsEverySeparatorAndCase(string block)
    {
        var parsed = AdvocateAnswerParser.Parse("Answer.\n\n" + block, Returned, Labels);

        Assert.Equal(new[] { 12, 40 }, parsed.Citations.Select(c => c.Id));
        Assert.All(parsed.Citations, c => Assert.Equal("kb", c.Kind));
        Assert.Equal("Prior written notice", parsed.Citations[0].Label);
        Assert.Null(parsed.Citations[1].Label);
        Assert.Equal("Answer.", parsed.Markdown);
    }

    [Fact]
    public void Parse_DropsRefsTheToolsetNeverReturned_AndDuplicates_AndGarbage()
    {
        var parsed = AdvocateAnswerParser.Parse("A\n<sources>kb:12; kb:999; iep:7; kb:12; nonsense; kb:abc; child:3</sources>", Returned);

        Assert.Equal(new[] { "kb:12", "child:3" }, parsed.Citations.Select(c => $"{c.Kind}:{c.Id}"));
    }

    [Fact]
    public void Parse_UnclosedSourcesBlockAtEnd_IsStrippedAndStillParsed()
    {
        var parsed = AdvocateAnswerParser.Parse("Answer text.\n<sources>kb:12; kb:40", Returned);

        Assert.Equal(new[] { 12, 40 }, parsed.Citations.Select(c => c.Id));
        Assert.Equal("Answer text.", parsed.Markdown);
    }

    // ------------------------------------------------------------------ <suggest>

    [Fact]
    public void Parse_ExtractsEverySuggestionKind_IncludingSelfClosing()
    {
        const string text = "Here is what I found.\n\n<sources>kb:12</sources>\n" +
                            "<suggest kind=\"prep_question\">What baseline was used?</suggest>\n" +
                            "<suggest kind=\"journal_entry\" date=\"2026-09-12\">Sent home early.</suggest>\n" +
                            "<suggest kind=\"open_kb\" id=\"12\"/>\n" +
                            "<suggest kind=\"open_goal\" id=\"340\"></suggest>";

        var parsed = AdvocateAnswerParser.Parse(text, Returned);

        Assert.Equal("Here is what I found.", parsed.Markdown);
        Assert.Collection(parsed.Suggestions,
            s => { Assert.Equal(AdvocateSuggestionKinds.PrepQuestion, s.Kind); Assert.Equal("What baseline was used?", s.Text); Assert.Null(s.Date); Assert.Null(s.Id); },
            s => { Assert.Equal(AdvocateSuggestionKinds.JournalEntry, s.Kind); Assert.Equal("Sent home early.", s.Text); Assert.Equal("2026-09-12", s.Date); },
            s => { Assert.Equal(AdvocateSuggestionKinds.OpenKnowledgeBase, s.Kind); Assert.Equal(12, s.Id); Assert.Null(s.Text); },
            s => { Assert.Equal(AdvocateSuggestionKinds.OpenGoal, s.Kind); Assert.Equal(340, s.Id); });
    }

    [Fact]
    public void Parse_DropsSuggestionsThatBreakTheContract_ButStillStripsThem()
    {
        var tooLong = new string('x', AdvocateAnswerParser.MaxSuggestionTextLength + 1);
        var text = "A\n" +
                   $"<suggest kind=\"prep_question\">{tooLong}</suggest>\n" +
                   "<suggest kind=\"open_kb\" id=\"999\"/>\n" +           // never returned
                   "<suggest kind=\"open_kb\" id=\"340\"/>\n" +           // returned, but as goal:340 not kb:340
                   "<suggest kind=\"open_goal\"/>\n" +                    // no id
                   "<suggest kind=\"delete_everything\">now</suggest>\n" + // unknown kind
                   "<suggest kind=\"prep_question\">   </suggest>\n" +     // empty
                   "<suggest kind=\"journal_entry\" date=\"yesterday\">Fine.</suggest>"; // bad date ⇒ kept, date null

        var parsed = AdvocateAnswerParser.Parse(text, Returned);

        var only = Assert.Single(parsed.Suggestions);
        Assert.Equal(AdvocateSuggestionKinds.JournalEntry, only.Kind);
        Assert.Null(only.Date);
        Assert.Equal("A", parsed.Markdown);
    }

    [Fact]
    public void Parse_ExactlyMaxLengthSuggestionText_IsKept()
    {
        var atLimit = new string('y', AdvocateAnswerParser.MaxSuggestionTextLength);
        var parsed = AdvocateAnswerParser.Parse($"<suggest kind=\"prep_question\">{atLimit}</suggest>", Returned);
        Assert.Equal(atLimit, Assert.Single(parsed.Suggestions).Text);
    }

    // ------------------------------------------------------------------ stripping / robustness

    [Fact]
    public void Parse_LeavesCleanMarkdown_WhenBlocksAreInterleavedWithTrailingWhitespace()
    {
        const string text = "## Short answer\n\nYes — the goal is **measurable**.\n\n- baseline given\n- target given\n\n" +
                            "<sources>child:3</sources>\n<suggest kind=\"open_goal\" id=\"340\"/>\n\n\n";

        var parsed = AdvocateAnswerParser.Parse(text, Returned);

        Assert.Equal("## Short answer\n\nYes — the goal is **measurable**.\n\n- baseline given\n- target given", parsed.Markdown);
        Assert.DoesNotContain("<sources", parsed.Markdown);
        Assert.DoesNotContain("<suggest", parsed.Markdown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain answer with no tags")]
    [InlineData("<sources>")]
    [InlineData("</sources>")]
    [InlineData("<suggest")]
    [InlineData("<suggest kind=\"prep_question\">never closed")]
    [InlineData("<suggest kind=>broken</suggest>")]
    [InlineData("<sources><suggest kind=\"open_kb\" id=\"\"/></sources>")]
    [InlineData("<suggest kind=\"open_kb\" id=\"99999999999999999999\"/>")]
    public void Parse_NeverThrows_OnMalformedInput(string? text)
    {
        var parsed = AdvocateAnswerParser.Parse(text, Returned);

        Assert.NotNull(parsed.Markdown);
        Assert.Empty(parsed.Citations);
        Assert.Empty(parsed.Suggestions);
    }

    [Fact]
    public void Parse_WithNoReturnedRefs_YieldsNoCitationsAndNoOpenSuggestions()
    {
        var parsed = AdvocateAnswerParser.Parse("A <sources>kb:12</sources><suggest kind=\"open_kb\" id=\"12\"/>", new HashSet<string>());

        Assert.Empty(parsed.Citations);
        Assert.Empty(parsed.Suggestions);
        Assert.Equal("A", parsed.Markdown);
    }
}
