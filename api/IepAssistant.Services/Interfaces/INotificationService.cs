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
    ///
    /// <para><b>Multilingual plan (2026-10-06) phase 4:</b> every recipient's <see cref="User.PreferredLanguage"/>
    /// is batch-loaded in one query (never per recipient, however large the fan-out), and recipients are
    /// grouped by normalized language. <paramref name="buildText"/> is invoked ONCE PER DISTINCT LANGUAGE
    /// (today: at most "en" and "es") inside a <see cref="Services.Localization.CultureScope"/> already
    /// set to that language, so an <c>IStringLocalizer</c> indexer call inside the callback resolves the
    /// right resx entry without the callback needing to touch culture itself — it receives the
    /// normalized language only for the rarer case of formatting a date explicitly (see
    /// <see cref="Services.Localization.NotificationDateFormat"/>). A caller whose text never varies by
    /// recipient may ignore the parameter entirely, e.g. <c>_ => ("Title", "Body")</c>.</para>
    /// </summary>
    Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, Func<string, (string Title, string Body)> buildText,
        string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default);

    Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default);
    Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default);
    Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default);

    /// <summary>Platform-admin view: rows with a recorded email failure, newest first.</summary>
    Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default);
}
