namespace IepAssistant.Domain.Entities;

/// <summary>Frozen copy of an <see cref="IepDraftGoal"/>; LineageId carried verbatim. Immutable.</summary>
public class IepVersionGoal : BaseEntity
{
    public int IepVersionId { get; set; }
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
