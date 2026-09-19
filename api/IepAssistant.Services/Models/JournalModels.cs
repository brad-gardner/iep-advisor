using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class JournalEntryModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public DateOnly OccurredOn { get; set; }
    public JournalTag Tag { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public int? LinkedIepDocumentId { get; set; }
    public int? LinkedEtrDocumentId { get; set; }
    public int? LinkedMeetingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedById { get; set; }
}

public class SaveJournalEntryModel
{
    public DateOnly OccurredOn { get; set; }
    public JournalTag Tag { get; set; } = JournalTag.Other;
    public string ContentMarkdown { get; set; } = string.Empty;
    public int? LinkedIepDocumentId { get; set; }
    public int? LinkedEtrDocumentId { get; set; }
    public int? LinkedMeetingId { get; set; }
}
