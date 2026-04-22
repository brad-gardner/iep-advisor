using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.EtrDocuments;

public class EtrAnalysisDto
{
    public int Id { get; set; }
    public int EtrDocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public AssessmentCompletenessResult? AssessmentCompleteness { get; set; }
    public EligibilityReviewResult? EligibilityReview { get; set; }
    public List<EtrRedFlag> OverallRedFlags { get; set; } = [];
    public List<EtrSuggestedQuestion> SuggestedQuestions { get; set; } = [];
    public string? OverallSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
