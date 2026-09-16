namespace IepAssistant.Domain.Entities;

/// <summary>
/// A family member's "I've reviewed this revision" stamp (plan 6, decision 7) — explicitly NOT consent
/// or a signature. Idempotent per (revision, user): re-acknowledging updates <see cref="AcknowledgedAt"/>
/// rather than creating a duplicate row.
/// </summary>
public class DraftAcknowledgement : BaseEntity, IAuditableEntity
{
    public int SharedDraftRevisionId { get; set; }
    public int UserId { get; set; }
    public DateTime AcknowledgedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SharedDraftRevision SharedDraftRevision { get; set; } = null!;
    public User User { get; set; } = null!;
}
