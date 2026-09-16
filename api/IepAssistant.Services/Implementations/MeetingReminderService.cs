using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>T-7d/T-1d/T-1h meeting reminders (see <see cref="IMeetingReminderService"/>, plan 4 decision 6).</summary>
public class MeetingReminderService : IMeetingReminderService
{
    private static readonly IReadOnlyDictionary<ReminderOffset, TimeSpan> OffsetSpans = new Dictionary<ReminderOffset, TimeSpan>
    {
        [ReminderOffset.SevenDays] = TimeSpan.FromDays(7),
        [ReminderOffset.OneDay] = TimeSpan.FromDays(1),
        [ReminderOffset.OneHour] = TimeSpan.FromHours(1)
    };

    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<MeetingReminderService> _logger;

    public MeetingReminderService(ApplicationDbContext context, INotificationService notifications, ILogger<MeetingReminderService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RunOnceAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Only meetings within the widest offset window (7 days) can have any reminder due.
        var candidates = await _context.Meetings.AsNoTracking()
            .Where(m => m.Status == MeetingStatus.Scheduled && m.StartsAtUtc > utcNow && m.StartsAtUtc <= utcNow.AddDays(7))
            .Select(m => new { m.Id, m.Title, m.StartsAtUtc, StudentName = m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName })
            .ToListAsync(ct);

        foreach (var meeting in candidates)
        {
            foreach (var (offset, span) in OffsetSpans)
            {
                if (utcNow < meeting.StartsAtUtc - span)
                    continue; // this offset's window hasn't opened yet

                var participantUserIds = await _context.MeetingParticipants.AsNoTracking()
                    .Where(p => p.MeetingId == meeting.Id && p.UserId != null)
                    .Select(p => p.UserId!.Value)
                    .Distinct()
                    .ToListAsync(ct);
                if (participantUserIds.Count == 0)
                    continue;

                var alreadySent = await _context.MeetingReminders.AsNoTracking()
                    .Where(r => r.MeetingId == meeting.Id && r.Offset == offset && participantUserIds.Contains(r.UserId))
                    .Select(r => r.UserId)
                    .ToListAsync(ct);
                var pending = participantUserIds.Except(alreadySent).ToList();

                foreach (var userId in pending)
                    await SendOneReminderAsync(meeting.Id, meeting.Title, meeting.StudentName, userId, offset, utcNow, ct);
            }
        }
    }

    private async Task SendOneReminderAsync(int meetingId, string meetingTitle, string studentName, int userId, ReminderOffset offset, DateTime utcNow, CancellationToken ct)
    {
        var reminder = new MeetingReminder { MeetingId = meetingId, UserId = userId, Offset = offset, SentAt = utcNow };
        _context.MeetingReminders.Add(reminder);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-index collision: another pass (or a concurrent instance) already claimed this
            // (meeting, user, offset) — the idempotency guard doing its job. Detach and move on.
            _context.Entry(reminder).State = EntityState.Detached;
            return;
        }

        var title = $"Reminder: {meetingTitle}";
        var body = $"{meetingTitle} for {studentName} is coming up.";
        try
        {
            await _notifications.NotifyAsync(new[] { userId }, NotificationKind.MeetingReminder, title, body,
                $"/meetings/{meetingId}", $"meeting-{meetingId}-reminder-{offset}", emailImmediately: true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue reminder notification for meeting {MeetingId}, user {UserId}, offset {Offset}", meetingId, userId, offset);
        }
    }
}
