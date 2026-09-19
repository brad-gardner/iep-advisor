namespace IepAssistant.Domain.Entities;

/// <summary>
/// A dated, parent-authored journal entry about a child (incident, communication, medical note, progress
/// observation…). Markdown-at-rest (<see cref="ContentMarkdown"/> is sanitized before persisting). Private
/// to the family: every reader must hold <see cref="AccessRole.Viewer"/>+ on the child; nothing here is
/// ever surfaced to a linked school team. The optional links point at records that belong to (or are
/// visible for) the same child — validated at write time.
/// </summary>
public class JournalEntry : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public DateOnly OccurredOn { get; set; }
    public JournalTag Tag { get; set; } = JournalTag.Other;
    public string ContentMarkdown { get; set; } = string.Empty;
    public int? LinkedIepDocumentId { get; set; }
    public int? LinkedEtrDocumentId { get; set; }
    public int? LinkedMeetingId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public IepDocument? LinkedIepDocument { get; set; }
    public EtrDocument? LinkedEtrDocument { get; set; }
    public Meeting? LinkedMeeting { get; set; }
}
