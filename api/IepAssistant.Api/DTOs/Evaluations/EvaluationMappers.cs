using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Evaluations;

internal static class EvaluationMappers
{
    public static EvaluatorAssignmentDto MapAssignment(EvaluatorAssignmentModel m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        DisplayName = m.DisplayName,
        Domain = m.Domain,
        DueDate = m.DueDate,
        SubmittedAt = m.SubmittedAt,
        Notes = m.Notes,
        IsOverdue = m.IsOverdue
    };

    public static EvaluationCaseDto MapCase(EvaluationCaseModel m) => new()
    {
        Id = m.Id,
        StudentId = m.SchoolStudentId,
        Kind = m.Kind,
        Status = m.Status,
        ReferralDate = m.ReferralDate,
        ReferralSource = m.ReferralSource,
        ConsentRequestedAt = m.ConsentRequestedAt,
        ConsentReceivedAt = m.ConsentReceivedAt,
        HasConsentDocument = m.HasConsentDocument,
        ConsentFileName = m.ConsentFileName,
        DeterminationDueDate = m.DeterminationDueDate,
        DueDateOverrideReason = m.DueDateOverrideReason,
        EligibilityOutcome = m.EligibilityOutcome,
        DeterminationDate = m.DeterminationDate,
        DeterminationRationale = m.DeterminationRationale,
        EtrAuthoredVersionId = m.EtrAuthoredVersionId,
        ClosedAt = m.ClosedAt,
        CreatedByName = m.CreatedByName,
        Assignments = m.Assignments.Select(MapAssignment).ToList(),
        Timeline = m.Timeline.Select(t => new EvaluationTimelineEntryDto { At = t.At, Label = t.Label }).ToList(),
        Obligation = m.Obligation == null ? null : ObligationDtoMapper.Map(m.Obligation)
    };
}
