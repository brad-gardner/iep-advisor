namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Renders a finalized IepVersion into a PDF and tracks the result on its IepVersionPdf row (P5b).
/// Invoked by the IepVersionPdfWorker off a queue after finalize commits. Idempotent/retryable —
/// a failed render leaves RenderStatus=Error and the version itself remains valid.
/// </summary>
public interface IIepVersionPdfService
{
    /// <summary>
    /// Render the version's PDF, upload it to blob storage, and update the IepVersionPdf row to
    /// Rendered (success) or Error (any failure). Never throws past the worker; safe to re-run.
    /// </summary>
    Task RenderAsync(int versionId, CancellationToken ct = default);

    /// <summary>Deterministic blob path for a version's rendered PDF. Used by render + download.</summary>
    static string BlobPathFor(int versionId, int versionNumber)
        => $"iep-versions/{versionId}/iep-v{versionNumber}.pdf";
}
