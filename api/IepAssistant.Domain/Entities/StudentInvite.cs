namespace IepAssistant.Domain.Entities;

/// <summary>
/// P7a. An invitation for a person to activate a Student account, mirroring the <c>ChildLink</c>/<c>ShareService</c>
/// SHA-token pattern: a 32-byte raw token is emailed, only its SHA256 hash is stored in <see cref="InviteToken"/>,
/// the invite is email-bound (case-insensitive on <see cref="InviteEmail"/>), single-use (token cleared on accept),
/// and expires after 14 days. Exactly one of <see cref="ChildProfileId"/> (parent-initiated) or
/// <see cref="SchoolStudentId"/> (educator-initiated) is set.
/// </summary>
public class StudentInvite : BaseEntity, IAuditableEntity
{
    public string InviteEmail { get; set; } = string.Empty;
    public string? InviteToken { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int InvitedByUserId { get; set; }

    // Parent-initiated invite links the student to a ChildProfile.
    public int? ChildProfileId { get; set; }

    // Educator-initiated invite links the student to a SchoolStudent.
    public int? SchoolStudentId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile? ChildProfile { get; set; }
    public SchoolStudent? SchoolStudent { get; set; }
}
