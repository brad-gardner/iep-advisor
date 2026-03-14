namespace IepAssistant.Domain.Entities;

public class Goal : BaseEntity, IAuditableEntity
{
    public int IepSectionId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepSection IepSection { get; set; } = null!;
}
