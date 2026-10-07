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

    /// <summary>The language ("en"/"es") this analysis was generated in (multilingual plan 2026-10-06
    /// phase 3, migration AddAiArtifactLanguage) — captured from the owning child's parent's saved
    /// <c>User.PreferredLanguage</c> when <c>ProgressReportAnalysisService.AnalyzeAsync</c> runs (it
    /// executes in a background worker with no requester-specific request culture of its own, and its
    /// enqueue call site is owned by a different concurrent work item, so the account's own saved
    /// preference — not an ephemeral request culture captured elsewhere — is the language signal here).
    /// Null means English, including every analysis generated before this column existed.</summary>
    public string? Language { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ProgressReport ProgressReport { get; set; } = null!;
}
