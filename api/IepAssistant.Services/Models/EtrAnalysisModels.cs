using System.Text.Json.Serialization;

namespace IepAssistant.Services.Models;

// Claude response deserialization model for ETR four-pillar analysis

public class EtrAnalysisResponse
{
    [JsonPropertyName("assessment_completeness")]
    public AssessmentCompletenessResult? AssessmentCompleteness { get; set; }

    [JsonPropertyName("eligibility_review")]
    public EligibilityReviewResult? EligibilityReview { get; set; }

    [JsonPropertyName("red_flags")]
    public List<EtrRedFlag> RedFlags { get; set; } = [];

    [JsonPropertyName("suggested_questions")]
    public List<EtrSuggestedQuestion> SuggestedQuestions { get; set; } = [];

    [JsonPropertyName("overall_summary")]
    public string? OverallSummary { get; set; }

    [JsonPropertyName("advocacy_gap_analysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

public class AssessmentCompletenessResult
{
    [JsonPropertyName("evaluated_domains")]
    public List<EvaluatedDomain> EvaluatedDomains { get; set; } = [];

    [JsonPropertyName("missing_domains")]
    public List<MissingDomain> MissingDomains { get; set; } = [];

    [JsonPropertyName("overall_completeness_rating")]
    public string OverallCompletenessRating { get; set; } = "adequate"; // strong|adequate|thin|concerning
}

public class EvaluatedDomain
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("tools_used")]
    public List<string> ToolsUsed { get; set; } = [];

    [JsonPropertyName("adequacy_rating")]
    public string AdequacyRating { get; set; } = "adequate"; // strong|adequate|thin|missing

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class MissingDomain
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;
}

public class EligibilityReviewResult
{
    [JsonPropertyName("stated_category")]
    public string? StatedCategory { get; set; }

    [JsonPropertyName("stated_conclusion")]
    public string? StatedConclusion { get; set; }

    [JsonPropertyName("data_supports_conclusion")]
    public bool DataSupportsConclusion { get; set; }

    [JsonPropertyName("supporting_evidence")]
    public List<string> SupportingEvidence { get; set; } = [];

    [JsonPropertyName("contradicting_evidence")]
    public List<string> ContradictingEvidence { get; set; } = [];

    [JsonPropertyName("alternative_considerations")]
    public List<string> AlternativeConsiderations { get; set; } = [];

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class EtrRedFlag
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium"; // high|medium|low

    [JsonPropertyName("category")]
    public string Category { get; set; } = "other"; // outdated_testing|missing_domain|boilerplate|procedural|under_evaluation|other

    [JsonPropertyName("finding")]
    public string Finding { get; set; } = string.Empty;

    [JsonPropertyName("why_it_matters")]
    public string WhyItMatters { get; set; } = string.Empty;

    [JsonPropertyName("parent_right_implicated")]
    public string? ParentRightImplicated { get; set; }
}

public class EtrSuggestedQuestion
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "clarification"; // clarification|challenge_eligibility|iee_request|procedural|services_next_steps

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;
}

// Service output model returned to controller

public class EtrAnalysisModel
{
    public int Id { get; set; }
    public int EtrDocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public AssessmentCompletenessResult? AssessmentCompleteness { get; set; }
    public EligibilityReviewResult? EligibilityReview { get; set; }
    public List<EtrRedFlag> OverallRedFlags { get; set; } = [];
    public List<EtrSuggestedQuestion> SuggestedQuestions { get; set; } = [];
    public string? OverallSummary { get; set; }
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
    public List<ParentGoalSnapshot> ParentGoalsSnapshot { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
