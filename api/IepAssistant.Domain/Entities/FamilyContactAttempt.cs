namespace IepAssistant.Domain.Entities;

/// <summary>
/// A logged attempt to reach a student's family outside the app (plan 7, decision 7) — offline
/// participation for school-only students, or simply documenting effort. Appears in the meeting brief
/// (last 90 days) and the student evidence bundle.
/// </summary>
public class FamilyContactAttempt : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }

    public DateTime AttemptedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public FamilyContactOutcome Outcome { get; set; }
    public string? Note { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
}
