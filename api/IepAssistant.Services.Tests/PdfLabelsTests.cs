using System.Globalization;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 7: <see cref="PdfLabels"/> is the one place
/// <c>IepVersionPdfDocument</c>/<c>AuthoredDocumentPdfDocument</c> get every app-generated word they print.
/// These tests prove the two things the plan requires without needing a PDF-text-extraction dependency:
/// resolving English through the real <c>Resources/Pdf.resx</c> reproduces the EXACT pre-phase-7 literals
/// (<see cref="PdfLabels.English"/>'s defaults), and resolving Spanish through <c>Pdf.es.resx</c> yields
/// real, distinct Spanish text for the labels the plan calls out by name.
/// </summary>
public sealed class PdfLabelsTests
{
    [Fact]
    public void From_English_MatchesTheHardcodedDefaults_ByteIdentical()
    {
        var fromResx = PdfLabels.From(TestSupport.TestLocalizers.Pdf());

        // PdfLabels.English's default parameter values are the exact strings every call site used before
        // this phase. If Pdf.resx's English values ever drift from them, this is the test that catches it
        // — the render service always goes through From(), never the bare defaults, so a drift here would
        // silently change the live English PDF output.
        Assert.Equal(PdfLabels.English, fromResx);
    }

    [Theory]
    [InlineData("Goals", "Metas")]
    [InlineData("Baseline", "Línea base")]
    [InlineData("Services", "Servicios")]
    [InlineData("Accommodations", "Adaptaciones")]
    [InlineData("Transition", "Transición")]
    [InlineData("Not addressed", "No abordado")]
    [InlineData("Participants", "Participantes")]
    [InlineData("Signatures", "Firmas")]
    [InlineData("Yes", "Sí")]
    public void From_Spanish_ReturnsRealDistinctSpanishText(string english, string spanish)
    {
        using var _ = CultureScope.For(SupportedLanguages.Spanish);

        var labels = PdfLabels.From(TestSupport.TestLocalizers.Pdf());
        var englishValues = new[]
        {
            labels.Goals, labels.Baseline, labels.Services, labels.Accommodations, labels.Transition,
            labels.NotAddressed, labels.Participants, labels.Signatures, labels.Yes
        };

        Assert.Contains(spanish, englishValues);
        Assert.DoesNotContain(english, englishValues);
    }

    [Fact]
    public void From_Spanish_FormatStringsStillHavePlaceholders()
    {
        using var _ = CultureScope.For(SupportedLanguages.Spanish);
        var labels = PdfLabels.From(TestSupport.TestLocalizers.Pdf());

        // A translator dropping the {0}/{1} token is a silent failure (string.Format throws instead at
        // render time) — assert every format-string label's raw TEMPLATE still carries its placeholder(s).
        Assert.Contains("{0}", labels.Version);
        Assert.Equal("Versión 1", string.Format(CultureInfo.InvariantCulture, labels.Version, 1));
        Assert.Equal("Responsable: Maestro", string.Format(CultureInfo.InvariantCulture, labels.Responsible, "Maestro"));
        Assert.Equal(
            "Fecha del IEP: 2026-01-01   Fecha del ETR: 2026-02-01",
            string.Format(CultureInfo.InvariantCulture, labels.IepEtrDateLine, "2026-01-01", "2026-02-01"));
    }

    [Fact]
    public void BlobPathFor_IepVersion_EnglishIsUnchangedAndSpanishIsDistinct()
    {
        var englishDefault = IIepVersionPdfService.BlobPathFor(5, 2);
        var englishExplicit = IIepVersionPdfService.BlobPathFor(5, 2, "en");
        var englishNull = IIepVersionPdfService.BlobPathFor(5, 2, null);
        var spanish = IIepVersionPdfService.BlobPathFor(5, 2, "es");

        // The pre-phase-7 literal path — this is the backward-compatibility contract: existing English
        // blobs must keep resolving to this exact key without regeneration.
        Assert.Equal("iep-versions/5/iep-v2.pdf", englishDefault);
        Assert.Equal(englishDefault, englishExplicit);
        Assert.Equal(englishDefault, englishNull);

        Assert.NotEqual(englishDefault, spanish);
        Assert.Equal("iep-versions/5/iep-v2.es.pdf", spanish);
    }

    [Fact]
    public void BlobPathFor_AuthoredDocument_EnglishIsUnchangedAndSpanishIsDistinct()
    {
        var englishDefault = IAuthoredDocumentPdfService.BlobPathFor(7, 3);
        var englishExplicit = IAuthoredDocumentPdfService.BlobPathFor(7, 3, "en");
        var spanish = IAuthoredDocumentPdfService.BlobPathFor(7, 3, "es");

        Assert.Equal("authored-docs/7/doc-v3.pdf", englishDefault);
        Assert.Equal(englishDefault, englishExplicit);

        Assert.NotEqual(englishDefault, spanish);
        Assert.Equal("authored-docs/7/doc-v3.es.pdf", spanish);
    }

    [Fact]
    public void BlobPathFor_UnsupportedOrBlankLanguage_FallsBackToEnglish()
    {
        Assert.Equal("iep-versions/1/iep-v1.pdf", IIepVersionPdfService.BlobPathFor(1, 1, "fr"));
        Assert.Equal("iep-versions/1/iep-v1.pdf", IIepVersionPdfService.BlobPathFor(1, 1, "  "));
        Assert.Equal("iep-versions/1/iep-v1.pdf", IIepVersionPdfService.BlobPathFor(1, 1, "EN"));
    }
}
