using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The single place that turns a loaded <see cref="Meeting"/> (with <c>Participants.User</c>,
/// <c>SchoolStudent</c> and <c>CreatedByUser</c> navigation properties populated) into the
/// <see cref="IcsMeetingInput"/> the pure <see cref="IIcsBuilder"/> renders. Shared by
/// <see cref="CalendarService"/> (the authoritative <c>GET /api/meetings/{id}.ics</c> endpoint) and
/// <see cref="NotificationEmailService"/> (the emailed .ics attachment) so the two mappings can't drift
/// out of sync again — the emailed .ics previously hardcoded <c>Sequence = 0</c> because it independently
/// re-implemented this mapping without it (todos/051, todos/064).
/// </summary>
internal static class IcsMeetingInputMapper
{
    /// <summary>The organizer-name fallback (no <c>CreatedByUser</c> on file) resolves from
    /// <paramref name="localizer"/> against whatever culture is ambient when this is called:
    /// <see cref="CalendarService"/> calls it inside its own request, already culture-aware from
    /// <c>RequestLocalization</c>; <see cref="NotificationEmailService"/> wraps the call in
    /// <see cref="Localization.CultureScope.For"/> for the recipient it is emailing.</summary>
    public static IcsMeetingInput Map(Meeting meeting, IStringLocalizer<Emails> localizer)
    {
        var organizerName = meeting.CreatedByUser != null
            ? $"{meeting.CreatedByUser.FirstName} {meeting.CreatedByUser.LastName}".Trim()
            : localizer["Meeting.OrganizerFallback"].Value;
        var organizerEmail = meeting.CreatedByUser?.Email ?? "no-reply@iep-advisor.com";

        return new IcsMeetingInput
        {
            MeetingId = meeting.Id,
            Title = meeting.Title,
            StartsAtUtc = meeting.StartsAtUtc,
            TimeZoneId = meeting.TimeZoneId,
            DurationMinutes = meeting.DurationMinutes,
            Location = meeting.Location,
            VideoUrl = meeting.VideoUrl,
            Notes = meeting.Notes,
            Sequence = meeting.Sequence,
            IsCancelled = meeting.Status == MeetingStatus.Cancelled,
            OrganizerName = organizerName,
            OrganizerEmail = organizerEmail,
            Attendees = meeting.Participants.Select(p => new IcsAttendee
            {
                Name = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : p.ExternalName,
                Email = p.User != null ? p.User.Email : (p.ExternalEmail ?? string.Empty),
                IsRequired = p.IsRequired,
                InviteStatus = p.InviteStatus
            }).Where(a => !string.IsNullOrWhiteSpace(a.Email)).ToList()
        };
    }
}
