namespace IepAssistant.Domain.Entities;

public class TeacherProfile : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public string? Title { get; set; }
    public string? Credentials { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public User User { get; set; } = null!;
    public School School { get; set; } = null!;
}
