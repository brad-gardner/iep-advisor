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

    /// <summary>
    /// Hash-chain tamper-evidence (pilot-gates plan, phase 1). <see cref="Hash"/> is
    /// SHA-256(Id|Action|ActorUserId|ResourceType|ResourceId|RecipientUserId|CreatedAt|PrevHash), computed
    /// by <c>AccessAuditLogWorker</c> (the sole writer) once the row's Id is known. <see cref="PrevHash"/>
    /// is the previous row's <see cref="Hash"/> (null for the very first row ever written). Both are null
    /// on a freshly-migrated historical row until the worker's startup backfill pass computes them, in Id
    /// order. A nightly (and on-demand) integrity walk recomputes the chain and flags the first row whose
    /// stored <see cref="Hash"/> no longer matches — proof the row (or one before it) was altered outside
    /// this one, sanctioned write path.
    /// </summary>
    public string? PrevHash { get; set; }

    public string? Hash { get; set; }
}
