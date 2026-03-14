using System.Text.Json.Serialization;

namespace IepAssistant.Services.Models;

// Claude response deserialization models

public class AnalysisResponse
{
    [JsonPropertyName("overallSummary")]
    public string OverallSummary { get; set; } = string.Empty;

    [JsonPropertyName("sectionAnalyses")]
    public List<SectionAnalysisResult> SectionAnalyses { get; set; } = [];

    [JsonPropertyName("goalAnalyses")]
    public List<GoalAnalysisResult> GoalAnalyses { get; set; } = [];

    [JsonPropertyName("overallRedFlags")]
    public List<RedFlag> OverallRedFlags { get; set; } = [];

    [JsonPropertyName("suggestedQuestions")]
    public List<SuggestedQuestion> SuggestedQuestions { get; set; } = [];

    [JsonPropertyName("advocacyGapAnalysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

public class SectionAnalysisResult
{
    [JsonPropertyName("sectionType")]
    public string SectionType { get; set; } = string.Empty;

    [JsonPropertyName("plainLanguageSummary")]
    public string PlainLanguageSummary { get; set; } = string.Empty;

    [JsonPropertyName("keyPoints")]
    public List<string> KeyPoints { get; set; } = [];

    [JsonPropertyName("redFlags")]
    public List<RedFlag> RedFlags { get; set; } = [];

    [JsonPropertyName("suggestedQuestions")]
    public List<string> SuggestedQuestions { get; set; } = [];

    [JsonPropertyName("legalReferences")]
    public List<LegalReference> LegalReferences { get; set; } = [];
}

public class GoalAnalysisResult
{
    [JsonPropertyName("goalId")]
    public int GoalId { get; set; }

    [JsonPropertyName("goalText")]
    public string GoalText { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("smartAnalysis")]
    public SmartAnalysis SmartAnalysis { get; set; } = new();

    [JsonPropertyName("overallRating")]
    public string OverallRating { get; set; } = "yellow";

    [JsonPropertyName("plainLanguageSummary")]
    public string PlainLanguageSummary { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = [];

    [JsonPropertyName("concerns")]
    public List<string> Concerns { get; set; } = [];

    [JsonPropertyName("suggestedImprovements")]
    public List<string> SuggestedImprovements { get; set; } = [];
}

public class SmartAnalysis
{
    [JsonPropertyName("specific")]
    public SmartCriterion Specific { get; set; } = new();

    [JsonPropertyName("measurable")]
    public SmartCriterion Measurable { get; set; } = new();

    [JsonPropertyName("achievable")]
    public SmartCriterion Achievable { get; set; } = new();

    [JsonPropertyName("relevant")]
    public SmartCriterion Relevant { get; set; } = new();

    [JsonPropertyName("timeBound")]
    public SmartCriterion TimeBound { get; set; } = new();
}

public class SmartCriterion
{
    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "yellow";

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}

public class RedFlag
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "yellow";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("legalBasis")]
    public string? LegalBasis { get; set; }
}

public class SuggestedQuestion
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}

public class LegalReference
{
    [JsonPropertyName("provision")]
    public string Provision { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

// Advocacy gap analysis response models

public class AdvocacyGapAnalysisResponse
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("goalAlignments")]
    public List<GoalAlignmentResult> GoalAlignments { get; set; } = [];
}

public class GoalAlignmentResult
{
    [JsonPropertyName("parentGoalText")]
    public string ParentGoalText { get; set; } = string.Empty;

    [JsonPropertyName("parentGoalCategory")]
    public string? ParentGoalCategory { get; set; }

    [JsonPropertyName("alignmentStatus")]
    public string AlignmentStatus { get; set; } = "not_addressed"; // addressed, partially_addressed, not_addressed

    [JsonPropertyName("alignedIepGoals")]
    public List<string> AlignedIepGoals { get; set; } = [];

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }
}

public class ParentGoalSnapshot
{
    public int Id { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
}

// Service output model

public class IepAnalysisModel
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
    public List<ParentGoalSnapshot> ParentGoalsSnapshot { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
