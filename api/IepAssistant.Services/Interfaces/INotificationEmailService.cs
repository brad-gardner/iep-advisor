namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Decision logic for <c>NotificationEmailWorker</c> — kept out of the hosted service so it is unit
/// testable without a timer, mirroring <see cref="IStaffInviteExpiryService"/>. Meeting-kind notifications
/// (MeetingScheduled/Updated/Cancelled) are sent through the matching ICS-attaching
/// <see cref="IEmailService"/> method; every other kind uses the generic
/// <see cref="IEmailService.SendNotificationAsync"/>.
/// </summary>
public interface INotificationEmailService
{
    /// <summary>Ids of queued, unsent, not-yet-exhausted (&lt; 3 attempts) notification rows.</summary>
    Task<IReadOnlyList<int>> FindQueuedIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Re-verifies the row is still eligible, sends it, and records the outcome: <c>EmailSentAt</c> on
    /// success, or an incremented <c>EmailAttempts</c> + truncated <c>EmailError</c> on failure. Never throws.
    /// </summary>
    Task ProcessNotificationAsync(int notificationId, CancellationToken ct = default);
}
