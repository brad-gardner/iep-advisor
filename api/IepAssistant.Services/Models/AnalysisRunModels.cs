using System.Text.Json.Serialization;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// --- Service input: a reference to a source document to include in a run ---

public sealed record AnalysisRunSourceRef(AnalysisSourceType SourceType, int SourceId);

// --- Claude response deserialization models (the JSON shape the LLM returns) ---
//
// Phase 1 (unified-analysis refactor): the engine now makes ONE Claude call per source, then one
// synthesis call when 2+ sources complete. SourceAnalysisResponse is the per-source call's shape;
// AnalysisRunSynthesisResponse is the synthesis call's shape. Neither carries the other source's
// content — the synthesis call is fed each completed source's *structured* output, never raw
// documents (docs/designs/2026-10-02-unified-analysis-design.md).

/// <summary>Response shape for a single per-source Claude call (one IEP, ETR, or progress report).</summary>
public class SourceAnalysisResponse
{
    [JsonPropertyName("overallSummary")]
    public string OverallSummary { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<AnalysisRunSectionResult> Sections { get; set; } = [];

    /// <summary>IEP sources only (the IEP-specific prompt asks for this; empty for every other
    /// source type). Persisted as a standalone <c>iep_goals</c> section, never an ordinary section.</summary>
    [JsonPropertyName("goalAnalyses")]
    public List<GoalAnalysisResult> GoalAnalyses { get; set; } = [];

    [JsonPropertyName("overallRedFlags")]
    public List<RedFlag> OverallRedFlags { get; set; } = [];

    [JsonPropertyName("advocacyGapAnalysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

/// <summary>Response shape for the synthesis call made when 2+ sources complete.</summary>
public class AnalysisRunSynthesisResponse
{
    [JsonPropertyName("overallSummary")]
    public string OverallSummary { get; set; } = string.Empty;

    [JsonPropertyName("crossDocSynthesis")]
    public CrossDocSynthesisResult? CrossDocSynthesis { get; set; }

    [JsonPropertyName("overallRedFlags")]
    public List<RedFlag> OverallRedFlags { get; set; } = [];

    [JsonPropertyName("advocacyGapAnalysis")]
    public AdvocacyGapAnalysisResponse? AdvocacyGapAnalysis { get; set; }
}

/// <summary>The JSON shape stored in an <c>iep_goals</c> <see cref="AnalysisRunSectionModel"/> row —
/// an object, not an array, so it round-trips distinctly from ordinary sections.</summary>
public class IepGoalsSectionPayload
{
    [JsonPropertyName("goalAnalyses")]
    public List<GoalAnalysisResult> GoalAnalyses { get; set; } = [];
}

/// <summary>Section-kind string constants shared between the prompt builders and the read mapping.</summary>
public static class AnalysisRunSectionKinds
{
    public const string IepGoals = "iep_goals";
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
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class AnalysisRunSectionModel
{
    public int Id { get; set; }
    public int? AnalysisRunSourceId { get; set; }
    public string SectionKind { get; set; } = string.Empty;

    /// <summary>Populated for every ordinary section kind; null for <c>iep_goals</c> (use
    /// <see cref="GoalAnalyses"/> instead) and for a section whose JSON failed to deserialize.</summary>
    public AnalysisRunSectionResult? Analysis { get; set; }

    /// <summary>Populated only when <see cref="SectionKind"/> is <c>iep_goals</c>; null otherwise,
    /// including when the section's JSON failed to deserialize.</summary>
    public List<GoalAnalysisResult>? GoalAnalyses { get; set; }

    public int DisplayOrder { get; set; }
}
