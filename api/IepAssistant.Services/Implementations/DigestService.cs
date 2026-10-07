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
/// Daily digest of DueSoon/Overdue obligations + meetings in the next 7 days (see
/// <see cref="IDigestService"/>, plan 4 decision 3). Sends the rich <see cref="DigestEmailModel"/> email
/// directly (rather than queuing a generic Notification for <c>NotificationEmailWorker</c>, which only
/// has a title/body to work with) and records the outcome on the same in-app <see cref="Notification"/>
/// row it creates for the bell, so a failure is still visible to the platform-admin failures view.
/// </summary>
public class DigestService : IDigestService
{
    private const int MaxErrorLength = 500;
    private const int MeetingLookaheadDays = 7;

    private readonly ApplicationDbContext _context;
    private readonly IObligationService _obligationService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DigestService> _logger;
    private readonly IStringLocalizer<Notifications> _notificationsLocalizer;

    public DigestService(ApplicationDbContext context, IObligationService obligationService, IEmailService emailService, IConfiguration configuration, ILogger<DigestService> logger, IStringLocalizer<Notifications> notificationsLocalizer)
    {
        _context = context;
        _obligationService = obligationService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _notificationsLocalizer = notificationsLocalizer;
    }

    public async Task RunForDateAsync(DateOnly localDate, CancellationToken ct = default)
    {
        var dedupKey = localDate.ToString("yyyy-MM-dd");
        var staffUserIds = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(ct);
        if (staffUserIds.Count == 0)
            return;

        // Idempotent per (user, date): one batched query for every staff user instead of an AnyAsync per user.
        var alreadySentUserIds = await _context.Notifications.AsNoTracking()
            .Where(n => staffUserIds.Contains(n.UserId) && n.Kind == NotificationKind.ObligationDigest && n.DedupKey == dedupKey)
            .Select(n => n.UserId)
            .ToListAsync(ct);
        var pendingUserIds = staffUserIds.Except(alreadySentUserIds).ToList();
        if (pendingUserIds.Count == 0)
            return;

        // Obligations for every pending staff user in a handful of queries — lead caseload for everyone
        // plus the full scope for School/District admins, i.e. exactly what GetMineAsync would give each
        // of them one at a time (todos/067, pass-2 fix).
        var obligationsByUser = (await _obligationService.GetForStaffDigestAsync(pendingUserIds, ct))
            .Select(kv => (kv.Key, Items: kv.Value.Where(o => o.Status is ObligationStatus.DueSoon or ObligationStatus.Overdue).ToList()))
            .Where(kv => kv.Items.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Items);

        // Upcoming meetings for every pending staff user in one query, grouped by participant in memory.
        // An explicit join (rather than a Where(...) inside a SelectMany over the Participants navigation)
        // so this stays a plain SQL join every provider (including SQLite in tests) can translate.
        var now = DateTime.UtcNow;
        var meetingRows = await _context.MeetingParticipants.AsNoTracking()
            .Where(p => p.UserId != null && pendingUserIds.Contains(p.UserId.Value))
            .Join(
                _context.Meetings.AsNoTracking().Where(m => m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= now && m.StartsAtUtc <= now.AddDays(MeetingLookaheadDays)),
                p => p.MeetingId, m => m.Id,
                (p, m) => new { UserId = p.UserId!.Value, m.Title, m.StartsAtUtc, m.TimeZoneId, StudentName = m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName })
            .ToListAsync(ct);
        var meetingsByUser = meetingRows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.StartsAtUtc).ToList());

        var usersWithContent = pendingUserIds
            .Where(id => (obligationsByUser.TryGetValue(id, out var o) && o.Count > 0) || (meetingsByUser.TryGetValue(id, out var m) && m.Count > 0))
            .ToList();
        if (usersWithContent.Count == 0)
            return;

        var userInfoById = await _context.Users.AsNoTracking()
            .Where(u => usersWithContent.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.Email, u.PreferredLanguage })
            .ToDictionaryAsync(u => u.Id, ct);

        var notificationsByUser = new Dictionary<int, Notification>();
        foreach (var userId in usersWithContent)
        {
            if (!userInfoById.TryGetValue(userId, out var info) || string.IsNullOrWhiteSpace(info.Email))
                continue;

            var dueOrOverdue = obligationsByUser.TryGetValue(userId, out var obl) ? obl : new List<ObligationModel>();
            var upcomingMeetings = meetingsByUser.TryGetValue(userId, out var mtg) ? mtg : new();

            // Multilingual plan (2026-10-06) phase 4: the bell title/body follow this recipient's own
            // language — one user at a time is already a single batched user-info query above, so no
            // extra per-recipient query is introduced here.
            //
            // Phase 4 review fix: the deadline/meeting clauses use real count==1 vs other plural resx keys
            // instead of the English-only "(s)"/Spanish "(es)" shortcut, which showed the parenthetical
            // literally (e.g. "1 deadline(s)") regardless of count.
            string title, body;
            using (CultureScope.For(info.PreferredLanguage))
            {
                title = _notificationsLocalizer["Notifications.Digest.Title"];
                string deadlineClause = dueOrOverdue.Count == 1
                    ? _notificationsLocalizer["Notifications.Digest.DeadlineCountOne", dueOrOverdue.Count]
                    : _notificationsLocalizer["Notifications.Digest.DeadlineCountOther", dueOrOverdue.Count];
                string meetingClause = upcomingMeetings.Count == 1
                    ? _notificationsLocalizer["Notifications.Digest.MeetingCountOne", upcomingMeetings.Count, MeetingLookaheadDays]
                    : _notificationsLocalizer["Notifications.Digest.MeetingCountOther", upcomingMeetings.Count, MeetingLookaheadDays];
                body = _notificationsLocalizer["Notifications.Digest.Body", deadlineClause, meetingClause];
            }

            var notification = new Notification
            {
                UserId = userId,
                Kind = NotificationKind.ObligationDigest,
                Title = title,
                Body = body,
                LinkPath = "/notifications",
                DedupKey = dedupKey,
                CreatedAt = DateTime.UtcNow
            };
            notificationsByUser[userId] = notification;
            await _context.Notifications.AddAsync(notification, ct);
        }
        if (notificationsByUser.Count == 0)
            return;
        await _context.SaveChangesAsync(ct); // one batched insert for every user's notification row

        var detailUrl = $"{_configuration["App:FrontendUrl"] ?? "http://localhost:5173"}/notifications";
        foreach (var (userId, notification) in notificationsByUser)
        {
            ct.ThrowIfCancellationRequested();
            var info = userInfoById[userId];
            var dueOrOverdue = obligationsByUser.TryGetValue(userId, out var obl) ? obl : new List<ObligationModel>();
            var upcomingMeetings = meetingsByUser.TryGetValue(userId, out var mtg) ? mtg : new();

            var model = new DigestEmailModel
            {
                RecipientFirstName = info.FirstName,
                Obligations = dueOrOverdue.Select(o => new DigestObligationItem
                {
                    StudentName = o.StudentName,
                    Kind = o.Kind,
                    Status = o.Status,
                    DueDate = o.DueDate
                }).ToList(),
                UpcomingMeetings = upcomingMeetings.Select(m => new DigestMeetingItem
                {
                    StudentName = m.StudentName,
                    Title = m.Title,
                    StartsAtUtc = m.StartsAtUtc,
                    TimeZoneId = m.TimeZoneId
                }).ToList(),
                DetailUrl = detailUrl
            };

            try
            {
                // Pass this recipient's already-loaded language straight through so the digest EMAIL
                // renders in the same language as the bell title/body built above, without EmailService
                // re-querying the same user row to resolve it again.
                await _emailService.SendDigestAsync(info.Email, model, info.PreferredLanguage, ct);
                notification.EmailSentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.EmailAttempts = 1;
                notification.EmailError = ex.Message.Length <= MaxErrorLength ? ex.Message : ex.Message[..MaxErrorLength];
                _logger.LogError(ex, "Failed to send digest email to user {UserId}", userId);
            }
        }

        await _context.SaveChangesAsync(ct); // one batched update recording every send outcome
    }
}
