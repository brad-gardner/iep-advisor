using System.Text.Json.Serialization;

namespace IepAssistant.Services.Models;

public class ParsedIep
{
    [JsonPropertyName("sections")]
    public List<ParsedSection> Sections { get; set; } = [];
}

public class ParsedSection
{
    [JsonPropertyName("sectionType")]
    public string SectionType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("goals")]
    public List<ParsedGoal>? Goals { get; set; }
}

public class ParsedGoal
{
    [JsonPropertyName("goalText")]
    public string GoalText { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("baseline")]
    public string? Baseline { get; set; }

    [JsonPropertyName("targetCriteria")]
    public string? TargetCriteria { get; set; }

    [JsonPropertyName("measurementMethod")]
    public string? MeasurementMethod { get; set; }

    [JsonPropertyName("timeframe")]
    public string? Timeframe { get; set; }
}

public class IepSectionModel
{
    public int Id { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public string? RawText { get; set; }
    public string? ParsedContent { get; set; }
    public int DisplayOrder { get; set; }
    public List<GoalModel> Goals { get; set; } = [];
}

public class GoalModel
{
    public int Id { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
}
