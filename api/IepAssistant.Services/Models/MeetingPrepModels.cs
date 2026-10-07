namespace IepAssistant.Services.Models;

using System.Text.Json.Serialization;

public class ChecklistItem
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("legalBasis")]
    public string? LegalBasis { get; set; }

    [JsonPropertyName("isChecked")]
    public bool IsChecked { get; set; } = false;
}

public class MeetingPrepResponse
{
    [JsonPropertyName("questionsToAsk")]
    public List<ChecklistItem> QuestionsToAsk { get; set; } = [];

    [JsonPropertyName("redFlagsToRaise")]
    public List<ChecklistItem> RedFlagsToRaise { get; set; } = [];

    [JsonPropertyName("preparationNotes")]
    public List<ChecklistItem> PreparationNotes { get; set; } = [];

    // Legacy fields — kept for deserialization of old checklists
    [JsonPropertyName("documentsToBring")]
    public List<ChecklistItem> DocumentsToBring { get; set; } = [];

    [JsonPropertyName("rightsToReference")]
    public List<ChecklistItem> RightsToReference { get; set; } = [];

    [JsonPropertyName("goalGaps")]
    public List<ChecklistItem> GoalGaps { get; set; } = [];

    [JsonPropertyName("generalTips")]
    public List<ChecklistItem> GeneralTips { get; set; } = [];
}

public class MeetingPrepChecklistModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public int? IepDocumentId { get; set; }
    /// <summary>Date of the IEP the checklist was built from (its IEP date, else upload date).</summary>
    public DateTime? IepDocumentDate { get; set; }
    public int? EtrDocumentId { get; set; }
    public DateTime? MeetingDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ChecklistItem> QuestionsToAsk { get; set; } = [];
    public List<ChecklistItem> RedFlagsToRaise { get; set; } = [];
    public List<ChecklistItem> PreparationNotes { get; set; } = [];
    public List<ChecklistItem> DocumentsToBring { get; set; } = [];
    public List<ChecklistItem> RightsToReference { get; set; } = [];
    public List<ChecklistItem> GoalGaps { get; set; } = [];
    public List<ChecklistItem> GeneralTips { get; set; } = [];
    public string? ErrorMessage { get; set; }

    /// <summary>"en" | "es" | null — the language this checklist was generated in (multilingual plan
    /// 2026-10-06 phase 3); null means English, including every checklist generated before this was
    /// tracked.</summary>
    public string? GeneratedLanguage { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CheckItemRequest
{
    public string Section { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool IsChecked { get; set; }
}
