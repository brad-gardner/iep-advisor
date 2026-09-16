namespace IepAssistant.Domain.Entities;

/// <summary>
/// In-app (and optionally emailed) notification (plan 4, decision 3). Dedup is
/// (<see cref="UserId"/>, <see cref="Kind"/>, <see cref="DedupKey"/>) within a rolling 24h window —
/// enforced by <c>NotificationService.NotifyAsync</c>, not a DB constraint (a rolling window can't be a
/// plain unique index). <see cref="EmailQueuedAt"/> is set when the kind is emailed immediately;
/// <c>NotificationEmailWorker</c> drains queued rows, retrying up to 3 attempts
/// (<see cref="EmailAttempts"/>) and recording <see cref="EmailError"/> on failure — never silent.
/// </summary>
public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkPath { get; set; }

    /// <summary>Dedup key within (UserId, Kind) — e.g. "meeting-{id}-{sequence}" or an ISO date for a digest.</summary>
    public string DedupKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public DateTime? EmailQueuedAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public int EmailAttempts { get; set; }
    public string? EmailError { get; set; }

    /// <summary>Backoff gate for a failed send: null (never attempted, or already sent) means eligible
    /// immediately; otherwise <c>NotificationEmailService.FindQueuedIdsAsync</c> excludes the row until
    /// this time (1m/5m/30m after attempts 1/2/3) so a burst of failures doesn't retry every 30s tick.</summary>
    public DateTime? NextAttemptAt { get; set; }

    public User User { get; set; } = null!;
}
