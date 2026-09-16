using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>Content for a meeting invitation/update/cancellation email (plan 4). The ICS bytes travel
/// alongside this model as a separate attachment parameter on the <c>IEmailService</c> method.</summary>
public class MeetingEmailModel
{
    public string StudentFirstName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? RsvpAcceptUrl { get; set; }
    public string? RsvpDeclineUrl { get; set; }
    public string DetailUrl { get; set; } = string.Empty;
}

public class DigestObligationItem
{
    public string StudentName { get; set; } = string.Empty;
    public ObligationKind Kind { get; set; }
    public ObligationStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
}

public class DigestMeetingItem
{
    public string StudentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
}

public class DigestEmailModel
{
    public string RecipientFirstName { get; set; } = string.Empty;
    public List<DigestObligationItem> Obligations { get; set; } = new();
    public List<DigestMeetingItem> UpcomingMeetings { get; set; } = new();
    public string DetailUrl { get; set; } = string.Empty;
}
