using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IIepProcessingService
{
    Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<IepSectionModel>> GetSectionsAsync(int documentId, int userId, CancellationToken cancellationToken = default);
}
