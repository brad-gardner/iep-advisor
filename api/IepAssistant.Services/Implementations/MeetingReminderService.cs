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
        if (candidates.Count == 0)
            return;

        var meetingIds = candidates.Select(m => m.Id).ToList();

        // One batched query for every candidate meeting's participants, and one for every reminder already
        // sent for those meetings — instead of re-querying both per (meeting, offset) on every 15-minute
        // tick, which re-does the same work for offsets that are already fully sent (todos/069).
        var participantsByMeeting = (await _context.MeetingParticipants.AsNoTracking()
                .Where(p => meetingIds.Contains(p.MeetingId) && p.UserId != null)
                .Select(p => new { p.MeetingId, UserId = p.UserId!.Value })
                .Distinct()
                .ToListAsync(ct))
            .GroupBy(p => p.MeetingId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.UserId).ToList());

        var sentByMeetingOffset = (await _context.MeetingReminders.AsNoTracking()
                .Where(r => meetingIds.Contains(r.MeetingId))
                .Select(r => new { r.MeetingId, r.Offset, r.UserId })
                .ToListAsync(ct))
            .GroupBy(r => (r.MeetingId, r.Offset))
            .ToDictionary(g => g.Key, g => g.Select(r => r.UserId).ToHashSet());

        foreach (var meeting in candidates)
        {
            if (!participantsByMeeting.TryGetValue(meeting.Id, out var participantUserIds) || participantUserIds.Count == 0)
                continue;

            foreach (var (offset, span) in OffsetSpans)
            {
                if (utcNow < meeting.StartsAtUtc - span)
                    continue; // this offset's window hasn't opened yet

                var sent = sentByMeetingOffset.TryGetValue((meeting.Id, offset), out var s) ? s : new HashSet<int>();
                var pending = participantUserIds.Where(id => !sent.Contains(id)).ToList();

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
        catch (DbUpdateException ex) when (IsReminderUniqueIndexCollision(ex))
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

    /// <summary>True when the failure is genuinely the (MeetingId, UserId, Offset) unique-index collision
    /// this idempotency guard expects — mirrors <c>EducatorService.IsExternalIdCollision</c>. Any other
    /// <see cref="DbUpdateException"/> (a transient connection failure, an FK violation, etc.) must not be
    /// silently treated as "already sent" (todos/053).</summary>
    internal static bool IsReminderUniqueIndexCollision(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        // SQL Server names the index; SQLite (tests) lists the columns.
        return message.Contains("IX_MeetingReminders_MeetingId_UserId_Offset", StringComparison.OrdinalIgnoreCase)
            || message.Contains("MeetingReminders.MeetingId, MeetingReminders.UserId, MeetingReminders.Offset", StringComparison.OrdinalIgnoreCase);
    }
}
