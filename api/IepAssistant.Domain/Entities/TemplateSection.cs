namespace IepAssistant.Domain.Entities;

/// <summary>
/// A section within a <see cref="DocumentTemplateVersion"/> (State Document Template Engine): a titled,
/// ordered group of <see cref="TemplateField"/> rows. Only editable while the owning version is a
/// <see cref="TemplateVersionStatus.Draft"/>; publishing freezes the version (and its sections/fields)
/// into an immutable snapshot guarded by <c>ImmutableVersionInterceptor</c> and the authoring service.
///
/// <para><see cref="SectionKey"/> is a stable GUID assigned once on create and carried verbatim when a
/// published version is forked into a new draft, so persisted instance values (keyed by field key,
/// grouped by section) stay mappable across versions — the same stable-lineage pattern as
/// <see cref="IepDraftSection.LineageId"/>.</para>
/// </summary>
public class TemplateSection : BaseEntity, IAuditableEntity
{
    public int DocumentTemplateVersionId { get; set; }

    /// <summary>Stable identity assigned once on create; carried verbatim across version forks. Never changed on update.</summary>
    public Guid SectionKey { get; set; }

    public string Title { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentTemplateVersion DocumentTemplateVersion { get; set; } = null!;
    public ICollection<TemplateField> Fields { get; set; } = new List<TemplateField>();
}
