namespace IepAssistant.Domain.Entities;

/// <summary>
/// An evaluation (initial or re-evaluation) case tracking referral → consent → clock → determination
/// (plan 7, decision 1). At most one case with <see cref="Status"/> not
/// <see cref="EvaluationCaseStatus.Closed"/> may exist per student — enforced by the service and backed
/// by a filtered unique index (<c>IX_EvaluationCases_OneOpenPerStudent</c>).
/// </summary>
public class EvaluationCase : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public EvaluationCaseKind Kind { get; set; }
    public DateTime ReferralDate { get; set; }
    public string? ReferralSource { get; set; }

    public DateTime? ConsentRequestedAt { get; set; }
    public DateTime? ConsentReceivedAt { get; set; }
    public string? ConsentBlobPath { get; set; }
    public string? ConsentFileName { get; set; }

    /// <summary>Computed as <see cref="ConsentReceivedAt"/> + 60 calendar days when consent is received
    /// (see <c>EvaluationCaseRules.EvaluationClockDays</c>); editable afterward via
    /// <c>PUT .../due-date</c>, which requires <see cref="DueDateOverrideReason"/>.</summary>
    public DateTime? DeterminationDueDate { get; set; }
    public string? DueDateOverrideReason { get; set; }

    public EvaluationCaseStatus Status { get; set; } = EvaluationCaseStatus.Open;
    public EligibilityOutcome? EligibilityOutcome { get; set; }
    public DateTime? DeterminationDate { get; set; }
    public string? DeterminationRationale { get; set; }

    /// <summary>Set by "Create IEP from ETR" once the resulting Draft instance/version exist (provenance only, unenforced FK).</summary>
    public int? EtrDocumentInstanceId { get; set; }
    public int? EtrAuthoredVersionId { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public ICollection<EvaluatorAssignment> Assignments { get; set; } = new List<EvaluatorAssignment>();
}
