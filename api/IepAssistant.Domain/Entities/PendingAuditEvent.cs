namespace IepAssistant.Domain.Entities;

/// <summary>
/// Durability staging table for <see cref="AccessAuditLog"/> writes (pilot-gates plan, phase 1).
/// <c>AccessAuditLogWorker</c> writes a row here when a batch fails after its retry budget, or when
/// host shutdown (<c>StopAsync</c>) catches events still sitting in the in-memory channel. A startup
/// pass on the next boot replays every row here into <see cref="AccessAuditLog"/> (assigning it through
/// the same hash-chain path as a live event) and then deletes the pending row — so an audit event
/// can be delayed by a crash/restart, but never silently dropped.
/// </summary>
public class PendingAuditEvent : BaseEntity
{
    public AuditAction ActionValue { get; set; }
    public int ActorUserId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public int? RecipientUserId { get; set; }

    /// <summary>When the audited action actually happened (mirrors <see cref="AccessAuditLog.CreatedAt"/>).</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>When this row was staged here (batch-failure or shutdown-drain time), for triage only.</summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
