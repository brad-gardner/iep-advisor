namespace IepAssistant.Domain.Entities;

public class ProgressReportAnalysis : BaseEntity, IAuditableEntity
{
    public int ProgressReportId { get; set; }
    public string Status { get; set; } = "pending"; // pending, analyzing, completed, error
    public string? Summary { get; set; }
    public string? GoalProgressFindings { get; set; } // JSON array
    public string? RedFlags { get; set; }             // JSON array
    public string? AdvocacyGapAnalysis { get; set; }  // JSON
    public string? ParentGoalsSnapshot { get; set; }  // JSON
    public string? IepGoalsSnapshot { get; set; }     // JSON
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ProgressReport ProgressReport { get; set; } = null!;
}
