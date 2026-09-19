namespace IepAssistant.Domain.Entities;

/// <summary>
/// One Virtual Advocate conversation about a child. Private to the parent who started it
/// (<see cref="ParentUserId"/>) — co-parents with access to the same child never see each other's
/// threads (design decision 8). Deleting the child cascades to threads and their messages.
/// </summary>
public class AdvocateThread : BaseEntity
{
    public int ChildProfileId { get; set; }
    public int ParentUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Bumped on every persisted message; the thread list is ordered by it.</summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ChildProfile ChildProfile { get; set; } = null!;
    public User ParentUser { get; set; } = null!;
    public ICollection<AdvocateMessage> Messages { get; set; } = new List<AdvocateMessage>();
}
