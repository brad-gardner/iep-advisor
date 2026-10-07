using System.Text.Json;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3 review fix: <see cref="AiEnumNormalization"/> is the one place
/// that maps an AI-returned enum-like string field (severity, rating, adequacy, alignment status,
/// progress rating, evidence quality, red-flag category) to its canonical English token, tolerating a
/// Spanish variant that slips through despite <see cref="ResponseLanguage"/>'s instruction to keep these
/// fields in English. Covers each scale's English passthrough, Spanish variant, and unknown-value
/// fallback to the MORE severe/conservative option — then proves the whole pipeline end to end by
/// deserializing a Spanish-style Claude JSON response (via <see cref="AnalysisRunService"/>'s and
/// <see cref="ProgressReportAnalysisService"/>'s own internal normalization entry points) and asserting
/// every field lands on the right canonical enum.
/// </summary>
public class AiEnumNormalizationTests
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    // --- red/yellow severity ---

    [Theory]
    [InlineData("red", "red")]
    [InlineData("RED", "red")]
    [InlineData("rojo", "red")]
    [InlineData("roja", "red")]
    [InlineData("yellow", "yellow")]
    [InlineData("amarillo", "yellow")]
    [InlineData("amarilla", "yellow")]
    public void NormalizeRedYellowSeverity_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeRedYellowSeverity(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("naranja")]
    public void NormalizeRedYellowSeverity_UnknownOrMissing_FallsBackToMoreSevere(string? input) =>
        Assert.Equal("red", AiEnumNormalization.NormalizeRedYellowSeverity(input));

    // --- green/yellow/red rating ---

    [Theory]
    [InlineData("green", "green")]
    [InlineData("verde", "green")]
    [InlineData("yellow", "yellow")]
    [InlineData("amarillo", "yellow")]
    [InlineData("red", "red")]
    [InlineData("rojo", "red")]
    public void NormalizeGreenYellowRedRating_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeGreenYellowRedRating(input));

    [Fact]
    public void NormalizeGreenYellowRedRating_Unknown_FallsBackToRed() =>
        Assert.Equal("red", AiEnumNormalization.NormalizeGreenYellowRedRating("azul"));

    // --- completeness / adequacy ---

    [Theory]
    [InlineData("strong", "strong")]
    [InlineData("fuerte", "strong")]
    [InlineData("adequate", "adequate")]
    [InlineData("adecuado", "adequate")]
    [InlineData("thin", "thin")]
    [InlineData("escaso", "thin")]
    [InlineData("concerning", "concerning")]
    [InlineData("preocupante", "concerning")]
    public void NormalizeCompletenessRating_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeCompletenessRating(input));

    [Fact]
    public void NormalizeCompletenessRating_Unknown_FallsBackToConcerning() =>
        Assert.Equal("concerning", AiEnumNormalization.NormalizeCompletenessRating("regular"));

    [Theory]
    [InlineData("strong", "strong")]
    [InlineData("fuerte", "strong")]
    [InlineData("missing", "missing")]
    [InlineData("faltante", "missing")]
    [InlineData("ausente", "missing")]
    public void NormalizeAdequacyRating_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeAdequacyRating(input));

    [Fact]
    public void NormalizeAdequacyRating_Unknown_FallsBackToMissing() =>
        Assert.Equal("missing", AiEnumNormalization.NormalizeAdequacyRating("mediocre"));

    // --- alignment status ---

    [Theory]
    [InlineData("addressed", "addressed")]
    [InlineData("atendido", "addressed")]
    [InlineData("abordada", "addressed")]
    [InlineData("partially_addressed", "partially_addressed")]
    [InlineData("parcialmente_atendido", "partially_addressed")]
    [InlineData("parcialmente atendido", "partially_addressed")]
    [InlineData("not_addressed", "not_addressed")]
    [InlineData("no_atendido", "not_addressed")]
    public void NormalizeAlignmentStatus_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeAlignmentStatus(input));

    [Fact]
    public void NormalizeAlignmentStatus_Unknown_FallsBackToNotAddressed() =>
        Assert.Equal("not_addressed", AiEnumNormalization.NormalizeAlignmentStatus("desconocido"));

    // --- high/medium/low severity ---

    [Theory]
    [InlineData("high", "high")]
    [InlineData("alta", "high")]
    [InlineData("alto", "high")]
    [InlineData("medium", "medium")]
    [InlineData("media", "medium")]
    [InlineData("low", "low")]
    [InlineData("baja", "low")]
    public void NormalizeHighMediumLowSeverity_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeHighMediumLowSeverity(input));

    [Fact]
    public void NormalizeHighMediumLowSeverity_Unknown_FallsBackToHigh() =>
        Assert.Equal("high", AiEnumNormalization.NormalizeHighMediumLowSeverity("extrema"));

    // --- progress rating ---

    [Theory]
    [InlineData("met", "met")]
    [InlineData("cumplido", "met")]
    [InlineData("alcanzado", "met")]
    [InlineData("alcanzada", "met")]
    [InlineData("logrado", "met")]
    [InlineData("on_track", "on_track")]
    [InlineData("encaminado", "on_track")]
    [InlineData("progresando", "on_track")]
    [InlineData("en_curso", "on_track")]
    [InlineData("en_progreso", "on_track")]
    [InlineData("concerning", "concerning")]
    [InlineData("no_cumplido", "concerning")]
    [InlineData("no_cumplida", "concerning")]
    [InlineData("estancado", "concerning")]
    [InlineData("estancada", "concerning")]
    [InlineData("regressing", "regressing")]
    [InlineData("retrocediendo", "regressing")]
    [InlineData("retroceso", "regressing")]
    [InlineData("insufficient_data", "insufficient_data")]
    [InlineData("datos_insuficientes", "insufficient_data")]
    [InlineData("insuficiente", "insufficient_data")]
    public void NormalizeProgressRating_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeProgressRating(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("desconocido")]
    public void NormalizeProgressRating_UnknownOrMissing_FallsBackToConcerning(string? input) =>
        Assert.Equal("concerning", AiEnumNormalization.NormalizeProgressRating(input));

    // --- evidence quality ---

    [Theory]
    [InlineData("strong", "strong")]
    [InlineData("fuerte", "strong")]
    [InlineData("weak", "weak")]
    [InlineData("debil", "weak")]
    [InlineData("débil", "weak")]
    public void NormalizeEvidenceQuality_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeEvidenceQuality(input));

    [Fact]
    public void NormalizeEvidenceQuality_Unknown_FallsBackToWeak() =>
        Assert.Equal("weak", AiEnumNormalization.NormalizeEvidenceQuality("regular"));

    // --- red-flag category ---

    [Theory]
    [InlineData("compliance", "compliance")]
    [InlineData("cumplimiento", "compliance")]
    [InlineData("boilerplate", "boilerplate")]
    [InlineData("texto_generico", "boilerplate")]
    [InlineData("other", "other")]
    [InlineData("otro", "other")]
    [InlineData("otros", "other")]
    [InlineData("otras", "other")]
    [InlineData("datos_insuficientes", "missing_data")]
    public void NormalizeRedFlagCategory_KnownValues_MapToCanonicalEnglish(string input, string expected) =>
        Assert.Equal(expected, AiEnumNormalization.NormalizeRedFlagCategory(input));

    [Fact]
    public void NormalizeRedFlagCategory_Unknown_FallsBackToCompliance() =>
        Assert.Equal("compliance", AiEnumNormalization.NormalizeRedFlagCategory("misceláneo"));

    // --- end-to-end: a Spanish-style Claude response parses to the right enums ---

    [Fact]
    public void AnalysisRunService_SourceAnalysisResponse_SpanishStyleValues_NormalizeToCanonicalEnglish()
    {
        const string json = """
        {
          "overallSummary": "Resumen en español.",
          "sections": [
            {
              "sectionKind": "present_levels",
              "plainLanguageSummary": "Explicación en español.",
              "keyPoints": [],
              "redFlags": [
                { "severity": "rojo", "title": "Preocupación", "description": "Detalle." }
              ],
              "legalReferences": []
            }
          ],
          "goalAnalyses": [
            {
              "goalId": 1,
              "goalText": "Meta en español",
              "smartAnalysis": {
                "specific": { "rating": "verde", "explanation": "..." },
                "measurable": { "rating": "amarillo", "explanation": "..." },
                "achievable": { "rating": "rojo", "explanation": "..." },
                "relevant": { "rating": "verde", "explanation": "..." },
                "timeBound": { "rating": "verde", "explanation": "..." }
              },
              "overallRating": "amarillo",
              "plainLanguageSummary": "..."
            }
          ],
          "etrCompleteness": {
            "evaluatedDomains": [
              { "domain": "Cognitivo", "toolsUsed": [], "adequacyRating": "escaso" }
            ],
            "missingDomains": [],
            "overallCompletenessRating": "preocupante"
          },
          "overallRedFlags": [
            { "severity": "amarillo", "title": "General", "description": "Detalle." }
          ],
          "advocacyGapAnalysis": {
            "summary": "...",
            "goalAlignments": [
              { "parentGoalText": "Meta", "alignmentStatus": "parcialmente_atendido", "explanation": "..." }
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<SourceAnalysisResponse>(json, CaseInsensitive);
        Assert.NotNull(response);

        AnalysisRunService.NormalizeNulls(response!);

        Assert.Equal("red", response!.Sections[0].RedFlags[0].Severity);
        Assert.Equal("yellow", response.OverallRedFlags[0].Severity);

        var goal = response.GoalAnalyses[0];
        Assert.Equal("green", goal.SmartAnalysis.Specific.Rating);
        Assert.Equal("yellow", goal.SmartAnalysis.Measurable.Rating);
        Assert.Equal("red", goal.SmartAnalysis.Achievable.Rating);
        Assert.Equal("green", goal.SmartAnalysis.Relevant.Rating);
        Assert.Equal("green", goal.SmartAnalysis.TimeBound.Rating);
        Assert.Equal("yellow", goal.OverallRating);

        Assert.Equal("thin", response.EtrCompleteness!.EvaluatedDomains[0].AdequacyRating);
        Assert.Equal("concerning", response.EtrCompleteness.OverallCompletenessRating);

        Assert.Equal("partially_addressed", response.AdvocacyGapAnalysis!.GoalAlignments[0].AlignmentStatus);
    }

    [Fact]
    public void ProgressReportAnalysisService_Response_SpanishStyleValues_NormalizeToCanonicalEnglish()
    {
        const string json = """
        {
          "summary": "Resumen en español.",
          "goalProgressFindings": [
            {
              "iepGoalText": "Meta",
              "reportedProgress": "...",
              "progressRating": "retrocediendo",
              "evidenceQuality": "debil"
            }
          ],
          "redFlags": [
            { "severity": "alta", "category": "cumplimiento", "finding": "...", "whyItMatters": "..." }
          ],
          "advocacyGapAnalysis": {
            "summary": "...",
            "goalAlignments": [
              { "parentGoalText": "Meta", "alignmentStatus": "no_atendido", "explanation": "..." }
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<ProgressReportAnalysisResponse>(json, CaseInsensitive);
        Assert.NotNull(response);

        ProgressReportAnalysisService.NormalizeEnums(response!);

        Assert.Equal("regressing", response!.GoalProgressFindings[0].ProgressRating);
        Assert.Equal("weak", response.GoalProgressFindings[0].EvidenceQuality);
        Assert.Equal("high", response.RedFlags[0].Severity);
        Assert.Equal("compliance", response.RedFlags[0].Category);
        Assert.Equal("not_addressed", response.AdvocacyGapAnalysis!.GoalAlignments[0].AlignmentStatus);
    }

    // --- null robustness: an explicit JSON `null` for an object field or inside an array must not throw ---

    [Fact]
    public void AnalysisRunService_SourceAnalysisResponse_NullSmartAnalysisAndNullArrayElements_DoesNotThrow()
    {
        // todos/249: in addition to a null element NESTED inside redFlags/overallRedFlags/goalAlignments
        // (already covered below), this also puts a null element directly in the TOP-LEVEL sections,
        // goalAnalyses and etrCompleteness.evaluatedDomains arrays — the gap a bare `??= []` doesn't
        // close, since it only substitutes [] for a null LIST, never strips a null ELEMENT within one.
        const string json = """
        {
          "overallSummary": "Summary.",
          "sections": [
            null,
            {
              "sectionKind": "present_levels",
              "plainLanguageSummary": "Explanation.",
              "keyPoints": [],
              "redFlags": [
                null,
                { "severity": "red", "title": "Concern", "description": "Detail." }
              ],
              "legalReferences": []
            }
          ],
          "goalAnalyses": [
            null,
            {
              "goalId": 1,
              "goalText": "Goal text",
              "smartAnalysis": null,
              "overallRating": "green",
              "plainLanguageSummary": "..."
            }
          ],
          "overallRedFlags": [
            null,
            { "severity": "yellow", "title": "General", "description": "Detail." }
          ],
          "etrCompleteness": {
            "evaluatedDomains": [
              null,
              { "domain": "Psychological", "toolsUsed": [], "adequacyRating": "thin" }
            ],
            "missingDomains": [],
            "overallCompletenessRating": "concerning"
          },
          "advocacyGapAnalysis": {
            "summary": "...",
            "goalAlignments": [
              null,
              { "parentGoalText": "Goal", "alignmentStatus": "addressed", "explanation": "..." }
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<SourceAnalysisResponse>(json, CaseInsensitive);
        Assert.NotNull(response);

        var ex = Record.Exception(() => AnalysisRunService.NormalizeNulls(response!));
        Assert.Null(ex);

        Assert.Single(response!.Sections);
        Assert.Single(response.Sections[0].RedFlags);
        Assert.Equal("red", response.Sections[0].RedFlags[0].Severity);

        Assert.Single(response.OverallRedFlags);
        Assert.Equal("yellow", response.OverallRedFlags[0].Severity);

        Assert.Single(response.GoalAnalyses);
        var goal = response.GoalAnalyses[0];
        Assert.NotNull(goal.SmartAnalysis);
        Assert.NotNull(goal.SmartAnalysis.Specific);
        Assert.NotNull(goal.SmartAnalysis.Measurable);
        Assert.NotNull(goal.SmartAnalysis.Achievable);
        Assert.NotNull(goal.SmartAnalysis.Relevant);
        Assert.NotNull(goal.SmartAnalysis.TimeBound);

        Assert.Single(response.EtrCompleteness!.EvaluatedDomains);
        Assert.Equal("thin", response.EtrCompleteness.EvaluatedDomains[0].AdequacyRating);

        Assert.Single(response.AdvocacyGapAnalysis!.GoalAlignments);
        Assert.Equal("addressed", response.AdvocacyGapAnalysis.GoalAlignments[0].AlignmentStatus);
    }

    [Fact]
    public void ProgressReportAnalysisService_Response_NullArrayElements_DoesNotThrow()
    {
        const string json = """
        {
          "summary": "Summary.",
          "goalProgressFindings": [
            null,
            { "iepGoalText": "Goal", "reportedProgress": "...", "progressRating": "met", "evidenceQuality": "strong" }
          ],
          "redFlags": [
            null,
            { "severity": "high", "category": "compliance", "finding": "...", "whyItMatters": "..." }
          ],
          "advocacyGapAnalysis": {
            "summary": "...",
            "goalAlignments": [
              null,
              { "parentGoalText": "Goal", "alignmentStatus": "not_addressed", "explanation": "..." }
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<ProgressReportAnalysisResponse>(json, CaseInsensitive);
        Assert.NotNull(response);

        var ex = Record.Exception(() => ProgressReportAnalysisService.NormalizeEnums(response!));
        Assert.Null(ex);

        Assert.Single(response!.GoalProgressFindings);
        Assert.Equal("met", response.GoalProgressFindings[0].ProgressRating);

        Assert.Single(response.RedFlags);
        Assert.Equal("high", response.RedFlags[0].Severity);

        Assert.Single(response.AdvocacyGapAnalysis!.GoalAlignments);
        Assert.Equal("not_addressed", response.AdvocacyGapAnalysis.GoalAlignments[0].AlignmentStatus);
    }
}
