using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Decision logic for <c>NotificationEmailWorker</c> (see <see cref="INotificationEmailService"/>). Meeting
/// kinds (Scheduled/Updated/Cancelled) are sent with a freshly-rendered .ics via the matching
/// <see cref="IEmailService"/> method; every other kind (including MeetingReminder, which has no dedicated
/// "with ics" method in the plan-4 contract) uses the generic <see cref="IEmailService.SendNotificationAsync"/>.
///
/// Multilingual plan (2026-10-06) phase 4 review fix: this resolves the RECIPIENT's own
/// <see cref="Domain.Entities.User.PreferredLanguage"/> (same per-recipient-record lookup EmailService
/// itself does) and uses it to set the emailed .ics's localized <see cref="IcsMeetingInput.VideoLabel"/>
/// before handing the bytes to EmailService — keeping <see cref="IIcsBuilder"/>/<see cref="IcsBuilder"/>
/// dependency-free (no <c>IStringLocalizer</c> of its own) was judged the smaller change versus moving
/// .ics construction into EmailService.
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
    private readonly IStringLocalizer<Emails> _localizer;

    public NotificationEmailService(
        ApplicationDbContext context,
        IEmailService emailService,
        IIcsBuilder icsBuilder,
        IConfiguration configuration,
        ILogger<NotificationEmailService> logger,
        IStringLocalizer<Emails> localizer)
    {
        _context = context;
        _emailService = emailService;
        _icsBuilder = icsBuilder;
        _configuration = configuration;
        _logger = logger;
        _localizer = localizer;
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

        // PreferredLanguage rides along with this same lookup (no extra query) so the emailed .ics's
        // localized chrome (IcsMeetingInput.VideoLabel) can follow this recipient's own language, the
        // same per-recipient-record lookup EmailService itself does.
        var recipient = await _context.Users.AsNoTracking()
            .Where(u => u.Id == notification.UserId)
            .Select(u => new { u.Email, u.PreferredLanguage })
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(recipient?.Email))
        {
            notification.EmailAttempts++;
            notification.EmailError = Truncate("Recipient has no email on file.");
            notification.NextAttemptAt = ComputeNextAttemptAt(notification.EmailAttempts);
            await _context.SaveChangesAsync(ct);
            return;
        }
        var recipientEmail = recipient.Email;

        try
        {
            var sentViaMeetingFlow = notification.Kind is NotificationKind.MeetingScheduled or NotificationKind.MeetingUpdated or NotificationKind.MeetingCancelled
                && await TrySendMeetingEmailAsync(notification, recipientEmail, recipient.PreferredLanguage, ct);

            if (!sentViaMeetingFlow)
                await _emailService.SendNotificationAsync(recipientEmail, notification.Title, notification.Body, BuildLinkUrl(notification.LinkPath), recipient.PreferredLanguage, ct);

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
    private async Task<bool> TrySendMeetingEmailAsync(Notification notification, string recipientEmail, string? recipientLanguage, CancellationToken ct)
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
        var normalizedLang = SupportedLanguages.Normalize(recipientLanguage) ?? SupportedLanguages.English;
        var langSuffix = $"&lang={Uri.EscapeDataString(normalizedLang)}";

        // The "Your school team"/"Organizer" fallbacks (no CreatedByUser on file) and the emailed .ics's
        // DESCRIPTION "Video:" label are all resolved under THIS recipient's language in one scope —
        // IcsMeetingInputMapper's own defaults stay English, so CalendarService's authoritative
        // single-meeting download (which has no per-recipient language of its own to resolve) is
        // unaffected.
        string organizerName;
        IcsMeetingInput icsInput;
        using (CultureScope.For(recipientLanguage))
        {
            organizerName = meeting.CreatedByUser != null
                ? $"{meeting.CreatedByUser.FirstName} {meeting.CreatedByUser.LastName}".Trim()
                : _localizer["Meeting.OrganizerTeamFallback"].Value;

            // Shared with CalendarService's authoritative GET /api/meetings/{id}.ics mapping (todos/051,
            // todos/064) so this best-effort emailed .ics carries the real (possibly bumped) Sequence.
            icsInput = IcsMeetingInputMapper.Map(meeting, _localizer);
            icsInput.VideoLabel = _localizer["Meeting.VideoLabel"].Value;
        }
        var ics = _icsBuilder.BuildMeetingEvent(icsInput, meeting.Status == MeetingStatus.Cancelled ? "CANCEL" : "REQUEST");

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
            RsvpAcceptUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Accepted{langSuffix}",
            RsvpDeclineUrl = $"{frontendUrl}/meetings/rsvp?token={participant.RsvpToken}&status=Declined{langSuffix}",
            DetailUrl = $"{frontendUrl}/meetings/{meeting.Id}"
        };

        switch (notification.Kind)
        {
            case NotificationKind.MeetingScheduled:
                await _emailService.SendMeetingInvitationAsync(recipientEmail, model, ics, recipientLanguage, ct);
                break;
            case NotificationKind.MeetingUpdated:
                await _emailService.SendMeetingUpdatedAsync(recipientEmail, model, ics, recipientLanguage, ct);
                break;
            case NotificationKind.MeetingCancelled:
                await _emailService.SendMeetingCancelledAsync(recipientEmail, model, ics, recipientLanguage, ct);
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
