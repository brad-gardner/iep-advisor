namespace IepAssistant.Domain.Entities;

/// <summary>
/// FERPA-aligned, append-only access record (P6a). Written fire-and-forget via a queued background
/// writer — never a synchronous INSERT on a read path. It IS the audit trail, so it deliberately does
/// not implement <see cref="IAuditableEntity"/> (no "who audited the audit" indirection): the actor,
/// action, resource, and timestamp are first-class columns.
/// </summary>
public class AccessAuditLog : BaseEntity
{
    public AuditAction Action { get; set; }

    /// <summary>User who performed the action (viewer, editor, exporter, finalizer, sharer).</summary>
    public int ActorUserId { get; set; }

    /// <summary>Logical resource type, e.g. "IepDraft" / "IepVersion" / "SchoolStudent".</summary>
    public string ResourceType { get; set; } = string.Empty;

    public int ResourceId { get; set; }

    /// <summary>For Share actions: the user the resource was shared with, when known.</summary>
    public int? RecipientUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
