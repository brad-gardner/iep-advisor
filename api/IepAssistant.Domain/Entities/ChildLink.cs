namespace IepAssistant.Domain.Entities;

public class ChildLink : BaseEntity, IAuditableEntity
{
    public int? ChildProfileId { get; set; }
    public int SchoolStudentId { get; set; }
    public DateTime? LinkedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Parent-invite flow (logic implemented in P3c).
    public int? InvitedByUserId { get; set; }
    public string? InviteEmail { get; set; }
    public string? InviteToken { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile? ChildProfile { get; set; }
    public SchoolStudent SchoolStudent { get; set; } = null!;
}
