using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.IepDocuments;

public class IepAnalysisDto
{
    public int Id { get; set; }
    public int IepDocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallSummary { get; set; }
    public List<SectionAnalysisResult> SectionAnalyses { get; set; } = [];
    public List<GoalAnalysisResult> GoalAnalyses { get; set; } = [];
    public List<RedFlag> OverallRedFlags { get; set; } = [];
    public List<SuggestedQuestion> SuggestedQuestions { get; set; } = [];
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
    public List<ParentGoalSnapshot>? ParentGoalsSnapshot { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
