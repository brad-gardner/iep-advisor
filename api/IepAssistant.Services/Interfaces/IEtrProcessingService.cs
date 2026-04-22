using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEtrProcessingService
{
    Task ProcessDocumentAsync(int etrId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EtrSectionModel>> GetSectionsAsync(int etrId, int userId, CancellationToken cancellationToken = default);
}
