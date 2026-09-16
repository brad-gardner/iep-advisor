using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>In-app + optionally-emailed notifications (see <see cref="INotificationService"/>, plan 4 decision 3).</summary>
public class NotificationService : INotificationService
{
    /// <summary>Dedup window for <see cref="NotifyAsync"/>: a duplicate (user, kind, dedupKey) within this
    /// span is skipped rather than re-notified.</summary>
    public static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body,
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

        var now = DateTime.UtcNow;
        var rows = distinctUserIds
            .Where(id => !recentSet.Contains(id))
            .Select(id => new Notification
            {
                UserId = id,
                Kind = kind,
                Title = title,
                Body = body,
                LinkPath = linkPath,
                DedupKey = dedupKey,
                CreatedAt = now,
                EmailQueuedAt = emailImmediately ? now : null
            })
            .ToList();

        if (rows.Count == 0)
            return;

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
            return ServiceResult.FailureResult("Notification not found.");

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
