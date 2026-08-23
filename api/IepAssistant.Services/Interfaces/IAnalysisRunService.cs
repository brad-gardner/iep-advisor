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
    /// Transitions a run to Error and refunds its reserved quota unit. Idempotent: a no-op if the
    /// run is already terminal (Completed/Error) or its unit was already refunded.
    /// </summary>
    /// <param name="failureKind">
    /// Classified cause, persisted so the UI can vary its affordance. Null for failures with no
    /// Claude-level classification (parse failures, interruptions).
    /// </param>
    Task FailRunAsync(int runId, string message, ClaudeFailureKind? failureKind = null, CancellationToken ct = default);

    Task<ServiceResult<List<AnalysisRunModel>>> GetRunsAsync(int childId, int userId, CancellationToken ct = default);

    Task<ServiceResult<AnalysisRunModel>> GetRunAsync(int runId, int userId, CancellationToken ct = default);
}
