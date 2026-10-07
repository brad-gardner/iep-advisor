using IepAssistant.Services.Localization;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Renders a finalized <see cref="Domain.Entities.AuthoredDocumentVersion"/> into a PDF and tracks the
/// result on its <see cref="Domain.Entities.AuthoredDocumentPdf"/> row (State Document Template Engine,
/// Phase 4). Invoked by the AuthoredDocumentPdfWorker off a queue after finalize commits. The
/// dynamic-template equivalent of <see cref="IIepVersionPdfService"/> — idempotent/retryable: a failed
/// render leaves RenderStatus=Error and the version itself remains valid.
///
/// <para><b>Multilingual (phase 7):</b> one rendered PDF per (version, language) — see
/// <see cref="Domain.Entities.AuthoredDocumentPdf.Language"/>.</para>
/// </summary>
public interface IAuthoredDocumentPdfService
{
    /// <summary>
    /// Render the version's PDF in <paramref name="language"/> ("en"/"es"; null/unsupported defaults to
    /// English — see <see cref="SupportedLanguages.Normalize"/>), upload it to blob storage, and update
    /// the matching AuthoredDocumentPdf row (by version + language) to Rendered (success) or Error (any
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
    /// (<c>authored-docs/{versionId}/doc-v{versionNumber}.pdf</c>) so existing English blobs keep working
    /// without regeneration; any other supported language gets its own path
    /// (<c>authored-docs/{versionId}/doc-v{versionNumber}.{language}.pdf</c>) so the two coexist.
    /// </summary>
    static string BlobPathFor(int versionId, int versionNumber, string? language)
    {
        var normalized = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;
        var suffix = normalized == SupportedLanguages.English ? string.Empty : $".{normalized}";
        return $"authored-docs/{versionId}/doc-v{versionNumber}{suffix}.pdf";
    }
}
