namespace IepAssistant.Domain.Entities;

public class AnalysisRun : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public int? UsageRecordId { get; set; } // Id of the UsageRecord reserved for this run; null once refunded
    public AnalysisRunStatus Status { get; set; } = AnalysisRunStatus.Pending;
    public string? OverallSummary { get; set; }
    public string? CrossDocSynthesis { get; set; } // JSON — cross-document synthesis narrative; null for single-source runs
    public string? OverallRedFlags { get; set; } // JSON
    public string? AdvocacyGapAnalysis { get; set; } // JSON
    public string? ParentGoalsSnapshot { get; set; } // JSON
    public string? ErrorMessage { get; set; }
    // Classified cause of the failure (ClaudeFailureKind name, e.g. "Configuration"), so the UI
    // can vary its affordance instead of offering Retry for a deterministic failure. Null unless failed.
    public string? FailureKind { get; set; }
    // Idempotency marker for the legacy-analysis backfill, e.g. "IepAnalysis:42" / "EtrAnalysis:7".
    // Null for runs created through the normal flow; unique (filtered) when present.
    public string? BackfillSourceKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public ICollection<AnalysisRunSource> Sources { get; set; } = new List<AnalysisRunSource>();
    public ICollection<AnalysisRunSection> Sections { get; set; } = new List<AnalysisRunSection>();
}
