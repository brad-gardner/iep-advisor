namespace IepAssistant.Domain.Entities;

public class IepAnalysis : BaseEntity, IAuditableEntity
{
    public int IepDocumentId { get; set; }
    public string Status { get; set; } = "pending"; // pending, analyzing, completed, error
    public string? SectionAnalyses { get; set; } // JSON
    public string? GoalAnalyses { get; set; } // JSON
    public string? OverallSummary { get; set; }
    public string? OverallRedFlags { get; set; } // JSON
    public string? SuggestedQuestions { get; set; } // JSON
    public string? AdvocacyGapAnalysis { get; set; } // JSON
    public string? ParentGoalsSnapshot { get; set; } // JSON
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDocument IepDocument { get; set; } = null!;
}
