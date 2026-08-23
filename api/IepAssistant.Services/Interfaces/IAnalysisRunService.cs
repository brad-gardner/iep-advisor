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
    Task FailRunAsync(int runId, string message, CancellationToken ct = default);

    Task<ServiceResult<List<AnalysisRunModel>>> GetRunsAsync(int childId, int userId, CancellationToken ct = default);

    Task<ServiceResult<AnalysisRunModel>> GetRunAsync(int runId, int userId, CancellationToken ct = default);
}
