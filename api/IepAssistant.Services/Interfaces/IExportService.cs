using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>District and per-student data export (plan 7, decision 8). Enqueue methods create the
/// <c>ExportJob</c> row only; the controller enqueues the id onto the worker queue after commit
/// (mirrors <c>AuthoredDocumentPdfQueue</c>). <see cref="RunAsync"/> is the actual ZIP build, called only
/// by <c>ExportWorker</c>.</summary>
public interface IExportService
{
    /// <summary>DistrictAdmin only.</summary>
    Task<ServiceResult<ExportJobModel>> EnqueueDistrictExportAsync(int userId, CancellationToken ct = default);

    /// <summary>DistrictAdmin only, newest first.</summary>
    Task<ServiceResult<List<ExportJobModel>>> ListDistrictExportsAsync(int userId, CancellationToken ct = default);

    /// <summary>DistrictAdmin (district scope) or the requester/Viewer+ on the student (student scope).</summary>
    Task<ServiceResult<ExportJobModel>> GetStatusAsync(int userId, int jobId, CancellationToken ct = default);

    /// <summary>Same authorization as <see cref="GetStatusAsync"/>; fails unless the job is Completed. 15-minute expiry.</summary>
    Task<ServiceResult<string>> GetDownloadUrlAsync(int userId, int jobId, CancellationToken ct = default);

    /// <summary>Collaborator+ on the student.</summary>
    Task<ServiceResult<ExportJobModel>> EnqueueStudentExportAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Builds the ZIP for a Queued job and uploads it. Called only by <c>ExportWorker</c> — never from a request path.</summary>
    Task RunAsync(int jobId, CancellationToken ct = default);
}
