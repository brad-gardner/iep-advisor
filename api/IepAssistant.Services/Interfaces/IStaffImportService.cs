using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Staff XLSX import: same pipeline as <see cref="IRosterImportService"/> over a <c>Staff</c> sheet.
/// Existing staff (matched by email, case-insensitive, within the district) get role/school/title
/// updates; unknown emails become staff invites on commit.
/// </summary>
public interface IStaffImportService
{
    Task<ServiceResult<byte[]>> GenerateTemplateAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult<ImportPreviewModel>> PreviewAsync(int userId, ImportUploadModel upload, CancellationToken ct = default);
    Task<ServiceResult<ImportResultModel>> CommitAsync(int userId, int batchId, bool commitValid, CancellationToken ct = default);
}
