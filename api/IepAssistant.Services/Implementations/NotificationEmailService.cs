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

    /// <summary>Caps FindQueuedIdsAsync so a burst (bulk reschedule, ACS outage, mass meeting creation)
    /// is bounded, predictable work per 30s cycle rather than draining the entire queue in one pass
    /// (todos/068).</summary>
    private const int BatchSize = 200;

    /// <summary>Backoff after attempt 1/2/3 respectively (index = attempts-1, clamped) — spreads the 3
    /// allowed attempts over up to ~36 minutes instead of ~90 seconds of fixed 30s-tick retries, so a
    /// transient outage doesn't burn through every attempt before it clears (todos/068).</summary>
    private static readonly TimeSpan[] RetryBackoffs = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) };

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
        var now = DateTime.UtcNow;
        return await _context.Notifications.AsNoTracking()
            .Where(n => n.EmailQueuedAt != null && n.EmailSentAt == null && n.EmailAttempts < MaxAttempts
                     && (n.NextAttemptAt == null || n.NextAttemptAt <= now))
            .OrderBy(n => n.EmailQueuedAt)
            .Take(BatchSize)
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
            notification.NextAttemptAt = ComputeNextAttemptAt(notification.EmailAttempts);
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
            notification.NextAttemptAt = null;
        }
        catch (Exception ex)
        {
            notification.EmailAttempts++;
            notification.EmailError = Truncate(ex.Message);
            notification.NextAttemptAt = ComputeNextAttemptAt(notification.EmailAttempts);
            _logger.LogError(ex, "Failed to send notification email {NotificationId} (attempt {Attempt})", notification.Id, notification.EmailAttempts);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>1 min after attempt 1, 5 min after attempt 2, 30 min after attempt 3+ (todos/068).</summary>
    private static DateTime ComputeNextAttemptAt(int attemptsSoFar)
    {
        var index = Math.Clamp(attemptsSoFar - 1, 0, RetryBackoffs.Length - 1);
        return DateTime.UtcNow + RetryBackoffs[index];
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
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .Include(m => m.CreatedByUser)
            .FirstOrDefaultAsync(m => m.Id == meetingId.Value, ct);
        if (meeting == null)
            return false;

        var participant = meeting.Participants.FirstOrDefault(p => p.UserId == notification.UserId);
        if (participant == null)
            return false;

        var frontendUrl = FrontendUrl();
        var organizerName = meeting.CreatedByUser != null
            ? $"{meeting.CreatedByUser.FirstName} {meeting.CreatedByUser.LastName}".Trim()
            : "Your school team";
        var model = new MeetingEmailModel
        {
            StudentFirstName = meeting.SchoolStudent.FirstName,
            Title = meeting.Title,
            StartsAtUtc = meeting.StartsAtUtc,
            TimeZoneId = meeting.TimeZoneId,
            DurationMinutes = meeting.DurationMinutes,
            Location = meeting.Location,
            VideoUrl = meeting.VideoUrl,
            OrganizerName = organizerName,
            RsvpAcceptUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Accepted",
            RsvpDeclineUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Declined",
            DetailUrl = $"{frontendUrl}/meetings/{meeting.Id}"
        };

        // Shared with CalendarService's authoritative GET /api/meetings/{id}.ics mapping (todos/051,
        // todos/064) so this best-effort emailed .ics carries the real (possibly bumped) Sequence.
        var icsInput = IcsMeetingInputMapper.Map(meeting);
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
