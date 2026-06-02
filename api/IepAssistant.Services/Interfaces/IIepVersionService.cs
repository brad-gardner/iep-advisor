using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Finalize + read of immutable IepVersion snapshots (P5a). Finalize deep-copies a draft into an
/// immutable version inside a serializable transaction (the draft is frozen via Status=Finalizing
/// while the copy runs). Reads are SchoolId-bound for educators and ChildLink-bound for parents.
/// </summary>
public interface IIepVersionService
{
    /// <summary>Finalize a draft into a new immutable IepVersion (Collaborator+ on the student).</summary>
    Task<ServiceResult<IepVersionSummaryModel>> FinalizeAsync(int userId, int draftId, DateTime? effectiveDate, CancellationToken ct = default);

    /// <summary>Educator list of a student's versions (SchoolId-bound), newest VersionNumber first.</summary>
    Task<ServiceResult<List<IepVersionSummaryModel>>> ListForStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>Parent list of a child's finalized versions (active ChildLink + AccessService), newest first.</summary>
    Task<ServiceResult<List<IepVersionSummaryModel>>> ListForChildAsync(int userId, int childId, CancellationToken ct = default);

    /// <summary>Full version (children + PDF status). Educator-with-access OR linked-parent-with-access.</summary>
    Task<ServiceResult<IepVersionModel>> GetVersionAsync(int userId, int versionId, CancellationToken ct = default);
}
