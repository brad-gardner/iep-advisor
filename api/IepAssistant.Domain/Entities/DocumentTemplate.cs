namespace IepAssistant.Domain.Entities;

/// <summary>
/// An admin-authored template for a specific <c>(state, documentType)</c> pairing
/// (State Document Template Engine). Identity is <see cref="StateCode"/> + <see cref="DocumentTypeId"/>;
/// a <c>null</c> <see cref="StateCode"/> is the default template used when no state-specific one exists
/// (mirrors the <c>KnowledgeBaseService</c> <c>State == null</c> default precedent). <see cref="StateCode"/>
/// is normalized to a 2-letter uppercase code in the service before persisting.
///
/// Each template owns one or more <see cref="DocumentTemplateVersion"/> rows (Draft/Published).
/// </summary>
public class DocumentTemplate : BaseEntity, IAuditableEntity
{
    /// <summary>Normalized 2-letter uppercase state code, or <c>null</c> for the default template.</summary>
    public string? StateCode { get; set; }

    public int DocumentTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentType DocumentType { get; set; } = null!;
    public ICollection<DocumentTemplateVersion> Versions { get; set; } = new List<DocumentTemplateVersion>();
}
