namespace IepAssistant.Domain.Entities;

public class IepDraftSection : BaseEntity, IAuditableEntity
{
    public int IepDraftId { get; set; }
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }

    // Stable lineage identity assigned once on create; never changed on update.
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDraft IepDraft { get; set; } = null!;
}
