using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Evaluation case lifecycle: referral → consent → clock → determination → ETR handoff (plan 7, decision 1).</summary>
public interface IEvaluationCaseService
{
    /// <summary>The student's open case, else its most recent closed case, else null. Staff Viewer+.</summary>
    Task<ServiceResult<EvaluationCaseModel>> GetForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> CreateAsync(int userId, int schoolStudentId, CreateEvaluationCaseModel model, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> RequestConsentAsync(int userId, int schoolStudentId, DateTime? requestedAt, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> ReceiveConsentAsync(int userId, int schoolStudentId, ReceiveConsentModel model, CancellationToken ct = default);

    Task<ServiceResult<string>> GetConsentDownloadUrlAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> OverrideDueDateAsync(int userId, int schoolStudentId, OverrideDueDateModel model, CancellationToken ct = default);

    Task<ServiceResult<EvaluatorAssignmentModel>> AddAssignmentAsync(int userId, int schoolStudentId, CreateEvaluatorAssignmentModel model, CancellationToken ct = default);

    Task<ServiceResult<EvaluatorAssignmentModel>> UpdateAssignmentAsync(int userId, int assignmentId, UpdateEvaluatorAssignmentModel model, CancellationToken ct = default);

    Task<ServiceResult> RemoveAssignmentAsync(int userId, int assignmentId, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> DetermineAsync(int userId, int schoolStudentId, DetermineEvaluationModel model, CancellationToken ct = default);

    Task<ServiceResult<EvaluationCaseModel>> CloseAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Creates a Draft IEP DocumentInstance for the student, prefilled from the student's
    /// evidence (which already includes the ETR version's findings). Returns the new instance id.</summary>
    Task<ServiceResult<int>> CreateIepFromEtrAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Sends <see cref="Domain.Entities.NotificationKind.EvaluatorOverdue"/> to each overdue
    /// assignment's evaluator + the case's lead, deduped per assignment per day (via
    /// <see cref="INotificationService"/>'s rolling 24h dedup window). Called by the daily worker.</summary>
    Task RunOverdueNotificationsAsync(CancellationToken ct = default);
}
