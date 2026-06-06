namespace IepAssistant.Domain.Entities;

public class IepDraftTransitionItem : BaseEntity, IAuditableEntity
{
    public int IepDraftId { get; set; }
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
    public int DisplayOrder { get; set; }

    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDraft IepDraft { get; set; } = null!;
}
