using IepAssistant.Services.Localization;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Renders a finalized IepVersion into a PDF and tracks the result on its IepVersionPdf row (P5b).
/// Invoked by the IepVersionPdfWorker off a queue after finalize commits. Idempotent/retryable —
/// a failed render leaves RenderStatus=Error and the version itself remains valid.
///
/// <para><b>Multilingual (phase 7):</b> one rendered PDF per (version, language) — see
/// <see cref="Domain.Entities.IepVersionPdf.Language"/>.</para>
/// </summary>
public interface IIepVersionPdfService
{
    /// <summary>
    /// Render the version's PDF in <paramref name="language"/> ("en"/"es"; null/unsupported defaults to
    /// English — see <see cref="SupportedLanguages.Normalize"/>), upload it to blob storage, and update
    /// the matching IepVersionPdf row (by version + language) to Rendered (success) or Error (any
    /// failure). Never throws past the worker; safe to re-run.
    /// </summary>
    Task RenderAsync(int versionId, string? language = null, CancellationToken ct = default);

    /// <summary>English blob path — identical to <see cref="BlobPathFor(int, int, string?)"/> with
    /// <paramref name="versionNumber"/>'s English path, kept as a 2-arg overload so every pre-phase-7 call
    /// site (and every blob written before this phase) keeps resolving to the SAME path.</summary>
    static string BlobPathFor(int versionId, int versionNumber)
        => BlobPathFor(versionId, versionNumber, null);

    /// <summary>
    /// Deterministic blob path for a version's rendered PDF in <paramref name="language"/>. English (null,
    /// unsupported, or "en") keeps the pre-phase-7 path exactly
    /// (<c>iep-versions/{versionId}/iep-v{versionNumber}.pdf</c>) so existing English blobs keep working
    /// without regeneration; any other supported language gets its own path
    /// (<c>iep-versions/{versionId}/iep-v{versionNumber}.{language}.pdf</c>) so the two coexist.
    /// </summary>
    static string BlobPathFor(int versionId, int versionNumber, string? language)
    {
        var normalized = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;
        var suffix = normalized == SupportedLanguages.English ? string.Empty : $".{normalized}";
        return $"iep-versions/{versionId}/iep-v{versionNumber}{suffix}.pdf";
    }
}
