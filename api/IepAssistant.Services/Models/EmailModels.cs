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

/// <summary>Which clause <see cref="Implementations.EmailService"/> renders for
/// <see cref="StudentInviteContext"/> (multilingual plan 2026-10-06, phase 4 review fix). Previously the
/// caller (<c>StudentInviteService</c>) built a pre-formatted ENGLISH sentence fragment ("to contribute
/// to Sam's IEP" / "at Lincoln High School") and handed it to EmailService as an opaque string, so a
/// Spanish recipient got an English clause mid-sentence. Passing structured data instead lets EmailService
/// render the whole sentence, including this clause, from <c>Emails.resx</c> in the recipient's own
/// language.</summary>
public enum StudentInviteContextKind
{
    /// <summary>A parent invited the student to contribute to a child's IEP; <see cref="StudentInviteContext.ChildFirstName"/> is set.</summary>
    ParentChild,

    /// <summary>An educator invited the student at a school; <see cref="StudentInviteContext.SchoolName"/> is set (null/blank falls back to a localized generic "your school" clause).</summary>
    EducatorSchool
}

public class StudentInviteContext
{
    public StudentInviteContextKind Kind { get; set; }
    public string? ChildFirstName { get; set; }
    public string? SchoolName { get; set; }
}
