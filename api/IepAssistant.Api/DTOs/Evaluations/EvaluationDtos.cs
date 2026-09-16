using System.ComponentModel.DataAnnotations;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Evaluations;

public class EvaluatorAssignmentDto
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

public class EvaluationTimelineEntryDto
{
    public DateTime At { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class EvaluationCaseDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
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
    public List<EvaluatorAssignmentDto> Assignments { get; set; } = new();
    public List<EvaluationTimelineEntryDto> Timeline { get; set; } = new();
    public ObligationDto? Obligation { get; set; }
}

public class CreateEvaluationCaseRequest
{
    [Required]
    public EvaluationCaseKind Kind { get; set; }

    [Required]
    public DateTime ReferralDate { get; set; }

    [MaxLength(200)]
    public string? ReferralSource { get; set; }
}

public class RequestConsentRequest
{
    public DateTime? RequestedAt { get; set; }
}

/// <summary>JSON body for <c>POST .../consent/receive</c> when no file is attached (multipart form fields
/// are read directly from the request instead — see <c>EvaluationCaseController.ReceiveConsent</c>).</summary>
public class ReceiveConsentJsonRequest
{
    [Required]
    public DateTime ReceivedAt { get; set; }
}

public class OverrideDueDateRequest
{
    [Required]
    public DateTime DeterminationDueDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class CreateEvaluatorAssignmentRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Domain { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }
}

public class UpdateEvaluatorAssignmentRequest
{
    public DateTime? SubmittedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime? DueDate { get; set; }
}

public class DetermineEvaluationRequest
{
    [Required]
    public EligibilityOutcome Outcome { get; set; }

    [Required]
    public DateTime DeterminationDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Rationale { get; set; } = string.Empty;

    public int? EtrAuthoredVersionId { get; set; }
}

public class CreateIepResponseDto
{
    public int InstanceId { get; set; }
}
