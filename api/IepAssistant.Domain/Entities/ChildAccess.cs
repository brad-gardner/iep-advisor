namespace IepAssistant.Domain.Entities;

public class ChildAccess : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public int? UserId { get; set; }
    public AccessRole Role { get; set; } = AccessRole.Viewer;
    public int? InvitedByUserId { get; set; }
    public string? InviteEmail { get; set; }
    public string? InviteToken { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public User? User { get; set; }
}
