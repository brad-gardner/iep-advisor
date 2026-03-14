using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IIepDocumentService
{
    Task<IEnumerable<IepDocumentModel>> GetByChildIdAsync(int childProfileId, int userId, CancellationToken cancellationToken = default);
    Task<IepDocumentModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IepDocumentModel>> UploadAsync(int childProfileId, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default);
}
