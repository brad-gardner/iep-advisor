using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class EvaluatorAssignmentModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsOverdue { get; set; }
}

public class EvaluationTimelineEntryModel
{
    public DateTime At { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class EvaluationCaseModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public EvaluationCaseKind Kind { get; set; }
    public EvaluationCaseStatus Status { get; set; }
    public DateTime ReferralDate { get; set; }
    public string? ReferralSource { get; set; }
    public DateTime? ConsentRequestedAt { get; set; }
    public DateTime? ConsentReceivedAt { get; set; }
    public bool HasConsentDocument { get; set; }
    public string? ConsentFileName { get; set; }
    public DateTime? DeterminationDueDate { get; set; }
    public string? DueDateOverrideReason { get; set; }
    public EligibilityOutcome? EligibilityOutcome { get; set; }
    public DateTime? DeterminationDate { get; set; }
    public string? DeterminationRationale { get; set; }
    public int? EtrAuthoredVersionId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public List<EvaluatorAssignmentModel> Assignments { get; set; } = new();
    public List<EvaluationTimelineEntryModel> Timeline { get; set; } = new();
    public ObligationModel? Obligation { get; set; }
}

public class CreateEvaluationCaseModel
{
    public EvaluationCaseKind Kind { get; set; }
    public DateTime ReferralDate { get; set; }
    public string? ReferralSource { get; set; }
}

public class ReceiveConsentModel
{
    public DateTime ReceivedAt { get; set; }
    public Stream? FileStream { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
}

public class OverrideDueDateModel
{
    public DateTime DeterminationDueDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CreateEvaluatorAssignmentModel
{
    public int UserId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

public class UpdateEvaluatorAssignmentModel
{
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
}

public class DetermineEvaluationModel
{
    public EligibilityOutcome Outcome { get; set; }
    public DateTime DeterminationDate { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public int? EtrAuthoredVersionId { get; set; }
}
