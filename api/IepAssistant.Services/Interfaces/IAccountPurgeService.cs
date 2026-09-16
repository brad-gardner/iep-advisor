namespace IepAssistant.Services.Interfaces;

/// <summary>Executes one account's 30-days-past-request purge (pilot-gates plan, phase 2, decision 3).
/// Used by <c>AccountPurgeWorker</c>; safe to call more than once for the same user (idempotent) so a
/// crash mid-purge is simply retried to completion on the next hourly cycle.</summary>
public interface IAccountPurgeService
{
    Task PurgeAsync(int userId, CancellationToken ct = default);
}
