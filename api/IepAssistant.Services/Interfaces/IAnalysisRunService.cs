using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IAnalysisRunService
{
    Task<ServiceResult<AnalysisRunModel>> CreateRunAsync(
        int childId,
        int userId,
        IReadOnlyList<AnalysisRunSourceRef> sources,
        CancellationToken ct = default);

    Task ExecuteRunAsync(int runId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a run to Error and, unless <paramref name="refundQuota"/> is false, refunds its
    /// reserved quota unit. Idempotent: a no-op if the run is already terminal (Completed/Error) or
    /// its unit was already refunded. <paramref name="refundQuota"/> is false for an InvalidResponse
    /// failure (todos/P2-02): the call was genuinely billed, so the unit is consumed rather than
    /// refunded — otherwise a document crafted to make Claude's output unparseable could retry
    /// indefinitely at zero quota cost.
    /// </summary>
    Task FailRunAsync(int runId, string message, bool refundQuota = true, CancellationToken ct = default);

    /// <summary>
    /// Fails (and refunds) every run still <see cref="Domain.Entities.AnalysisRunStatus.Running"/>
    /// whose last transition into Running was more than <paramref name="staleAfter"/> ago — a run
    /// stuck past the HTTP client's own timeout with no process restart to trigger the startup
    /// reconcile. Called every 5 minutes by <c>AnalysisRunWorker</c>'s periodic sweep; exposed here
    /// so the 30-minute threshold is independently testable without a live timer.
    /// </summary>
    Task FailStaleRunsAsync(TimeSpan staleAfter, CancellationToken ct = default);

    Task<ServiceResult<List<AnalysisRunModel>>> GetRunsAsync(int childId, int userId, CancellationToken ct = default);

    Task<ServiceResult<AnalysisRunModel>> GetRunAsync(int runId, int userId, CancellationToken ct = default);
}
