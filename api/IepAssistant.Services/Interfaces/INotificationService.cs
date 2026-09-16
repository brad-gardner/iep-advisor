using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>In-app + optionally-emailed notifications (plan 4, decision 3).</summary>
public interface INotificationService
{
    /// <summary>
    /// Creates one <see cref="Notification"/> row per user id, de-duplicated per user on
    /// (<paramref name="kind"/>, <paramref name="dedupKey"/>) within a rolling 24h window. When
    /// <paramref name="emailImmediately"/> is true, the row is queued for <c>NotificationEmailWorker</c>.
    /// </summary>
    Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body,
        string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default);

    Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default);
    Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default);
    Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default);

    /// <summary>Platform-admin view: rows with a recorded email failure, newest first.</summary>
    Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default);
}
