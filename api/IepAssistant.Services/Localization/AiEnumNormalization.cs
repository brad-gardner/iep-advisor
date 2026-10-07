using System.Globalization;
using System.Text;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3 review fix: <see cref="ResponseLanguage"/>'s Spanish
/// instruction tells the model to keep every enumerated/status/severity/rating value in its original
/// English form, but an instruction is not a guarantee — a Spanish-responding model can still slip a
/// Spanish word (or an unexpected English synonym) into one of these fields. Every caller that
/// deserializes a Claude JSON response into one of the enum-like string fields below (severity, rating,
/// adequacy, alignment status, progress rating, evidence quality, red-flag category) runs it through the
/// matching <c>Normalize*</c> method here immediately after parsing — the same defensive pass
/// <c>AnalysisRunService.NormalizeNulls</c> already makes for null lists.
///
/// A recognized English token (any case) or a recognized Spanish variant maps to its canonical English
/// token. A value that matches neither — an unanticipated word, a typo, a language this method does not
/// yet cover — falls back to the MORE severe/conservative option on that field's own scale, never the
/// mildest one: this is an analysis tool surfacing concerns to a parent, and silently downgrading an
/// unrecognized classification to "fine" is the one failure mode that must never happen. (Every list here
/// is intentionally small and enumerable — this is normalization of a closed vocabulary the prompt itself
/// defines, not free-form translation.)
/// </summary>
public static class AiEnumNormalization
{
    /// <summary>Trims, lowercases (invariant), folds spaces to underscores, and strips diacritics (NFKD,
    /// drop combining marks) so "Parcialmente Atendido", "parcialmente atendido" and "parcialmente_atendido"
    /// all fold to the same lookup key — without needing an accented duplicate of every dictionary entry.</summary>
    private static string Fold(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant().Replace(' ', '_');
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Resolve(string? value, IReadOnlyDictionary<string, string> map, string conservativeFallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return conservativeFallback;

        return map.TryGetValue(Fold(value), out var canonical) ? canonical : conservativeFallback;
    }

    // --- red/yellow severity (RedFlag.Severity — AnalysisRunService) — more severe: red ---
    private static readonly Dictionary<string, string> RedYellowSeverityMap = new()
    {
        ["red"] = "red", ["rojo"] = "red", ["roja"] = "red",
        ["yellow"] = "yellow", ["amarillo"] = "yellow", ["amarilla"] = "yellow",
    };

    public static string NormalizeRedYellowSeverity(string? value) => Resolve(value, RedYellowSeverityMap, "red");

    // --- green/yellow/red rating (SmartCriterion.Rating, GoalAnalysisResult.OverallRating —
    // AnalysisRunService) — more severe: red ---
    private static readonly Dictionary<string, string> GreenYellowRedRatingMap = new()
    {
        ["green"] = "green", ["verde"] = "green",
        ["yellow"] = "yellow", ["amarillo"] = "yellow", ["amarilla"] = "yellow",
        ["red"] = "red", ["rojo"] = "red", ["roja"] = "red",
    };

    public static string NormalizeGreenYellowRedRating(string? value) => Resolve(value, GreenYellowRedRatingMap, "red");

    // --- strong/adequate/thin/concerning overall completeness (EtrCompletenessSectionPayload.
    // OverallCompletenessRating — AnalysisRunService) — more severe: concerning ---
    private static readonly Dictionary<string, string> CompletenessRatingMap = new()
    {
        ["strong"] = "strong", ["fuerte"] = "strong", ["solido"] = "strong", ["solida"] = "strong",
        ["adequate"] = "adequate", ["adecuado"] = "adequate", ["adecuada"] = "adequate",
        ["thin"] = "thin", ["escaso"] = "thin", ["escasa"] = "thin", ["debil"] = "thin",
        ["concerning"] = "concerning", ["preocupante"] = "concerning",
    };

    public static string NormalizeCompletenessRating(string? value) => Resolve(value, CompletenessRatingMap, "concerning");

    // --- strong/adequate/thin/missing domain adequacy (EtrEvaluatedDomain.AdequacyRating —
    // AnalysisRunService) — more severe: missing ---
    private static readonly Dictionary<string, string> AdequacyRatingMap = new()
    {
        ["strong"] = "strong", ["fuerte"] = "strong", ["solido"] = "strong", ["solida"] = "strong",
        ["adequate"] = "adequate", ["adecuado"] = "adequate", ["adecuada"] = "adequate",
        ["thin"] = "thin", ["escaso"] = "thin", ["escasa"] = "thin", ["debil"] = "thin",
        ["missing"] = "missing", ["faltante"] = "missing", ["ausente"] = "missing",
    };

    public static string NormalizeAdequacyRating(string? value) => Resolve(value, AdequacyRatingMap, "missing");

    // --- addressed/partially_addressed/not_addressed alignment (GoalAlignmentResult.AlignmentStatus —
    // AnalysisRunService + ProgressReportAnalysisService, both via AdvocacyGapAnalysisResponse) —
    // more severe: not_addressed ---
    private static readonly Dictionary<string, string> AlignmentStatusMap = new()
    {
        ["addressed"] = "addressed", ["atendido"] = "addressed", ["atendida"] = "addressed",
        ["abordado"] = "addressed", ["abordada"] = "addressed",
        ["partially_addressed"] = "partially_addressed",
        ["parcialmente_atendido"] = "partially_addressed", ["parcialmente_atendida"] = "partially_addressed",
        ["parcialmente_abordado"] = "partially_addressed", ["parcialmente_abordada"] = "partially_addressed",
        ["not_addressed"] = "not_addressed",
        ["no_atendido"] = "not_addressed", ["no_atendida"] = "not_addressed",
        ["no_abordado"] = "not_addressed", ["no_abordada"] = "not_addressed",
    };

    public static string NormalizeAlignmentStatus(string? value) => Resolve(value, AlignmentStatusMap, "not_addressed");

    // --- high/medium/low severity (ProgressReportRedFlag.Severity — ProgressReportAnalysisService) —
    // more severe: high ---
    private static readonly Dictionary<string, string> HighMediumLowSeverityMap = new()
    {
        ["high"] = "high", ["alta"] = "high", ["alto"] = "high",
        ["medium"] = "medium", ["media"] = "medium", ["medio"] = "medium",
        ["low"] = "low", ["baja"] = "low", ["bajo"] = "low",
    };

    public static string NormalizeHighMediumLowSeverity(string? value) => Resolve(value, HighMediumLowSeverityMap, "high");

    // --- met/on_track/concerning/regressing/insufficient_data progress rating (GoalProgressFinding.
    // ProgressRating — ProgressReportAnalysisService) — more severe: regressing ---
    private static readonly Dictionary<string, string> ProgressRatingMap = new()
    {
        ["met"] = "met", ["cumplido"] = "met", ["cumplida"] = "met", ["logrado"] = "met", ["lograda"] = "met",
        ["on_track"] = "on_track", ["en_progreso"] = "on_track", ["en_camino"] = "on_track",
        ["encaminado"] = "on_track", ["encaminada"] = "on_track",
        ["concerning"] = "concerning", ["preocupante"] = "concerning",
        ["regressing"] = "regressing", ["retrocediendo"] = "regressing", ["en_retroceso"] = "regressing",
        ["insufficient_data"] = "insufficient_data",
        ["datos_insuficientes"] = "insufficient_data", ["informacion_insuficiente"] = "insufficient_data",
    };

    public static string NormalizeProgressRating(string? value) => Resolve(value, ProgressRatingMap, "regressing");

    // --- strong/adequate/weak evidence quality (GoalProgressFinding.EvidenceQuality —
    // ProgressReportAnalysisService) — more severe: weak ---
    private static readonly Dictionary<string, string> EvidenceQualityMap = new()
    {
        ["strong"] = "strong", ["fuerte"] = "strong", ["solido"] = "strong", ["solida"] = "strong",
        ["adequate"] = "adequate", ["adecuado"] = "adequate", ["adecuada"] = "adequate",
        ["weak"] = "weak", ["debil"] = "weak",
    };

    public static string NormalizeEvidenceQuality(string? value) => Resolve(value, EvidenceQualityMap, "weak");

    // --- red-flag category taxonomy (ProgressReportRedFlag.Category — ProgressReportAnalysisService) —
    // these are not a severity ladder, but an unrecognized value still must not be filed under the
    // mildest/most-dismissible bucket ("other"); "compliance" (a potential IDEA procedural issue) is the
    // conservative choice when the model's own category can't be confidently classified ---
    private static readonly Dictionary<string, string> RedFlagCategoryMap = new()
    {
        ["missing_data"] = "missing_data", ["datos_faltantes"] = "missing_data", ["falta_de_datos"] = "missing_data",
        ["boilerplate"] = "boilerplate", ["texto_generico"] = "boilerplate", ["lenguaje_generico"] = "boilerplate",
        ["regression"] = "regression", ["retroceso"] = "regression", ["regresion"] = "regression",
        ["insufficient_evidence"] = "insufficient_evidence", ["evidencia_insuficiente"] = "insufficient_evidence",
        ["compliance"] = "compliance", ["cumplimiento"] = "compliance", ["procedimental"] = "compliance",
        ["other"] = "other", ["otro"] = "other", ["otra"] = "other",
    };

    public static string NormalizeRedFlagCategory(string? value) => Resolve(value, RedFlagCategoryMap, "compliance");
}
