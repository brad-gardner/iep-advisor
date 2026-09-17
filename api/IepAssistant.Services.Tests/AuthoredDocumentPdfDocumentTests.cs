using System.Text.Json;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Golden-structure coverage for <see cref="AuthoredDocumentPdfDocument"/> (plan 7, decision 9): asserts
/// the composed <see cref="AuthoredDocumentPdfDocument.Outline"/> rather than parsing PDF bytes, per the
/// contract. Covers both the generic (state-less) layout — unchanged empty-section-omission behavior —
/// and the OH form layout — numbered sections in template order, goal blocks, "Not addressed" for an
/// entirely-empty section, participants, signatures, and the amendment banner.
/// </summary>
public sealed class AuthoredDocumentPdfDocumentTests
{
    static AuthoredDocumentPdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static string Cfg<T>(T config) => JsonSerializer.Serialize(config, TemplateFieldConfigValidator.JsonOptions);

    [Fact]
    public void GenericLayout_OmitsEmptySections_NoNumbering_NoNotAddressed()
    {
        var narrativeKey = Guid.NewGuid();
        var emptyKey = Guid.NewGuid();

        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = narrativeKey, FieldType = FieldType.Text, Label = "Summary", Required = true, DisplayOrder = 0 }
                    }
                },
                new()
                {
                    Id = 2, Title = "Empty Section", DisplayOrder = 1,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 2, FieldKey = emptyKey, FieldType = FieldType.Text, Label = "Optional", Required = false, DisplayOrder = 0 }
                    }
                }
            }
        };
        var values = $$"""{ "{{narrativeKey}}": "Reads at grade level." }""";

        var doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 1, 1), tree, values);
        doc.GeneratePdf();

        // No state context => generic layout: the empty section is omitted entirely, never marked.
        Assert.Equal(new[] { "Header", "Section: Present Levels" }, doc.Outline);
    }

    [Fact]
    public void OhLayout_NumbersSectionsInOrder_RendersGoalBlocks_MarksEmptySectionNotAddressed_AndAmendmentBanner()
    {
        var textKey = Guid.NewGuid();
        var emptyKey = Guid.NewGuid();
        var goalsKey = Guid.NewGuid();
        var goalTextCol = Guid.NewGuid();
        var baselineCol = Guid.NewGuid();

        var goalsConfig = Cfg(new TableFieldConfig
        {
            Semantic = FieldSemantics.Goals,
            Columns = new List<TableColumn>
            {
                new() { ColumnKey = goalTextCol, Type = FieldType.Text, Label = "Goal", Required = true, Semantic = ColumnSemantics.GoalText },
                new() { ColumnKey = baselineCol, Type = FieldType.Text, Label = "Baseline", Required = false, Semantic = ColumnSemantics.Baseline }
            }
        });

        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 3,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = textKey, FieldType = FieldType.Text, Label = "Summary", Required = true, DisplayOrder = 0 }
                    }
                },
                new()
                {
                    Id = 2, Title = "Special Factors", DisplayOrder = 1,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 2, FieldKey = emptyKey, FieldType = FieldType.Text, Label = "Notes", Required = false, DisplayOrder = 0 }
                    }
                },
                new()
                {
                    Id = 3, Title = "Goals", DisplayOrder = 2,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 3, FieldKey = goalsKey, FieldType = FieldType.Table, Label = "Goals", Required = true, ConfigJson = goalsConfig, DisplayOrder = 0 }
                    }
                }
            }
        };

        var values = $$"""
        {
          "{{textKey}}": "Reads at grade level.",
          "{{goalsKey}}": [
            { "_rowId": "{{Guid.NewGuid()}}", "{{goalTextCol}}": "Improve reading fluency", "{{baselineCol}}": "60 wpm" },
            { "_rowId": "{{Guid.NewGuid()}}", "{{goalTextCol}}": "Improve math computation" }
          ]
        }
        """;

        var header = new AuthoredDocumentPdfHeaderContext(
            StateCode: "OH",
            DocumentTypeKey: "IEP",
            StudentFirstName: "Alex",
            StudentLastName: "Smith",
            StudentDateOfBirth: new DateTime(2010, 5, 1),
            DistrictName: "Test District",
            IepDate: new DateTime(2026, 1, 1),
            EtrDate: null,
            MeetingDate: new DateTime(2026, 1, 10),
            Participants: new List<AuthoredDocumentPdfParticipant> { new("Jamie Lead", "CaseManager", true) },
            AmendsVersionNumber: 2,
            EffectiveDate: new DateTime(2026, 2, 1));

        var doc = new AuthoredDocumentPdfDocument("IEP", 3, new DateTime(2026, 1, 15), tree, values, header);
        doc.GeneratePdf();

        var outline = doc.Outline.ToList();

        Assert.Equal("Header", outline[0]);
        Assert.Contains("Amendment", outline);
        Assert.Contains("Section 1: Present Levels", outline);
        Assert.Contains("Section 2: Special Factors", outline);
        Assert.Contains("Not addressed: Special Factors", outline);
        Assert.Contains("Section 3: Goals", outline);
        Assert.Contains("Goal 1", outline);
        Assert.Contains("Goal 2", outline);
        Assert.Contains("Participants", outline);
        Assert.Contains("Signatures", outline);

        // Template order preserved throughout.
        Assert.True(outline.IndexOf("Section 1: Present Levels") < outline.IndexOf("Section 2: Special Factors"));
        Assert.True(outline.IndexOf("Section 2: Special Factors") < outline.IndexOf("Not addressed: Special Factors"));
        Assert.True(outline.IndexOf("Not addressed: Special Factors") < outline.IndexOf("Section 3: Goals"));
        Assert.True(outline.IndexOf("Section 3: Goals") < outline.IndexOf("Goal 1"));
        Assert.True(outline.IndexOf("Goal 1") < outline.IndexOf("Goal 2"));
        Assert.True(outline.IndexOf("Goal 2") < outline.IndexOf("Participants"));
        Assert.True(outline.IndexOf("Participants") < outline.IndexOf("Signatures"));

        // The unrelated "Present Levels" section (which DID render content) is never marked "Not addressed".
        Assert.DoesNotContain("Not addressed: Present Levels", outline);
    }

    [Fact]
    public void OhLayout_NoAmendment_NoBannerNote()
    {
        var textKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = textKey, FieldType = FieldType.Text, Label = "Summary", Required = true, DisplayOrder = 0 }
                    }
                }
            }
        };
        var values = $$"""{ "{{textKey}}": "Reads at grade level." }""";
        var header = new AuthoredDocumentPdfHeaderContext(
            StateCode: "OH", DocumentTypeKey: "IEP", StudentFirstName: "Alex", StudentLastName: null,
            StudentDateOfBirth: null, DistrictName: null, IepDate: null, EtrDate: null, MeetingDate: null,
            Participants: Array.Empty<AuthoredDocumentPdfParticipant>(), AmendsVersionNumber: null, EffectiveDate: null);

        var doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 1, 1), tree, values, header);
        doc.GeneratePdf();

        Assert.DoesNotContain("Amendment", doc.Outline);
        Assert.Contains("Participants", doc.Outline);
    }

    [Fact]
    public void GenericLayout_RichTextField_RendersMarkdownStructurally_WithoutThrowing()
    {
        // This file asserts the composed Outline rather than PDF bytes (see the class doc) — there is no
        // PDF-text-extraction dependency here to also assert "**"/"- " never appear in the rendered
        // output, or that bold/link formatting was actually applied. That is what
        // MarkdownPdfPlanBuilderTests covers directly (it asserts the pure plan AuthoredDocumentPdfDocument
        // renders from — run flags, resolved hrefs, list/quote depth — without needing QuestPDF at all).
        // This test proves only that composing that plan into QuestPDF elements runs end to end for a
        // realistic mix — bold, a list, a link — without throwing, which is the failure mode a regression
        // in the QuestPDF-rendering half specifically would produce (a null Inline, an unhandled block
        // shape reaching RenderPlanBlocks, etc.).
        var richKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = richKey, FieldType = FieldType.RichText, Label = "Summary", Required = true, DisplayOrder = 0 }
                    }
                }
            }
        };
        var markdown = "Reads at **grade level** with _some_ support.\n\n" +
            "- Strength: phonics\n- Strength: fluency\n\n" +
            "See [progress report](https://example.com/report) for detail.";
        var markdownJson = JsonSerializer.Serialize(markdown);
        var values = $$"""{ "{{richKey}}": {{markdownJson}} }""";

        var doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 1, 1), tree, values);
        var bytes = doc.GeneratePdf();

        Assert.NotEmpty(bytes);
        Assert.Equal(new[] { "Header", "Section: Present Levels" }, doc.Outline);
    }

    [Fact]
    public void GenericLayout_RichTextField_UnsafeSchemeLink_RendersWithoutThrowing_AndNeverEmitsHyperlink()
    {
        // Defense-in-depth end-to-end smoke test (reviewer pass1 P1: link scheme bypass) — the actual
        // "no Href reaches Hyperlink()" assertion is made directly against MarkdownPdfPlanBuilder's output
        // in MarkdownPdfPlanBuilderTests (no PDF-text-extraction dependency needed to prove that). This
        // test only proves the whole pipeline, including the render-time re-check in
        // AuthoredDocumentPdfDocument.EmitMarkdownLink, tolerates an unsafe-scheme link without throwing.
        var markdown = "Contact us: [click here](javascript:alert(document.cookie)) or <javascript:alert(1)>.";
        var bytes = BuildSingleRichTextFieldPdf(markdown, out var doc);

        Assert.NotEmpty(bytes);
        Assert.Equal(new[] { "Header", "Section: Present Levels" }, doc.Outline);
    }

    [Fact]
    public void GenericLayout_RichTextField_PipeTable_RendersWithoutThrowing()
    {
        // Reviewer pass1 P1: a GFM Table is a ContainerBlock, not a LeafBlock, and previously matched no
        // case in the compose switch, so its content silently vanished. MarkdownTextTests /
        // MarkdownPdfPlanBuilderTests separately assert the cell text isn't dropped from the flattened/
        // plan representations; this proves the same input also renders to a PDF without throwing.
        var markdown = "Before.\n\n| Name | Score |\n| --- | --- |\n| Alex | 92 |\n\nAfter.";
        var bytes = BuildSingleRichTextFieldPdf(markdown, out var doc);

        Assert.NotEmpty(bytes);
        Assert.Equal(new[] { "Header", "Section: Present Levels" }, doc.Outline);
    }

    [Fact]
    public void GenericLayout_RichTextField_SixtyDeepBlockquote_RendersWithoutThrowing()
    {
        // Reviewer pass1 P1: 45+ levels of nested blockquote used to throw QuestPDF.Drawing.Exceptions.
        // DocumentLayoutException ("conflicting size constraints") because each level's fixed 12pt of
        // left padding compounded with no cap. MarkdownPdfPlanBuilder now caps the number of wrapped
        // Columns at MaxNestingDepth (8), so cumulative padding is bounded regardless of source depth.
        var markdown = new string('>', 60) + " deep quote text";
        var bytes = BuildSingleRichTextFieldPdf(markdown, out _);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void GenericLayout_RichTextField_ThirtyDeepNestedList_RendersWithoutThrowing_AndIsFast()
    {
        // Reviewer pass1 P1: nested-list rendering was exponential in depth (15 deep = 275ms, 20 deep =
        // 3.8s, 23 deep = 25s) because the composer built one QuestPDF Row-in-Column per nesting level.
        // MarkdownPdfPlanBuilder/AuthoredDocumentPdfDocument now render every list item as a single flat,
        // PaddingLeft-indented Column item regardless of depth, so cost is linear in item count. Measured
        // locally at ~190ms in isolation for this 30-item case (most of which is JIT/QuestPDF warm-up on
        // the first PDF generated in the process, not the list itself) vs. an extrapolated multiple
        // minutes on the old exponential path; the 5s ceiling below leaves generous margin for slower CI
        // hardware while still catching a regression back to exponential behavior.
        var markdown = string.Join("\n", Enumerable.Range(0, 30).Select(i => new string(' ', i * 2) + "- item " + i));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var bytes = BuildSingleRichTextFieldPdf(markdown, out _);
        stopwatch.Stop();

        Assert.NotEmpty(bytes);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected 30-deep nested list to render in well under 5s, took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void GenericLayout_RichTextField_TwoHundredDeepBlockquote_RendersWithoutThrowing()
    {
        // Reviewer pass2 P1: Markdig's own internal depth-limit guard (default 128) throws a bare
        // ArgumentException from inside Markdown.Parse itself -- well before MarkdownPdfPlanBuilder's
        // MaxNestingDepth=8 AST-level cap (which only bounds an already-parsed tree) ever runs. The 60-deep
        // case above stays safely under Markdig's own ceiling, so it never exercised this path.
        // MarkdownText.Parse now catches it and degrades to a single literal-text paragraph instead.
        var markdown = new string('>', 200) + " deep quote text";
        var bytes = BuildSingleRichTextFieldPdf(markdown, out _);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void GenericLayout_RichTextField_OneHundredFiftyLevelAlternatingListAndQuote_RendersWithoutThrowing()
    {
        // Reviewer pass2 P1: alternating list/quote nesting trips Markdig's own depth guard at a much
        // shallower total depth than either pure block type alone -- a shape plausible for a pasted long
        // reply chain that also has bullet points.
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 150; i++)
        {
            var indent = new string(' ', i * 2);
            sb.Append(indent).Append(i % 2 == 0 ? "- item" + i : "> quote" + i).Append('\n');
        }
        sb.Append(new string(' ', 150 * 2)).Append("leafword");

        var bytes = BuildSingleRichTextFieldPdf(sb.ToString(), out _);

        Assert.NotEmpty(bytes);
    }

    private static byte[] BuildSingleRichTextFieldPdf(string markdown, out AuthoredDocumentPdfDocument doc)
    {
        var richKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = richKey, FieldType = FieldType.RichText, Label = "Summary", Required = true, DisplayOrder = 0 }
                    }
                }
            }
        };
        var markdownJson = JsonSerializer.Serialize(markdown);
        var values = $$"""{ "{{richKey}}": {{markdownJson}} }""";

        doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 1, 1), tree, values);
        return doc.GeneratePdf();
    }

    [Fact]
    public void GenericLayout_EmptyRichTextField_OmitsSection()
    {
        // Emptiness for RichText still checks the raw stored string (whitespace-only), independent of
        // markdown parsing — matches the pre-existing empty-field/empty-section rule (G-d.1).
        var richKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            VersionNumber = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Present Levels", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = richKey, FieldType = FieldType.RichText, Label = "Summary", Required = false, DisplayOrder = 0 }
                    }
                }
            }
        };
        var values = $$"""{ "{{richKey}}": "   " }""";

        var doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 1, 1), tree, values);
        doc.GeneratePdf();

        Assert.Equal(new[] { "Header" }, doc.Outline);
    }
}
