using System.Globalization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: the one place that decides whether an AI system prompt gets a
/// Spanish response-language instruction. <see cref="SystemLine"/> returns a FIXED string — never
/// interpolated from user, child or document text — so it is safe to append to every non-parser prompt
/// builder's system prompt (<c>SystemPrompt = SomePrompts.X + ResponseLanguage.SystemLine(culture)</c>).
///
/// <para><b>Callers:</b> an in-request call (Advocate, draft question/explanation, meeting summary,
/// student-workspace interview) passes <see cref="CultureInfo.CurrentUICulture"/> directly — ASP.NET
/// Core's <c>RequestLocalization</c> middleware has already set it from the signed-in user's saved
/// preference or <c>Accept-Language</c> by the time a controller action runs. Work that executes OUTSIDE
/// the request that created it (analysis-run synthesis, meeting-prep generation) has no ambient request
/// culture by the time its background worker picks it up — that caller must capture the requester's
/// language onto the artifact at CREATE time (while still inside the request) and re-apply it with
/// <see cref="CultureScope.For"/> around the background execution, so <see cref="CultureInfo.CurrentUICulture"/>
/// is correct again when it calls this method.</para>
///
/// <para><b>Never changed for the document parsers</b> (<c>IepProcessingService</c>,
/// <c>EtrProcessingService</c>) — extraction must reproduce the source document's own text exactly,
/// regardless of the requester's language.</para>
/// </summary>
public static class ResponseLanguage
{
    /// <summary>
    /// Fixed instruction appended to an AI system prompt when responding to a Spanish-preferring
    /// requester. Neutral Latin American Spanish, formal "usted" (matching <c>docs/i18n/glossary-es.md</c>),
    /// keeps the special-education acronyms families see on school paperwork, keeps quoted document text
    /// and names exactly as written (never translate or alter a quotation), and names the glossary as the
    /// source of truth for special-education terminology so vocabulary stays consistent with the rest of
    /// the Spanish site.
    /// </summary>
    private const string SpanishInstruction =
        "\n\nRESPONSE LANGUAGE: Respond in Spanish — neutral Latin American Spanish for a US family, " +
        "formal \"usted\" throughout (never \"tú\"/\"vosotros\"). Keep the special-education acronyms " +
        "IEP, ETR, IDEA, FAPE and LRE exactly as written — do not translate or spell them out differently. " +
        "Keep any quoted document text, names and dates exactly as given to you, in their original " +
        "language — never translate a quotation or a proper name. Use the following terms for these " +
        "concepts, matching the app's Spanish glossary: \"annual goal\" -> \"meta anual\"; " +
        "\"accommodations\" -> \"adaptaciones\"; \"related services\" -> \"servicios relacionados\"; " +
        "\"present levels\" -> \"niveles actuales de desempeño\"; \"due process\" -> \"debido proceso\"; " +
        "\"IEP team\" -> \"equipo del IEP\".";

    /// <summary>
    /// "" for English (and for any culture that is not Spanish) so byte-stable, cacheable prompts are
    /// unaffected when the requester's language is the default; the fixed <see cref="SpanishInstruction"/>
    /// for Spanish. <paramref name="uiCulture"/> is matched by <see cref="CultureInfo.TwoLetterISOLanguageName"/>
    /// through <see cref="SupportedLanguages.Normalize"/> so a region-qualified culture (e.g. "es-MX") and
    /// a bare "es" both resolve the same way; null is treated as English.
    /// </summary>
    public static string SystemLine(CultureInfo? uiCulture)
    {
        var normalized = SupportedLanguages.Normalize(uiCulture?.TwoLetterISOLanguageName);
        return normalized == SupportedLanguages.Spanish ? SpanishInstruction : string.Empty;
    }
}
