using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IProgressReportService
{
    Task<IEnumerable<ProgressReportModel>> GetByIepIdAsync(int iepDocumentId, int userId, CancellationToken cancellationToken = default);
    Task<ProgressReportModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProgressReportModel>> CreateAsync(int iepDocumentId, int userId, CreateProgressReportModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProgressReportModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateMetadataAsync(int id, int userId, CreateProgressReportModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default);
}
