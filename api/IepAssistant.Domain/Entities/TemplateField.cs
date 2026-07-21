namespace IepAssistant.Domain.Entities;

/// <summary>
/// A single typed field within a <see cref="TemplateSection"/> (State Document Template Engine).
/// Only editable while the owning version is a <see cref="TemplateVersionStatus.Draft"/>; publishing
/// freezes it (immutable, guarded by <c>ImmutableVersionInterceptor</c> + the authoring service).
///
/// <para><see cref="FieldKey"/> is the stable GUID that persisted instance values are keyed by. It is
/// assigned once on create and carried verbatim when a published version is forked into a new draft, so
/// reorder/version changes never remap stored values (the same stable-lineage pattern as
/// <see cref="IepDraftSection.LineageId"/>). It must be unique within a version — enforced both by the
/// authoring service and by a DB unique index on (<see cref="DocumentTemplateVersionId"/>,
/// <see cref="FieldKey"/>).</para>
///
/// <para><see cref="DocumentTemplateVersionId"/> is denormalized (the field also belongs to a section)
/// so version-scoped FieldKey uniqueness can be a single DB index and immutability can be enforced in
/// one hop. <see cref="ConfigJson"/> holds the per-<see cref="FieldType"/> configuration (Select
/// options, Table columns + row bounds, Date format, Text max length); its shape is validated on save
/// and again at publish.</para>
/// </summary>
public class TemplateField : BaseEntity, IAuditableEntity
{
    public int TemplateSectionId { get; set; }

    /// <summary>Denormalized owning-version id (kept in sync with the section's version) for version-scoped uniqueness + immutability checks.</summary>
    public int DocumentTemplateVersionId { get; set; }

    /// <summary>Stable identity that instance values are keyed by. Assigned once; carried verbatim across version forks.</summary>
    public Guid FieldKey { get; set; }

    public FieldType FieldType { get; set; }

    public string Label { get; set; } = string.Empty;

    public bool Required { get; set; }

    /// <summary>Per-<see cref="FieldType"/> configuration JSON (see the authoring service's config validation); null when the type needs none.</summary>
    public string? ConfigJson { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public TemplateSection Section { get; set; } = null!;
    public DocumentTemplateVersion Version { get; set; } = null!;
}
