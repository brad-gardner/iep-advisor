namespace IepAssistant.Domain.Entities;

public class AnalysisRunSection : BaseEntity
{
    public int AnalysisRunId { get; set; }
    public int? AnalysisRunSourceId { get; set; } // which source this section analyzes; null for run-level sections
    public string SectionKind { get; set; } = string.Empty; // e.g. "present_levels", "annual_goals", "eligibility"
    public string? Analysis { get; set; } // JSON
    public int DisplayOrder { get; set; }

    public AnalysisRun AnalysisRun { get; set; } = null!;
}
