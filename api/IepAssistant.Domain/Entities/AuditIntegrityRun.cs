namespace IepAssistant.Domain.Entities;

/// <summary>Outcome of one <see cref="AccessAuditLog"/> hash-chain walk (pilot-gates plan, phase 1).</summary>
public enum AuditIntegrityStatus
{
    Ok,
    Broken,
    Failed
}

/// <summary>
/// Record of one hash-chain integrity walk over <see cref="AccessAuditLog"/> (nightly, plus an
/// on-demand platform-admin trigger). The walk recomputes each row's hash from its stored fields and
/// <see cref="AccessAuditLog.PrevHash"/> and compares it to the stored <see cref="AccessAuditLog.Hash"/>;
/// the first mismatch stops the walk and is recorded as <see cref="FirstBrokenId"/>.
/// </summary>
public class AuditIntegrityRun : BaseEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RowsChecked { get; set; }
    public int? FirstBrokenId { get; set; }
    public AuditIntegrityStatus Status { get; set; }
    public string? Detail { get; set; }
}
