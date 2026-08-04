namespace IepAssistant.Domain.Entities;

/// <summary>
/// Document-type lookup table for the State Document Template Engine (e.g. IEP, Section504, ETR).
/// A DB lookup (not a code enum) so admins can add new authored document types without a deploy.
/// Seeded with stable IDs 1=IEP, 2=Section504, 3=ETR (see <see cref="Data.Configurations.DocumentTypeConfiguration"/>).
///
/// Distinct from the code-side <see cref="IepDocumentType"/> enum used by the legacy typed
/// IepDraft/IepVersion aggregate and from the student-upload EtrDocument system.
/// </summary>
public class DocumentType : BaseEntity, IAuditableEntity
{
    /// <summary>Stable machine key, e.g. "IEP", "Section504", "ETR". Unique.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable name shown in admin UI dropdowns.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Inactive types are hidden from selection but retained for existing references.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<DocumentTemplate> Templates { get; set; } = new List<DocumentTemplate>();
}
