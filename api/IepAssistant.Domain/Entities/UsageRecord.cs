namespace IepAssistant.Domain.Entities;

public class UsageRecord : BaseEntity
{
    public int UserId { get; set; }
    public int ChildProfileId { get; set; }
    public string OperationType { get; set; } = string.Empty; // "analysis", "meeting_prep"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ChildProfile ChildProfile { get; set; } = null!;
}
