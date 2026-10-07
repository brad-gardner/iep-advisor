namespace IepAssistant.Domain.Entities;

public class MeetingPrepChecklist : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public int? IepDocumentId { get; set; }
    public int? EtrDocumentId { get; set; }
    public DateTime? MeetingDate { get; set; }
    public string Status { get; set; } = "pending"; // pending, generating, completed, error
    public string? QuestionsToAsk { get; set; }     // JSON array of ChecklistItem
    public string? DocumentsToBring { get; set; }   // JSON array of ChecklistItem
    public string? RedFlagsToRaise { get; set; }    // JSON array of ChecklistItem
    public string? RightsToReference { get; set; }  // JSON array of ChecklistItem
    public string? GoalGaps { get; set; }           // JSON array of ChecklistItem
    public string? GeneralTips { get; set; }        // JSON array of ChecklistItem (legacy)
    public string? PreparationNotes { get; set; }   // JSON array of ChecklistItem
    public string? ErrorMessage { get; set; }

    /// <summary>The requester's language ("en"/"es") when this checklist was CREATED (multilingual plan
    /// 2026-10-06 phase 3, migration AddAiArtifactLanguage) — captured at create time because generation
    /// runs in a background worker (<c>MeetingPrepWorker</c>) with no ambient request culture of its own;
    /// <c>MeetingPrepService.GenerateChecklistAsync</c> re-applies it via <c>CultureScope.For</c> before
    /// calling Claude. Null means English (including every checklist created before this column existed).</summary>
    public string? Language { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public IepDocument? IepDocument { get; set; }
    public EtrDocument? EtrDocument { get; set; }
}
