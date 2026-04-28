using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IProgressReportAnalysisService
{
    Task<ProgressReportAnalysisModel?> GetAnalysisAsync(int progressReportId, int userId, CancellationToken cancellationToken = default);
    Task AnalyzeAsync(int progressReportId, CancellationToken cancellationToken = default);
}
