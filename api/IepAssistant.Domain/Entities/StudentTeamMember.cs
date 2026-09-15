namespace IepAssistant.Domain.Entities;

/// <summary>
/// A staff member's functional membership on a student's IEP team (plan 3, decision 4). Permission stays
/// separate: adding a member upserts a <see cref="SchoolStudentAccess"/> row with a role-derived default
/// that an admin can override; removing a member deactivates both rows. Exactly one ACTIVE member may be
/// the lead (<see cref="IsLead"/>, the case manager of record — mirrored to
/// <see cref="SchoolStudent.CaseManagerUserId"/>), enforced by a filtered unique index.
/// </summary>
public class StudentTeamMember : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public int UserId { get; set; }
    public TeamRole TeamRole { get; set; } = TeamRole.Other;
    public bool IsLead { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Optional system note, e.g. why the row was deactivated on transfer.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public User User { get; set; } = null!;
}
