namespace IepAssistant.Domain.Entities;

/// <summary>
/// A deliberately-shared, immutable snapshot of a <see cref="DocumentInstance"/> (plan 6, decision 1).
/// Sharing freezes <see cref="ValuesJson"/> and the pinned <see cref="DocumentTemplateVersionId"/> at the
/// moment of sharing so later staff edits to the live instance are never visible to the family until a
/// deliberate re-share creates the next revision. <see cref="RevisionNumber"/> is monotonic per
/// <see cref="DocumentInstanceId"/> (1, 2, 3, …); sharing again marks the previous <see cref="Active"/>
/// revision <see cref="SharedDraftStatus.Superseded"/> and computes <see cref="ChangeSummaryJson"/> as the
/// semantic diff against it. Withdrawn/superseded revisions stay readable to the family as history.
/// </summary>
public class SharedDraftRevision : BaseEntity, IAuditableEntity
{
    public int DocumentInstanceId { get; set; }

    /// <summary>Monotonic per <see cref="DocumentInstanceId"/>, starting at 1.</summary>
    public int RevisionNumber { get; set; }

    /// <summary>Frozen snapshot of the instance's value-document at the moment of sharing. Immutable.</summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>The pinned template version the frozen values are keyed against (carried verbatim from the instance).</summary>
    public int DocumentTemplateVersionId { get; set; }

    public int SharedByUserId { get; set; }
    public DateTime SharedAt { get; set; }

    /// <summary>Optional note from the sharer to the family, shown alongside the revision.</summary>
    public string? Message { get; set; }

    public SharedDraftStatus Status { get; set; } = SharedDraftStatus.Active;
    public DateTime? WithdrawnAt { get; set; }
    public int? WithdrawnByUserId { get; set; }

    /// <summary>
    /// Semantic diff vs. the previous Active revision for this instance (added/removed/changed rows by
    /// `_rowId`, changed narrative fields) — <c>null</c> for the first share. JSON-serialized
    /// <c>ChangeSummaryModel</c>.
    /// </summary>
    public string? ChangeSummaryJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentInstance DocumentInstance { get; set; } = null!;
    public DocumentTemplateVersion DocumentTemplateVersion { get; set; } = null!;
    public User SharedByUser { get; set; } = null!;
}
