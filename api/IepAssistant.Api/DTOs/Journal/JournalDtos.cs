using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Journal;

public class JournalEntryDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    /// <summary>yyyy-MM-dd (the day the entry is about, not when it was written).</summary>
    public string OccurredOn { get; set; } = string.Empty;
    /// <summary>Incident | Communication | Medical | Progress | Other</summary>
    public string Tag { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public int? LinkedIepDocumentId { get; set; }
    public int? LinkedEtrDocumentId { get; set; }
    public int? LinkedMeetingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedById { get; set; }
}

public class SaveJournalEntryRequest
{
    /// <summary>yyyy-MM-dd; must not be after today (UTC).</summary>
    [Required] public string OccurredOn { get; set; } = string.Empty;
    /// <summary>Incident | Communication | Medical | Progress | Other (case-insensitive).</summary>
    [Required] public string Tag { get; set; } = string.Empty;
    /// <summary>Markdown; sanitized then limited to 4000 characters.</summary>
    [Required] public string ContentMarkdown { get; set; } = string.Empty;
    public int? LinkedIepDocumentId { get; set; }
    public int? LinkedEtrDocumentId { get; set; }
    public int? LinkedMeetingId { get; set; }
}
