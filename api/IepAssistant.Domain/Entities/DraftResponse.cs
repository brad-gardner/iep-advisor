namespace IepAssistant.Domain.Entities;

/// <summary>
/// A family member's response (Agree/Question/ChangeRequest/Comment) to a whole revision or one of its
/// items (plan 6, decision 4). Visible to the parent who wrote it and to staff (Collaborator+) on the
/// student; staff resolve it with a reply and/or a "resolved in the draft" flag.
/// </summary>
public class DraftResponse : BaseEntity, IAuditableEntity
{
    public int SharedDraftRevisionId { get; set; }
    public int ParentUserId { get; set; }

    /// <summary>Optional target (a template FieldKey); null for a whole-revision response.</summary>
    public Guid? TargetFieldKey { get; set; }

    /// <summary>Optional target row id (`_rowId`) within a table field.</summary>
    public string? TargetRowId { get; set; }

    public DraftResponseKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;

    public DraftResponseStatus Status { get; set; } = DraftResponseStatus.Open;
    public int? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? StaffReply { get; set; }
    public bool ResolvedInDraft { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SharedDraftRevision SharedDraftRevision { get; set; } = null!;
    public User ParentUser { get; set; } = null!;
}
