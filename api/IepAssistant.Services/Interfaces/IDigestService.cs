namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Daily 07:00 (district tz) digest of DueSoon/Overdue obligations + meetings in the next 7 days (plan 4,
/// decision 3). Scheduling lives in <c>DigestWorker</c>; this service owns the per-date, per-user content
/// and send/record logic so it is testable without a timer.
/// </summary>
public interface IDigestService
{
    /// <summary>Runs the digest for every eligible staff user, exactly once per <paramref name="localDate"/>
    /// (dedup key = the date) — safe to call more than once for the same date.</summary>
    Task RunForDateAsync(DateOnly localDate, CancellationToken ct = default);
}
