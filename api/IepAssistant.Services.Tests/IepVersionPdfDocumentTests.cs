using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Coverage for <see cref="IepVersionPdfDocument"/> (the legacy, pre-authored-document IEP PDF path).
/// <see cref="IepVersionPdfDocument.ComposeSections"/> flattens each section's <c>RichText</c> markdown
/// via <see cref="MarkdownText.ToPlainText"/> — the same shared <see cref="MarkdownText.Parse"/> entry
/// point <see cref="AuthoredDocumentPdfDocumentTests"/> exercises for the newer authored-document path —
/// so this class proves the reviewer pass2 P1 fix (Markdig's own internal depth-limit guard throwing a
/// bare <see cref="ArgumentException"/> from inside <c>Markdown.Parse</c>, before any of this codebase's
/// own AST-level nesting caps ever run) also protects this older path, which was already reachable before
/// this diff (commit 50803e4) and would otherwise strand a finalized version's PDF in a permanent,
/// retry-proof Error state.
/// </summary>
public sealed class IepVersionPdfDocumentTests
{
    static IepVersionPdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static IepVersion BuildVersionWithSectionRichText(string richText)
    {
        return new IepVersion
        {
            VersionNumber = 1,
            Title = "IEP",
            FinalizedAt = new DateTime(2026, 1, 1),
            Sections = new List<IepVersionSection>
            {
                new() { SectionKind = IepSectionKind.PresentLevels, RichText = richText, DisplayOrder = 0 }
            }
        };
    }

    [Fact]
    public void GeneratePdf_TwoHundredDeepBlockquoteSectionRichText_DoesNotThrow()
    {
        var markdown = new string('>', 200) + " leafword";
        var version = BuildVersionWithSectionRichText(markdown);

        var bytes = new IepVersionPdfDocument(version).GeneratePdf();

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void GeneratePdf_OneHundredFiftyLevelAlternatingListAndQuoteSectionRichText_DoesNotThrow()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 150; i++)
        {
            var indent = new string(' ', i * 2);
            sb.Append(indent).Append(i % 2 == 0 ? "- item" + i : "> quote" + i).Append('\n');
        }
        sb.Append(new string(' ', 150 * 2)).Append("leafword");
        var version = BuildVersionWithSectionRichText(sb.ToString());

        var bytes = new IepVersionPdfDocument(version).GeneratePdf();

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void GeneratePdf_OrdinaryMarkdownSectionRichText_RendersWithoutThrowing()
    {
        var version = BuildVersionWithSectionRichText("Reads at **grade level** with _some_ support.");

        var bytes = new IepVersionPdfDocument(version).GeneratePdf();

        Assert.NotEmpty(bytes);
    }
}
