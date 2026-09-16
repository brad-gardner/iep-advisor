using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>One attendee line for an ICS VEVENT.</summary>
public class IcsAttendee
{
    public string? Name { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public InviteStatus InviteStatus { get; set; } = InviteStatus.Pending;
}

/// <summary>Everything <see cref="Implementations.IcsBuilder"/> needs to render one meeting VEVENT.</summary>
public class IcsMeetingInput
{
    public int MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public string? Notes { get; set; }
    public int Sequence { get; set; }
    public bool IsCancelled { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public List<IcsAttendee> Attendees { get; set; } = new();
}

/// <summary>One all-day event for the per-user calendar feed (a meeting or an obligation due date).</summary>
public class IcsFeedEvent
{
    public string Uid { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }

    /// <summary>Set for a timed meeting event; null for an all-day obligation event (uses <see cref="DateOnly"/>).</summary>
    public DateTime? StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? TimeZoneId { get; set; }

    public DateOnly? AllDayDate { get; set; }
    public int Sequence { get; set; }
}
