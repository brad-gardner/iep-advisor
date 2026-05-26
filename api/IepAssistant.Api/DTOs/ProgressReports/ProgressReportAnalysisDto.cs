using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.ProgressReports;

public class ProgressReportAnalysisDto
{
    public int Id { get; set; }
    public int ProgressReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public List<GoalProgressFinding> GoalProgressFindings { get; set; } = [];
    public List<ProgressReportRedFlag> RedFlags { get; set; } = [];
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
    public List<ParentGoalSnapshot> ParentGoalsSnapshot { get; set; } = [];
    public List<IepGoalSnapshot> IepGoalsSnapshot { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
