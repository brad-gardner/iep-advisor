using System.Text.Json.Serialization;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// --- Service input: a reference to a source document to include in a run ---

public sealed record AnalysisRunSourceRef(AnalysisSourceType SourceType, int SourceId);

// --- Claude response deserialization models (the JSON shape the LLM returns) ---

public class AnalysisRunResponse
{
    [JsonPropertyName("overallSummary")]
    public string OverallSummary { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<AnalysisRunSourceResult> Sources { get; set; } = [];

    // null for single-source runs
    [JsonPropertyName("crossDocSynthesis")]
    public CrossDocSynthesisResult? CrossDocSynthesis { get; set; }

    [JsonPropertyName("overallRedFlags")]
    public List<RedFlag> OverallRedFlags { get; set; } = [];

    [JsonPropertyName("advocacyGapAnalysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

public class AnalysisRunSourceResult
{
    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("sourceId")]
    public int SourceId { get; set; }

    [JsonPropertyName("sections")]
    public List<AnalysisRunSectionResult> Sections { get; set; } = [];
}

public class AnalysisRunSectionResult
{
    [JsonPropertyName("sectionKind")]
    public string SectionKind { get; set; } = string.Empty;

    [JsonPropertyName("plainLanguageSummary")]
    public string PlainLanguageSummary { get; set; } = string.Empty;

    [JsonPropertyName("keyPoints")]
    public List<string> KeyPoints { get; set; } = [];

    [JsonPropertyName("redFlags")]
    public List<RedFlag> RedFlags { get; set; } = [];

    [JsonPropertyName("legalReferences")]
    public List<LegalReference> LegalReferences { get; set; } = [];
}

public class CrossDocSynthesisResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("timeline")]
    public List<string> Timeline { get; set; } = [];

    [JsonPropertyName("contradictions")]
    public List<string> Contradictions { get; set; } = [];

    [JsonPropertyName("progression")]
    public string? Progression { get; set; }
}

// --- Service-facing output models (returned to the controller) ---

public class AnalysisRunModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallSummary { get; set; }
    public CrossDocSynthesisResult? CrossDocSynthesis { get; set; }
    public List<RedFlag> OverallRedFlags { get; set; } = [];
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
    public List<ParentGoalSnapshot> ParentGoalsSnapshot { get; set; } = [];
    public List<AnalysisRunSourceModel> Sources { get; set; } = [];
    public List<AnalysisRunSectionModel> Sections { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AnalysisRunSourceModel
{
    public int Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string? SourceLabel { get; set; }
}

public class AnalysisRunSectionModel
{
    public int Id { get; set; }
    public int? AnalysisRunSourceId { get; set; }
    public string SectionKind { get; set; } = string.Empty;
    public AnalysisRunSectionResult? Analysis { get; set; }
    public int DisplayOrder { get; set; }
}
