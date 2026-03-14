using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IIepAnalysisService
{
    Task<IepAnalysisModel?> GetAnalysisAsync(int documentId, int userId, CancellationToken cancellationToken = default);
    Task AnalyzeDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}
