namespace IepAssistant.Domain.Entities;

// Named IepDraftServiceLine (not IepDraftService) to avoid colliding with the
// application service class naming convention (IepDraftService).
public class IepDraftServiceLine : BaseEntity, IAuditableEntity
{
    public int IepDraftId { get; set; }
    public string? ServiceType { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Location { get; set; }
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
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
