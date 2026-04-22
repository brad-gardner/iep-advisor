using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEtrDocumentService
{
    Task<IEnumerable<EtrDocumentModel>> GetByChildIdAsync(int childProfileId, int userId, CancellationToken cancellationToken = default);
    Task<EtrDocumentModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<EtrDocumentModel>> CreateAsync(int childProfileId, int userId, CreateEtrDocumentModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateMetadataAsync(int id, int userId, UpdateEtrMetadataModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<EtrDocumentModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default);
    Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
}
