namespace IepAssistant.Domain.Entities;

/// <summary>
/// The reason a goal row is being removed from a Draft instance, captured by the editor's "Remove goal"
/// dialog BEFORE the row is deleted from <see cref="DocumentInstance.ValuesJson"/> (plan 7, decision 6).
/// Consumed by the finalize projection: when a lineage present in the prior finalized version is absent
/// from the new one, its most recent matching (by <see cref="DocumentInstanceId"/>,
/// <see cref="LineageId"/>) retirement reason becomes the retired <see cref="GoalRecord.StatusReason"/>;
/// finalize never blocks on a missing reason — the editor is the enforcement point, not the schema.
/// </summary>
public class GoalRetirement : BaseEntity, IAuditableEntity
{
    public int DocumentInstanceId { get; set; }
    public Guid LineageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RetiredByUserId { get; set; }
    public DateTime RetiredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public DocumentInstance DocumentInstance { get; set; } = null!;
}
