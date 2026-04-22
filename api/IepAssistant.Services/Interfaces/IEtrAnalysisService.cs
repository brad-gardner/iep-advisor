using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEtrAnalysisService
{
    Task<EtrAnalysisModel?> GetAnalysisAsync(int etrDocumentId, int userId, CancellationToken cancellationToken = default);
    Task AnalyzeDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default);
    Task<bool> CheckEtrAnalysisLimitAsync(int userId, int childId, CancellationToken cancellationToken = default);
}
