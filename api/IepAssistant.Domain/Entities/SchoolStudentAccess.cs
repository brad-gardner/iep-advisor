namespace IepAssistant.Domain.Entities;

public class SchoolStudentAccess : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public int UserId { get; set; }
    public AccessRole Role { get; set; } = AccessRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public User User { get; set; } = null!;
}
