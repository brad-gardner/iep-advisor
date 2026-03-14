namespace IepAssistant.Domain.Entities;

public class ParentAdvocacyGoal : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Category { get; set; } // academic, behavioral, services, placement
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
}
