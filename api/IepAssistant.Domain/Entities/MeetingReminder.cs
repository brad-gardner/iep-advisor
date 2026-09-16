namespace IepAssistant.Domain.Entities;

/// <summary>
/// Idempotency marker for <c>MeetingReminderWorker</c>/<c>MeetingReminderService</c> (plan 4, decision 6):
/// one row per (meeting, user, offset) actually sent. The unique index on those three columns is the sole
/// idempotency mechanism — re-running the same 15-minute cycle finds the row and skips. Not
/// <see cref="IAuditableEntity"/>: this is a system-generated send record, not a user-editable one.
/// </summary>
public class MeetingReminder : BaseEntity
{
    public int MeetingId { get; set; }
    public int UserId { get; set; }
    public ReminderOffset Offset { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
    public User User { get; set; } = null!;
}
