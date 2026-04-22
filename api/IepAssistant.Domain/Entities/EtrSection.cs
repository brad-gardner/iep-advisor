namespace IepAssistant.Domain.Entities;

public class EtrSection : BaseEntity, IAuditableEntity
{
    public int EtrDocumentId { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public string? RawText { get; set; }
    public string? ParsedContent { get; set; } // JSON
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public EtrDocument EtrDocument { get; set; } = null!;
}
