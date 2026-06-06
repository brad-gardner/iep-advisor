namespace IepAssistant.Domain.Entities;

public class IepDraftGoal : BaseEntity, IAuditableEntity
{
    public int IepDraftId { get; set; }
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public int DisplayOrder { get; set; }

    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDraft IepDraft { get; set; } = null!;
}
