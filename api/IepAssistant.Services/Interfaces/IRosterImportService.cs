using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Student roster XLSX import (plan 3, decisions 6–7). DistrictAdmin/SchoolAdmin only. Preview validates
/// every row and persists an <c>ImportBatch</c> + <c>ImportRow</c>s (parsed payload, never the file);
/// commit applies New/Updated rows exactly once. Formulas are never evaluated; <c>.xlsm</c> is rejected.
/// </summary>
public interface IRosterImportService
{
    Task<ServiceResult<byte[]>> GenerateTemplateAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult<ImportPreviewModel>> PreviewAsync(int userId, ImportUploadModel upload, CancellationToken ct = default);
    Task<ServiceResult<ImportResultModel>> CommitAsync(int userId, int batchId, bool commitValid, CancellationToken ct = default);
    Task<ServiceResult<List<ImportBatchModel>>> GetHistoryAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult<ImportPreviewModel>> GetBatchAsync(int userId, int batchId, CancellationToken ct = default);

    /// <summary>Batch header only (no rows) — e.g. to learn the kind before dispatching a commit.</summary>
    Task<ServiceResult<ImportBatchModel>> GetBatchSummaryAsync(int userId, int batchId, CancellationToken ct = default);
    Task<ServiceResult<byte[]>> BuildErrorWorkbookAsync(int userId, int batchId, CancellationToken ct = default);
}
