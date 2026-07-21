using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Domain.Entities;

/// <summary>
/// A versioned working copy / published snapshot of a <see cref="DocumentTemplate"/>
/// (State Document Template Engine). A template always has exactly one <see cref="TemplateVersionStatus.Draft"/>
/// version being authored; publishing freezes it into an immutable <see cref="TemplateVersionStatus.Published"/>
/// version and forks a new Draft for further edits.
///
/// Sections and fields are added in a later phase; this phase only tracks the version envelope.
/// <see cref="RowVersion"/> is an optimistic-concurrency token guarding the working copy against
/// concurrent admin edits (SQL Server rowversion; not auto-populated under the SQLite test provider).
/// </summary>
public class DocumentTemplateVersion : BaseEntity, IAuditableEntity
{
    public int DocumentTemplateId { get; set; }

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; } = TemplateVersionStatus.Draft;

    public DateTime? PublishedAt { get; set; }

    /// <summary>Optimistic-concurrency token for the working copy.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentTemplate DocumentTemplate { get; set; } = null!;
}
