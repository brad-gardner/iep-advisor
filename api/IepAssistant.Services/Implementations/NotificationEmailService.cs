using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Decision logic for <c>NotificationEmailWorker</c> (see <see cref="INotificationEmailService"/>). Meeting
/// kinds (Scheduled/Updated/Cancelled) are sent with a freshly-rendered .ics via the matching
/// <see cref="IEmailService"/> method; every other kind (including MeetingReminder, which has no dedicated
/// "with ics" method in the plan-4 contract) uses the generic <see cref="IEmailService.SendNotificationAsync"/>.
/// </summary>
public class NotificationEmailService : INotificationEmailService
{
    public const int MaxAttempts = 3;
    private const int MaxErrorLength = 500;

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IIcsBuilder _icsBuilder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationEmailService> _logger;

    public NotificationEmailService(
        ApplicationDbContext context,
        IEmailService emailService,
        IIcsBuilder icsBuilder,
        IConfiguration configuration,
        ILogger<NotificationEmailService> logger)
    {
        _context = context;
        _emailService = emailService;
        _icsBuilder = icsBuilder;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> FindQueuedIdsAsync(CancellationToken ct = default)
    {
        return await _context.Notifications.AsNoTracking()
            .Where(n => n.EmailQueuedAt != null && n.EmailSentAt == null && n.EmailAttempts < MaxAttempts)
            .OrderBy(n => n.EmailQueuedAt)
            .Select(n => n.Id)
            .ToListAsync(ct);
    }

    public async Task ProcessNotificationAsync(int notificationId, CancellationToken ct = default)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (notification == null)
            return;
        // Re-verify (race guard): a prior cycle, or a concurrent process, may have already resolved this row.
        if (notification.EmailQueuedAt == null || notification.EmailSentAt != null || notification.EmailAttempts >= MaxAttempts)
            return;

        var recipientEmail = await _context.Users.AsNoTracking()
            .Where(u => u.Id == notification.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            notification.EmailAttempts++;
            notification.EmailError = Truncate("Recipient has no email on file.");
            await _context.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var sentViaMeetingFlow = notification.Kind is NotificationKind.MeetingScheduled or NotificationKind.MeetingUpdated or NotificationKind.MeetingCancelled
                && await TrySendMeetingEmailAsync(notification, recipientEmail, ct);

            if (!sentViaMeetingFlow)
                await _emailService.SendNotificationAsync(recipientEmail, notification.Title, notification.Body, BuildLinkUrl(notification.LinkPath), ct);

            notification.EmailSentAt = DateTime.UtcNow;
            notification.EmailError = null;
        }
        catch (Exception ex)
        {
            notification.EmailAttempts++;
            notification.EmailError = Truncate(ex.Message);
            _logger.LogError(ex, "Failed to send notification email {NotificationId} (attempt {Attempt})", notification.Id, notification.EmailAttempts);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Resolves the meeting id from <see cref="Notification.LinkPath"/> (of the form
    /// "/meetings/{id}") and, if the recipient still has a participant row, sends the ICS-attached email.
    /// Returns false (caller falls back to the generic email) when either can't be resolved.</summary>
    private async Task<bool> TrySendMeetingEmailAsync(Notification notification, string recipientEmail, CancellationToken ct)
    {
        var meetingId = ParseMeetingId(notification.LinkPath);
        if (meetingId == null)
            return false;

        var meeting = await _context.Meetings.AsNoTracking()
            .Where(m => m.Id == meetingId.Value)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.StartsAtUtc,
                m.TimeZoneId,
                m.DurationMinutes,
                m.Location,
                m.VideoUrl,
                m.Status,
                StudentFirstName = m.SchoolStudent.FirstName,
                OrganizerName = m.CreatedByUser != null ? (m.CreatedByUser.FirstName + " " + m.CreatedByUser.LastName).Trim() : "Your school team",
                OrganizerEmail = m.CreatedByUser != null ? m.CreatedByUser.Email : null
            })
            .FirstOrDefaultAsync(ct);
        if (meeting == null)
            return false;

        var participant = await _context.MeetingParticipants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId.Value && p.UserId == notification.UserId, ct);
        if (participant == null)
            return false;

        var frontendUrl = FrontendUrl();
        var model = new MeetingEmailModel
        {
            StudentFirstName = meeting.StudentFirstName,
            Title = meeting.Title,
            StartsAtUtc = meeting.StartsAtUtc,
            TimeZoneId = meeting.TimeZoneId,
            DurationMinutes = meeting.DurationMinutes,
            Location = meeting.Location,
            VideoUrl = meeting.VideoUrl,
            OrganizerName = meeting.OrganizerName,
            RsvpAcceptUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Accepted",
            RsvpDeclineUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Declined",
            DetailUrl = $"{frontendUrl}/meetings/{meeting.Id}"
        };

        var icsInput = new IcsMeetingInput
        {
            MeetingId = meeting.Id,
            Title = meeting.Title,
            StartsAtUtc = meeting.StartsAtUtc,
            TimeZoneId = meeting.TimeZoneId,
            DurationMinutes = meeting.DurationMinutes,
            Location = meeting.Location,
            VideoUrl = meeting.VideoUrl,
            Sequence = 0, // Sequence is not needed on this best-effort re-render for email; the authoritative
                          // .ics (with the real SEQUENCE) is available via GET /api/meetings/{id}.ics.
            IsCancelled = meeting.Status == MeetingStatus.Cancelled,
            OrganizerName = meeting.OrganizerName,
            OrganizerEmail = meeting.OrganizerEmail ?? recipientEmail
        };
        var ics = _icsBuilder.BuildMeetingEvent(icsInput, meeting.Status == MeetingStatus.Cancelled ? "CANCEL" : "REQUEST");

        switch (notification.Kind)
        {
            case NotificationKind.MeetingScheduled:
                await _emailService.SendMeetingInvitationAsync(recipientEmail, model, ics, ct);
                break;
            case NotificationKind.MeetingUpdated:
                await _emailService.SendMeetingUpdatedAsync(recipientEmail, model, ics, ct);
                break;
            case NotificationKind.MeetingCancelled:
                await _emailService.SendMeetingCancelledAsync(recipientEmail, model, ics, ct);
                break;
        }
        return true;
    }

    private static int? ParseMeetingId(string? linkPath)
    {
        if (string.IsNullOrWhiteSpace(linkPath))
            return null;
        var parts = linkPath.TrimEnd('/').Split('/');
        return parts.Length > 0 && int.TryParse(parts[^1], out var id) ? id : null;
    }

    private string BuildLinkUrl(string? linkPath) => string.IsNullOrWhiteSpace(linkPath) ? FrontendUrl() : $"{FrontendUrl()}{linkPath}";

    private string FrontendUrl() => _configuration["App:FrontendUrl"] ?? "http://localhost:5173";

    private static string Truncate(string message) => message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
}
