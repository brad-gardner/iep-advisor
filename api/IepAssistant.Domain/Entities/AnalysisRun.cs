namespace IepAssistant.Domain.Entities;

public class AnalysisRun : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public AnalysisRunStatus Status { get; set; } = AnalysisRunStatus.Pending;
    public string? OverallSummary { get; set; }
    public string? CrossDocSynthesis { get; set; } // JSON — cross-document synthesis narrative; null for single-source runs
    public string? OverallRedFlags { get; set; } // JSON
    public string? AdvocacyGapAnalysis { get; set; } // JSON
    public string? ParentGoalsSnapshot { get; set; } // JSON
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public ICollection<AnalysisRunSource> Sources { get; set; } = new List<AnalysisRunSource>();
    public ICollection<AnalysisRunSection> Sections { get; set; } = new List<AnalysisRunSection>();
}
