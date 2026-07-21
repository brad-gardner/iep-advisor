namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Renders a finalized <see cref="Domain.Entities.AuthoredDocumentVersion"/> into a PDF and tracks the
/// result on its <see cref="Domain.Entities.AuthoredDocumentPdf"/> row (State Document Template Engine,
/// Phase 4). Invoked by the AuthoredDocumentPdfWorker off a queue after finalize commits. The
/// dynamic-template equivalent of <see cref="IIepVersionPdfService"/> — idempotent/retryable: a failed
/// render leaves RenderStatus=Error and the version itself remains valid.
/// </summary>
public interface IAuthoredDocumentPdfService
{
    /// <summary>
    /// Render the version's PDF, upload it to blob storage, and update the AuthoredDocumentPdf row to
    /// Rendered (success) or Error (any failure). Never throws past the worker; safe to re-run.
    /// </summary>
    Task RenderAsync(int versionId, CancellationToken ct = default);

    /// <summary>Deterministic blob path for a version's rendered PDF. Used by render + download.</summary>
    static string BlobPathFor(int versionId, int versionNumber)
        => $"authored-docs/{versionId}/doc-v{versionNumber}.pdf";
}
