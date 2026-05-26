using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ProgressReportService : IProgressReportService
{
    private readonly IProgressReportRepository _repository;
    private readonly IIepDocumentRepository _iepRepository;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;

    public ProgressReportService(
        IProgressReportRepository repository,
        IIepDocumentRepository iepRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context)
    {
        _repository = repository;
        _iepRepository = iepRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
    }

    public async Task<IEnumerable<ProgressReportModel>> GetByIepIdAsync(int iepDocumentId, int userId, CancellationToken cancellationToken = default)
    {
        var iep = await _iepRepository.GetByIdAsync(iepDocumentId, cancellationToken);
        if (iep == null)
            return [];

        var role = await _accessService.GetRoleAsync(iep.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return [];

        var reports = await _repository.GetByIepDocumentIdAsync(iepDocumentId, cancellationToken);
        return reports.Select(MapToModel);
    }

    public async Task<ProgressReportModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdWithIepAsync(id, cancellationToken);
        if (report == null)
            return null;

        var role = await _accessService.GetRoleAsync(report.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        return MapToModel(report);
    }

    public async Task<ServiceResult<ProgressReportModel>> CreateAsync(int iepDocumentId, int userId, CreateProgressReportModel model, CancellationToken cancellationToken = default)
    {
        var iep = await _iepRepository.GetByIdAsync(iepDocumentId, cancellationToken);
        if (iep == null)
            return ServiceResult<ProgressReportModel>.FailureResult("IEP not found.");

        if (!await _accessService.HasMinimumRoleAsync(iep.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<ProgressReportModel>.FailureResult("IEP not found.");

        var entity = new ProgressReport
        {
            IepDocumentId = iepDocumentId,
            ChildProfileId = iep.ChildProfileId,
            ReportingPeriodStart = model.ReportingPeriodStart,
            ReportingPeriodEnd = model.ReportingPeriodEnd,
            Notes = model.Notes?.Trim(),
            Status = "created",
            CreatedById = userId,
            UpdatedById = userId
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProgressReportModel>.SuccessResult(MapToModel(entity), "Progress report created.");
    }

    public async Task<ServiceResult<ProgressReportModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdWithIepAsync(id, cancellationToken);
        if (report == null)
            return ServiceResult<ProgressReportModel>.FailureResult("Progress report not found.");

        if (!await _accessService.HasMinimumRoleAsync(report.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<ProgressReportModel>.FailureResult("Progress report not found.");

        if (report.Status == "processing")
            return ServiceResult<ProgressReportModel>.FailureResult("Cannot replace file while document is being processed.");

        if (!string.IsNullOrEmpty(report.BlobUri))
            await _blobStorage.DeleteAsync(report.BlobUri, cancellationToken);

        var blobPath = $"children/{report.ChildProfileId}/progress-reports/{Guid.NewGuid()}/{fileName}";
        await _blobStorage.UploadAsync(blobPath, fileStream, "application/pdf", cancellationToken);

        report.FileName = fileName;
        report.BlobUri = blobPath;
        report.FileSizeBytes = fileSize;
        report.UploadDate = DateTime.UtcNow;
        report.Status = "uploaded";
        report.UpdatedById = userId;

        _repository.Update(report);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProgressReportModel>.SuccessResult(MapToModel(report), "File attached.");
    }

    public async Task<ServiceResult> UpdateMetadataAsync(int id, int userId, CreateProgressReportModel model, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdWithIepAsync(id, cancellationToken);
        if (report == null)
            return ServiceResult.FailureResult("Progress report not found.");

        if (!await _accessService.HasMinimumRoleAsync(report.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Progress report not found.");

        if (model.ReportingPeriodStart.HasValue)
            report.ReportingPeriodStart = model.ReportingPeriodStart;
        if (model.ReportingPeriodEnd.HasValue)
            report.ReportingPeriodEnd = model.ReportingPeriodEnd;
        if (model.Notes != null)
            report.Notes = model.Notes.Trim();

        report.UpdatedById = userId;
        _repository.Update(report);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Metadata updated.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdWithIepAsync(id, cancellationToken);
        if (report == null)
            return ServiceResult.FailureResult("Progress report not found.");

        if (!await _accessService.HasMinimumRoleAsync(report.ChildProfileId, userId, AccessRole.Owner, cancellationToken))
            return ServiceResult.FailureResult("Progress report not found.");

        if (!string.IsNullOrEmpty(report.BlobUri))
            await _blobStorage.DeleteAsync(report.BlobUri, cancellationToken);

        report.IsActive = false;
        report.UpdatedById = userId;
        _repository.Update(report);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Progress report deleted.");
    }

    public async Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdWithIepAsync(id, cancellationToken);
        if (report == null)
            return null;

        var role = await _accessService.GetRoleAsync(report.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        if (string.IsNullOrEmpty(report.BlobUri))
            return null;

        return await _blobStorage.GetDownloadUrlAsync(report.BlobUri);
    }

    private static ProgressReportModel MapToModel(ProgressReport entity) => new()
    {
        Id = entity.Id,
        IepDocumentId = entity.IepDocumentId,
        ChildProfileId = entity.ChildProfileId,
        FileName = entity.FileName,
        UploadDate = entity.UploadDate,
        ReportingPeriodStart = entity.ReportingPeriodStart,
        ReportingPeriodEnd = entity.ReportingPeriodEnd,
        Notes = entity.Notes,
        Status = entity.Status,
        ErrorMessage = entity.ErrorMessage,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt
    };
}
