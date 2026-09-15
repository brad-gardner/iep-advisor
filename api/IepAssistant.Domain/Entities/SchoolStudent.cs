namespace IepAssistant.Domain.Entities;

/// <summary>
/// A student on a school's roster. Plan 3 added a stable district-wide identity
/// (<see cref="DistrictId"/>, <see cref="ExternalStudentId"/>), controlled grade/disability values, a
/// lifecycle (<see cref="Status"/> — <see cref="IsActive"/> is kept equal to <c>Status == Active</c> for
/// existing callers), the lead case manager mirror (<see cref="CaseManagerUserId"/>) and IEP/ETR timeline
/// dates. <see cref="DistrictId"/> is denormalized from the school and maintained by the service on
/// create/transfer so the external-id uniqueness index can be per district.
/// </summary>
public class SchoolStudent : BaseEntity, IAuditableEntity
{
    public int SchoolId { get; set; }
    public int DistrictId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }

    /// <summary>District student ID as text (leading zeros preserved). Unique per district when set.</summary>
    public string? ExternalStudentId { get; set; }

    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }

    /// <summary>Original free-text disability preserved when the migration could not map it (dropped later).</summary>
    public string? LegacyDisabilityText { get; set; }

    public string? HomeLanguage { get; set; } = "en";

    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateTime? ExitedAt { get; set; }
    public ExitReason? ExitReason { get; set; }

    /// <summary>Mirror of the active lead <see cref="StudentTeamMember"/>'s user.</summary>
    public int? CaseManagerUserId { get; set; }

    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }

    /// <summary>
    /// Compatibility mirror of <see cref="Status"/> (not mapped — filter on <c>Status</c> in queries).
    /// Setting <c>false</c> archives; setting <c>true</c> reactivates.
    /// </summary>
    public bool IsActive
    {
        get => Status == StudentStatus.Active;
        set => Status = value ? StudentStatus.Active : (Status == StudentStatus.Active ? StudentStatus.Archived : Status);
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public School School { get; set; } = null!;
    public District District { get; set; } = null!;
    public User? CaseManager { get; set; }
}
