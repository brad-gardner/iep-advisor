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

        foreach (var userId in staffUserIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ProcessUserAsync(userId, dedupKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Digest failed for user {UserId}", userId);
            }
        }
    }

    private async Task ProcessUserAsync(int userId, string dedupKey, CancellationToken ct)
    {
        // Idempotent per (user, date): a re-run for the same date (worker restart, manual retrigger) is a no-op.
        var alreadySent = await _context.Notifications.AsNoTracking()
            .AnyAsync(n => n.UserId == userId && n.Kind == NotificationKind.ObligationDigest && n.DedupKey == dedupKey, ct);
        if (alreadySent)
            return;

        var obligationsResult = await _obligationService.GetMineAsync(userId, null, ct);
        var dueOrOverdue = (obligationsResult.Data ?? new List<ObligationModel>())
            .Where(o => o.Status is ObligationStatus.DueSoon or ObligationStatus.Overdue)
            .ToList();

        var now = DateTime.UtcNow;
        var upcomingMeetings = await _context.Meetings.AsNoTracking()
            .Where(m => m.Status == MeetingStatus.Scheduled
                     && m.StartsAtUtc >= now && m.StartsAtUtc <= now.AddDays(MeetingLookaheadDays)
                     && m.Participants.Any(p => p.UserId == userId))
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => new { m.Title, m.StartsAtUtc, m.TimeZoneId, StudentName = m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName })
            .ToListAsync(ct);

        if (dueOrOverdue.Count == 0 && upcomingMeetings.Count == 0)
            return;

        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.Email })
            .FirstOrDefaultAsync(ct);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
            return;

        var title = "Your daily IEP Advisor digest";
        var body = $"{dueOrOverdue.Count} deadline(s) due soon or overdue, {upcomingMeetings.Count} meeting(s) in the next {MeetingLookaheadDays} days.";

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
        await _context.Notifications.AddAsync(notification, ct);
        await _context.SaveChangesAsync(ct);

        var model = new DigestEmailModel
        {
            RecipientFirstName = user.FirstName,
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
            DetailUrl = $"{_configuration["App:FrontendUrl"] ?? "http://localhost:5173"}/notifications"
        };

        try
        {
            await _emailService.SendDigestAsync(user.Email, model, ct);
            notification.EmailSentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            notification.EmailAttempts = 1;
            notification.EmailError = ex.Message.Length <= MaxErrorLength ? ex.Message : ex.Message[..MaxErrorLength];
            _logger.LogError(ex, "Failed to send digest email to user {UserId}", userId);
        }

        await _context.SaveChangesAsync(ct);
    }
}
