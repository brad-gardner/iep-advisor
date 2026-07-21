using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Finalize + read of immutable <see cref="Domain.Entities.AuthoredDocumentVersion"/> snapshots for the
/// State Document Template Engine (Phase 4). The dynamic-template equivalent of
/// <see cref="IIepVersionService"/>. Finalize validates a document instance against its pinned template
/// schema and deep-copies it into an immutable version inside a serializable transaction (the instance
/// is briefly frozen via Status=Finalizing while the copy runs). Reads are org-bound for educators and
/// ChildLink-bound for linked parents.
/// </summary>
public interface IAuthoredDocumentVersionService
{
    /// <summary>
    /// Finalize a Draft <see cref="Domain.Entities.DocumentInstance"/> into a new immutable
    /// <see cref="Domain.Entities.AuthoredDocumentVersion"/> (Collaborator+ on the student). Validates the
    /// value-document against the pinned schema; on failure the result carries a COMPLETE list of
    /// friendly errors (in <c>ServiceResult.Errors</c>) each identifying section + field label (+ row
    /// index for table cells) and no version is created. On success a Pending
    /// <see cref="Domain.Entities.AuthoredDocumentPdf"/> is created and the version id is returned so the
    /// controller can enqueue the render.
    /// </summary>
    Task<ServiceResult<AuthoredDocumentVersionSummaryModel>> FinalizeAsync(int instanceId, int actingUserId, CancellationToken ct = default);

    /// <summary>Educator list of a student's finalized versions (org-bound), newest VersionNumber first.</summary>
    Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListVersionsForStudentAsync(int studentId, int actingUserId, CancellationToken ct = default);

    /// <summary>Parent list of a child's finalized versions (active ChildLink + AccessService), newest first.</summary>
    Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListForChildAsync(int childId, int actingUserId, CancellationToken ct = default);

    /// <summary>Full version (frozen values + pinned template tree + PDF status). Educator-with-access OR linked-parent-with-access.</summary>
    Task<ServiceResult<AuthoredDocumentVersionDetailModel>> GetVersionAsync(int versionId, int actingUserId, CancellationToken ct = default);

    /// <summary>PDF status + (when Rendered) a short-lived download URL. Same authorization as <see cref="GetVersionAsync"/>.</summary>
    Task<ServiceResult<AuthoredDocumentPdfStatusModel>> GetPdfStatusAsync(int versionId, int actingUserId, CancellationToken ct = default);

    /// <summary>
    /// Re-queue a version's PDF render (educator, Collaborator+). Allowed when the current RenderStatus
    /// is Error or Pending; sets it back to Pending and returns the version id so the controller can
    /// enqueue. The version itself is never modified.
    /// </summary>
    Task<ServiceResult<int>> RequestPdfRetryAsync(int versionId, int actingUserId, CancellationToken ct = default);
}
