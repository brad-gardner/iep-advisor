namespace IepAssistant.Services.Interfaces;

/// <summary>
/// T-7d/T-1d/T-1h meeting reminders (plan 4, decision 6). All scheduling/idempotency logic lives here
/// (not in <c>MeetingReminderWorker</c>) so it is testable without a timer.
/// </summary>
public interface IMeetingReminderService
{
    /// <summary>One pass: for every Scheduled future meeting whose T-7d/T-1d/T-1h window has been reached,
    /// notifies each participant-with-a-user who hasn't already received that (meeting, offset) reminder.</summary>
    Task RunOnceAsync(DateTime utcNow, CancellationToken ct = default);
}
