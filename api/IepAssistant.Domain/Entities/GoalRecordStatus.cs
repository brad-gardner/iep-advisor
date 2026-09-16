namespace IepAssistant.Domain.Entities;

/// <summary>
/// Lifecycle status of a <see cref="GoalRecord"/> (plan 7, decision 6). By construction, exactly one
/// non-terminal status (<see cref="Active"/>/<see cref="Met"/>/<see cref="NotMet"/>) exists per
/// <see cref="GoalRecord.LineageId"/> at any time — the "current" record for that goal lineage.
/// <see cref="Carried"/> and <see cref="Retired"/> are always superseded-by-history rows produced by a
/// later finalize's projection.
/// </summary>
public enum GoalRecordStatus
{
    Active,
    Met,
    NotMet,
    Retired,
    Carried
}
