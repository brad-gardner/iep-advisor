namespace IepAssistant.Domain.Entities;

/// <summary>
/// A provider's progress-monitoring data point against a <see cref="GoalRecord"/> (plan 7, decision 6 —
/// the C11 provider-observation slice). Either <see cref="Value"/> or <see cref="Note"/> (or both) is
/// required — enforced by the service, not the schema.
/// </summary>
public class GoalObservation : BaseEntity, IAuditableEntity
{
    public int GoalRecordId { get; set; }
    public DateTime ObservedAt { get; set; }
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public GoalRecord GoalRecord { get; set; } = null!;
}
