namespace IepAssistant.Domain.Entities;

public class IepSection : BaseEntity, IAuditableEntity
{
    public int IepDocumentId { get; set; }
    public string SectionType { get; set; } = string.Empty; // student_profile, present_levels, annual_goals, services, accommodations, placement, transition
    public string? RawText { get; set; }
    public string? ParsedContent { get; set; } // JSON
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDocument IepDocument { get; set; } = null!;
    public ICollection<Goal> Goals { get; set; } = [];
}
