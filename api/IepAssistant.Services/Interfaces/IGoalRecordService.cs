using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Goals as first-class records across documents/years, plus provider progress observations
/// (plan 7, decision 6).</summary>
public interface IGoalRecordService
{
    /// <summary>
    /// Projects the Goals table field of a just-finalized version into GoalRecord rows, carrying
    /// forward/retiring the prior version's lineage (see <see cref="GoalRecord"/>). Called from
    /// <c>AuthoredDocumentVersionService.FinalizeAsync</c> inside its finalize transaction — no
    /// authorization check (the caller already authorized the finalize) and no explicit SaveChanges
    /// beyond what is needed to read prior lineage; the caller's own SaveChanges/commit persists
    /// everything together.
    /// </summary>
    Task ProjectOnFinalizeAsync(DocumentInstance instance, AuthoredDocumentVersion version, CancellationToken ct = default);

    /// <summary>Current goals (Status Active|Met|NotMet) for the student, staff Viewer+.</summary>
    Task<ServiceResult<List<GoalRecordModel>>> GetForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Every lineage's full record history for the student, newest-first per lineage, staff Viewer+.</summary>
    Task<ServiceResult<List<GoalLineageModel>>> GetHistoryForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Current goals across every SchoolStudent linked to this child (parent progress view).</summary>
    Task<ServiceResult<List<GoalRecordModel>>> GetForChildAsync(int userId, int childId, CancellationToken ct = default);

    Task<ServiceResult<GoalObservationModel>> AddObservationAsync(int userId, int goalRecordId, CreateGoalObservationModel model, CancellationToken ct = default);

    Task<ServiceResult<GoalRecordModel>> UpdateStatusAsync(int userId, int goalRecordId, UpdateGoalStatusModel model, CancellationToken ct = default);

    /// <summary>Records a retirement reason for a lineage BEFORE the row is removed from the Draft's
    /// ValuesJson; the instance must be Draft/Finalizing. Staff Collaborator+.</summary>
    Task<ServiceResult> RecordRetirementAsync(int userId, int documentInstanceId, CreateGoalRetirementModel model, CancellationToken ct = default);
}
