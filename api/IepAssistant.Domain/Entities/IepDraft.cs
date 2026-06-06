namespace IepAssistant.Domain.Entities;

public class IepDraft : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public IepDraftStatus Status { get; set; } = IepDraftStatus.Draft;
    public IepDocumentType DocumentType { get; set; } = IepDocumentType.Iep;
    public string? Title { get; set; }

    // Overall last-edit stamp (mirrors the last writer across any child entity).
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;

    public ICollection<IepDraftSection> Sections { get; set; } = new List<IepDraftSection>();
    public ICollection<IepDraftGoal> Goals { get; set; } = new List<IepDraftGoal>();
    public ICollection<IepDraftServiceLine> ServiceLines { get; set; } = new List<IepDraftServiceLine>();
    public ICollection<IepDraftAccommodation> Accommodations { get; set; } = new List<IepDraftAccommodation>();
    public ICollection<IepDraftTransitionItem> TransitionItems { get; set; } = new List<IepDraftTransitionItem>();
}
