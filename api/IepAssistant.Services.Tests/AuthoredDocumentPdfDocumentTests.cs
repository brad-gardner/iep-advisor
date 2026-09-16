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
}
