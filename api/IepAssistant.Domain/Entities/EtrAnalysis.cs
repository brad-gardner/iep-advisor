namespace IepAssistant.Domain.Entities;

public class EtrAnalysis : BaseEntity, IAuditableEntity
{
    public int EtrDocumentId { get; set; }
    public string Status { get; set; } = "pending"; // pending, analyzing, completed, error
    public string? AssessmentCompleteness { get; set; } // JSON
    public string? EligibilityReview { get; set; } // JSON
    public string? OverallRedFlags { get; set; } // JSON array
    public string? SuggestedQuestions { get; set; } // JSON array
    public string? OverallSummary { get; set; }
    public string? AdvocacyGapAnalysis { get; set; } // JSON
    public string? ParentGoalsSnapshot { get; set; } // JSON
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public EtrDocument EtrDocument { get; set; } = null!;
}
