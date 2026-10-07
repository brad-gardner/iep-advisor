namespace IepAssistant.Services;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;Ai&gt;</c>, resolving to <c>Resources/Ai.resx</c> (English) and
/// <c>Resources/Ai.es.resx</c> (Spanish) via the <c>ResourcesPath = "Resources"</c> configured in
/// <c>AddLocalization</c> (Api/Program.cs) — see <see cref="Messages"/> for why this lives in the
/// project's root namespace rather than <c>IepAssistant.Services.Resources</c>.
///
/// Multilingual plan (2026-10-06) phase 3: holds the AI-feature strings — canned user-facing text from
/// the Advocate/draft-explanation/draft-question/meeting-summary/meeting-prep/analysis/progress-report-
/// analysis/student-workspace prompt builders and services (disclaimers, unavailable/usage-cap messages,
/// default titles, tool-activity labels) and the <c>ServiceResult</c> failure messages those services and
/// their controllers return. Deliberately a SEPARATE resx from <c>Messages.resx</c> (owned by a different,
/// concurrently-active phase-3 work item covering the non-AI services) so the two never edit the same
/// file. <see cref="Services.Localization.ResponseLanguage"/> is the one place that decides whether an AI
/// system prompt gets a Spanish response-language instruction; it is unrelated to this resx, which only
/// covers text the SERVER itself writes (never the model's own output).
/// </summary>
public sealed class Ai
{
}
