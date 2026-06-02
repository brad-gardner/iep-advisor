namespace IepAssistant.Domain.Entities;

public class IepDraftAccommodation : BaseEntity, IAuditableEntity
{
    public int IepDraftId { get; set; }
    public string? Category { get; set; }
    public string? Text { get; set; }
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
