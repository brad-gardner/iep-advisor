namespace IepAssistant.Domain.Entities;

/// <summary>
/// A structured decision captured live during a Held/Continued/InProgress <see cref="Meeting"/> (plan 7,
/// decision 3): an optional target (a template field/row the decision is about), free text, and an
/// outcome. Never auto-applied — a human uses <c>GET /api/documents/{instanceId}/proposed-edits</c> to
/// review decisions in the editor and edits the draft themselves, then calls
/// <c>POST /api/decisions/{id}/mark-applied</c>.
/// </summary>
public class MeetingDecision : BaseEntity, IAuditableEntity
{
    public int MeetingId { get; set; }

    /// <summary>The template field this decision targets, when known (e.g. the Goals table's FieldKey).</summary>
    public Guid? TargetFieldKey { get; set; }

    /// <summary>The specific row (<c>_rowId</c>) this decision targets, when it is about one row of a table field.</summary>
    public string? TargetRowId { get; set; }

    /// <summary>Human-readable label captured at record time (e.g. the row's goal text) so the decision
    /// still reads sensibly even if the target field/row later disappears from the draft.</summary>
    public string? TargetLabel { get; set; }

    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }

    public int RecordedByUserId { get; set; }

    /// <summary>Set by <c>POST /api/decisions/{id}/mark-applied</c> once a human has applied the edit to a draft.</summary>
    public DateTime? AppliedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Meeting Meeting { get; set; } = null!;
}
