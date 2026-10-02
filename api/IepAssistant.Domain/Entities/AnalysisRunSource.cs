namespace IepAssistant.Domain.Entities;

public class AnalysisRunSource : BaseEntity
{
    public int AnalysisRunId { get; set; }
    public AnalysisSourceType SourceType { get; set; }
    public int SourceId { get; set; } // FK-by-value to the underlying IepDocument/EtrDocument/ProgressReport row (polymorphic, not a hard FK)
    public string? SourceLabel { get; set; } // human label captured at enqueue, e.g. "IEP — Annual Review 2025-03-12"
    public string? SourceContentSnapshot { get; set; } // extracted text/section content captured at enqueue so the run stays stable if the source is deleted
    public AnalysisRunSourceStatus Status { get; set; } = AnalysisRunSourceStatus.Pending;
    public string? ErrorMessage { get; set; } // user-safe message when Status == Error

    public AnalysisRun AnalysisRun { get; set; } = null!;
}
