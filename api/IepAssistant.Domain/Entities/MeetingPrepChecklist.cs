namespace IepAssistant.Domain.Entities;

public class MeetingPrepChecklist : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public int? IepDocumentId { get; set; }
    public string Status { get; set; } = "pending"; // pending, generating, completed, error
    public string? QuestionsToAsk { get; set; }     // JSON array of ChecklistItem
    public string? DocumentsToBring { get; set; }   // JSON array of ChecklistItem
    public string? RedFlagsToRaise { get; set; }    // JSON array of ChecklistItem
    public string? RightsToReference { get; set; }  // JSON array of ChecklistItem
    public string? GoalGaps { get; set; }           // JSON array of ChecklistItem
    public string? GeneralTips { get; set; }        // JSON array of ChecklistItem
    public string? ErrorMessage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public IepDocument? IepDocument { get; set; }
}
