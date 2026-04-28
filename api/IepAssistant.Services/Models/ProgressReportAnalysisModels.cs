using System.Text.Json.Serialization;

namespace IepAssistant.Services.Models;

// Claude response shape

public class ProgressReportAnalysisResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("goalProgressFindings")]
    public List<GoalProgressFinding> GoalProgressFindings { get; set; } = [];

    [JsonPropertyName("redFlags")]
    public List<ProgressReportRedFlag> RedFlags { get; set; } = [];

    [JsonPropertyName("advocacyGapAnalysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

public class GoalProgressFinding
{
    [JsonPropertyName("iepGoalText")]
    public string IepGoalText { get; set; } = string.Empty;

    [JsonPropertyName("iepGoalId")]
    public int? IepGoalId { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("reportedProgress")]
    public string ReportedProgress { get; set; } = string.Empty;

    [JsonPropertyName("progressRating")]
    public string ProgressRating { get; set; } = "insufficient_data"; // met|on_track|concerning|regressing|insufficient_data

    [JsonPropertyName("evidenceQuality")]
    public string EvidenceQuality { get; set; } = "adequate"; // strong|adequate|weak

    [JsonPropertyName("redFlags")]
    public List<string> RedFlags { get; set; } = [];

    [JsonPropertyName("parentTalkingPoints")]
    public List<string> ParentTalkingPoints { get; set; } = [];
}

public class ProgressReportRedFlag
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium"; // high|medium|low

    [JsonPropertyName("category")]
    public string Category { get; set; } = "other";

    [JsonPropertyName("finding")]
    public string Finding { get; set; } = string.Empty;

    [JsonPropertyName("whyItMatters")]
    public string WhyItMatters { get; set; } = string.Empty;
}

public class IepGoalSnapshot
{
    public int? Id { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Domain { get; set; }
}

// Service output model returned to controller

public class ProgressReportAnalysisModel
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
