using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>In-app + optionally-emailed notifications (see <see cref="INotificationService"/>, plan 4 decision 3).</summary>
public class NotificationService : INotificationService
{
    /// <summary>Dedup window for <see cref="NotifyAsync"/>: a duplicate (user, kind, dedupKey) within this
    /// span is skipped rather than re-notified.</summary>
    public static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<Messages> _localizer;

    public NotificationService(ApplicationDbContext context, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, Func<string, (string Title, string Body)> buildText,
        string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default)
    {
        var distinctUserIds = userIds.Distinct().ToList();
        if (distinctUserIds.Count == 0)
            return;

        var cutoff = DateTime.UtcNow.Add(-DedupWindow);
        var recentlyNotified = await _context.Notifications.AsNoTracking()
            .Where(n => distinctUserIds.Contains(n.UserId) && n.Kind == kind && n.DedupKey == dedupKey && n.CreatedAt >= cutoff)
            .Select(n => n.UserId)
            .ToListAsync(ct);
        var recentSet = recentlyNotified.ToHashSet();

        var pendingUserIds = distinctUserIds.Where(id => !recentSet.Contains(id)).ToList();
        if (pendingUserIds.Count == 0)
            return;

        // Batch-load every pending recipient's language in ONE query — never per recipient — so a large
        // fan-out (every platform admin, every district staff member, ...) stays a fixed number of
        // queries regardless of recipient count (multilingual plan 2026-10-06 phase 4, decision 1).
        var languageByUserId = await _context.Users.AsNoTracking()
            .Where(u => pendingUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.PreferredLanguage })
            .ToDictionaryAsync(u => u.Id, u => u.PreferredLanguage, ct);

        var now = DateTime.UtcNow;
        var rows = new List<Notification>();
        // Grouping collapses to at most SupportedLanguages.All.Count groups (today: en/es), so
        // buildText — which may call an IStringLocalizer — runs once per LANGUAGE, not once per
        // recipient, and each group's resource lookups resolve under that language's CultureScope.
        foreach (var group in pendingUserIds.GroupBy(id => SupportedLanguages.Normalize(languageByUserId.GetValueOrDefault(id)) ?? SupportedLanguages.English))
        {
            using var _ = CultureScope.For(group.Key);
            var (title, body) = buildText(group.Key);
            rows.AddRange(group.Select(userId => new Notification
            {
                UserId = userId,
                Kind = kind,
                Title = title,
                Body = body,
                LinkPath = linkPath,
                DedupKey = dedupKey,
                CreatedAt = now,
                EmailQueuedAt = emailImmediately ? now : null
            }));
        }

        await _context.Notifications.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 200);
        var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(boundedLimit)
            .Select(Map)
            .ToListAsync(ct);
        var unreadCount = await _context.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

        return ServiceResult<NotificationListModel>.SuccessResult(new NotificationListModel { Items = items, UnreadCount = unreadCount });
    }

    public async Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification == null)
            return ServiceResult.NotFound(_localizer["Notifications.NotFound"]);

        if (notification.ReadAt == null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var marked = await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
        return ServiceResult<int>.SuccessResult(marked);
    }

    public async Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default)
    {
        var boundedMax = Math.Clamp(maxCount, 1, 200);
        var items = await _context.Notifications.AsNoTracking()
            .Where(n => n.EmailError != null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(boundedMax)
            .Select(Map)
            .ToListAsync(ct);
        return ServiceResult<List<NotificationModel>>.SuccessResult(items);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Notification, NotificationModel>> Map = n => new NotificationModel
    {
        Id = n.Id,
        Kind = n.Kind,
        Title = n.Title,
        Body = n.Body,
        LinkPath = n.LinkPath,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt,
        EmailSentAt = n.EmailSentAt,
        EmailError = n.EmailError
    };
}
