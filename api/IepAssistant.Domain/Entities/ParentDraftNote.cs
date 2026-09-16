namespace IepAssistant.Domain.Entities;

/// <summary>
/// A parent's private question about a <see cref="SharedDraftRevision"/> and its AI-generated answer
/// (plan 6, decision 3). Always private to the asking parent — never visible to staff, and answered
/// only from the revision plus the parent's own evidence (their child profile and their own
/// <see cref="ParentContribution"/>s, shared or not).
/// </summary>
public class ParentDraftNote : BaseEntity, IAuditableEntity
{
    public int SharedDraftRevisionId { get; set; }
    public int ParentUserId { get; set; }

    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    /// <summary>Optional target the question was asked about (a template FieldKey).</summary>
    public Guid? TargetFieldKey { get; set; }

    /// <summary>Optional target row id (`_rowId`) within a table field.</summary>
    public string? TargetRowId { get; set; }

    /// <summary>
    /// The resolved citations the answer was grounded in, as JSON (<c>[{fieldKey,rowId,label,excerpt}]</c>),
    /// so a reloaded note still shows the parent *what in the draft* the answer came from.
    /// </summary>
    public string? CitationsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SharedDraftRevision SharedDraftRevision { get; set; } = null!;
    public User ParentUser { get; set; } = null!;
}
