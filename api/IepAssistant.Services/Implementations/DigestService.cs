using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
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

    public DigestService(ApplicationDbContext context, IObligationService obligationService, IEmailService emailService, IConfiguration configuration, ILogger<DigestService> logger)
    {
        _context = context;
        _obligationService = obligationService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
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

        // Lead obligations for every pending staff user in one query, grouped by owner in memory, instead
        // of GetMineAsync's (GetStaffContextAsync + LoadLeadStudentsAsync) per user (todos/067).
        var obligationsResult = await _obligationService.GetForLeadUsersAsync(pendingUserIds, ct);
        var obligationsByUser = (obligationsResult.Data ?? new List<ObligationModel>())
            .Where(o => o.Status is ObligationStatus.DueSoon or ObligationStatus.Overdue)
            .GroupBy(o => o.OwnerUserId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

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
            .Select(u => new { u.Id, u.FirstName, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        var notificationsByUser = new Dictionary<int, Notification>();
        foreach (var userId in usersWithContent)
        {
            if (!userInfoById.TryGetValue(userId, out var info) || string.IsNullOrWhiteSpace(info.Email))
                continue;

            var dueOrOverdue = obligationsByUser.TryGetValue(userId, out var obl) ? obl : new List<ObligationModel>();
            var upcomingMeetings = meetingsByUser.TryGetValue(userId, out var mtg) ? mtg : new();

            var notification = new Notification
            {
                UserId = userId,
                Kind = NotificationKind.ObligationDigest,
                Title = "Your daily IEP Advisor digest",
                Body = $"{dueOrOverdue.Count} deadline(s) due soon or overdue, {upcomingMeetings.Count} meeting(s) in the next {MeetingLookaheadDays} days.",
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
                await _emailService.SendDigestAsync(info.Email, model, ct);
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
