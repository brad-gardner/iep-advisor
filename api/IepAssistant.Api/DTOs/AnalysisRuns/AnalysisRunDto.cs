using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.AnalysisRuns;

public class AnalysisRunDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallSummary { get; set; }
    public CrossDocSynthesisResult? CrossDocSynthesis { get; set; }
    public List<RedFlag> OverallRedFlags { get; set; } = [];
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
    public List<ParentGoalSnapshot> ParentGoalsSnapshot { get; set; } = [];
    public List<AnalysisRunSourceDto> Sources { get; set; } = [];
    public List<AnalysisRunSectionDto> Sections { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AnalysisRunSourceDto
{
    public int Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string? SourceLabel { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class AnalysisRunSectionDto
{
    public int Id { get; set; }
    public int? AnalysisRunSourceId { get; set; }
    public string SectionKind { get; set; } = string.Empty;
    public AnalysisRunSectionResult? Analysis { get; set; }
    public List<GoalAnalysisResult>? GoalAnalyses { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>The <c>GET .../analysis-runs/latest</c> response: every <see cref="AnalysisRunDto"/> field,
/// plus the run's other sources (for a "part of a larger analysis" note) and whether it is stale for the
/// requested document.</summary>
public class AnalysisRunLatestDto : AnalysisRunDto
{
    public List<AnalysisRunOtherSourceDto> OtherSources { get; set; } = [];
    public bool Stale { get; set; }
}

public class AnalysisRunOtherSourceDto
{
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string? Label { get; set; }
}
