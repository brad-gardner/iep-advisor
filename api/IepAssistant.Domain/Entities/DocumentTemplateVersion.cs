namespace IepAssistant.Domain.Entities;

/// <summary>
/// A versioned working copy / published snapshot of a <see cref="DocumentTemplate"/>
/// (State Document Template Engine). A template has at most one <see cref="TemplateVersionStatus.Draft"/>
/// version being authored; publishing freezes it into an immutable <see cref="TemplateVersionStatus.Published"/>
/// version <em>without</em> auto-creating the next draft — further edits require an explicit fork
/// (CreateDraftFromPublished), which copies the latest published version into a new Draft.
///
/// Sections (<see cref="Sections"/>) and their fields are authored while the version is a Draft.
/// <see cref="RowVersion"/> is an optimistic-concurrency token guarding the working copy against
/// concurrent admin edits. It is a plain concurrency-token byte[] rotated by the authoring service on
/// every edit rather than a store-generated <c>rowversion</c>, so it advances deterministically under
/// both SQL Server and the SQLite test provider.
/// </summary>
public class DocumentTemplateVersion : BaseEntity, IAuditableEntity
{
    public int DocumentTemplateId { get; set; }

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; } = TemplateVersionStatus.Draft;

    public DateTime? PublishedAt { get; set; }

    /// <summary>Optimistic-concurrency token for the working copy; rotated by the authoring service on each edit.</summary>
    public byte[]? RowVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentTemplate DocumentTemplate { get; set; } = null!;
    public ICollection<TemplateSection> Sections { get; set; } = new List<TemplateSection>();
}
