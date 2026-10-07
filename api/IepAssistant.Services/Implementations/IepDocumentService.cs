using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class IepDocumentService : IIepDocumentService
{
    private readonly IIepDocumentRepository _documentRepository;
    private readonly IChildProfileRepository _childProfileRepository;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<Messages> _localizer;

    private static readonly HashSet<string> ValidMeetingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "initial", "annual_review", "amendment", "reevaluation"
    };

    public IepDocumentService(
        IIepDocumentRepository documentRepository,
        IChildProfileRepository childProfileRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context,
        IStringLocalizer<Messages> localizer)
    {
        _documentRepository = documentRepository;
        _childProfileRepository = childProfileRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
        _localizer = localizer;
    }

    public async Task<IEnumerable<IepDocumentModel>> GetByChildIdAsync(int childProfileId, int userId, CancellationToken cancellationToken = default)
    {
        var role = await _accessService.GetRoleAsync(childProfileId, userId, cancellationToken);
        if (role == null)
            return [];

        var documents = await _documentRepository.GetByChildProfileIdAsync(childProfileId, cancellationToken);
        return documents.Select(MapToModel);
    }

    public async Task<IepDocumentModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return null;

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        return MapToModel(document);
    }

    public async Task<ServiceResult<IepDocumentModel>> CreateAsync(int childProfileId, int userId, CreateIepDocumentModel model, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Children.NotFound"]);

        if (!ValidMeetingTypes.Contains(model.MeetingType))
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.Validation, _localizer["IepDocuments.InvalidMeetingTypeDetailed"]);

        var entity = new IepDocument
        {
            ChildProfileId = childProfileId,
            IepDate = model.IepDate,
            MeetingType = model.MeetingType.ToLowerInvariant(),
            Attendees = model.Attendees?.Trim(),
            Notes = model.Notes?.Trim(),
            Status = "created",
            CreatedById = userId,
            UpdatedById = userId
        };

        await _documentRepository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await EnsureCurrentIepAsync(childProfileId, entity.Id, cancellationToken);

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(entity), _localizer["IepDocuments.CreatedSuccessfully"]);
    }

    public async Task<ServiceResult<IepDocumentModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (document.Status == "processing")
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.Conflict, _localizer["Documents.ProcessingInProgress"]);

        // Delete existing blob if replacing
        if (!string.IsNullOrEmpty(document.BlobUri))
        {
            await _blobStorage.DeleteAsync(document.BlobUri, cancellationToken);
        }

        var blobPath = $"children/{document.ChildProfileId}/{Guid.NewGuid()}/{fileName}";
        await _blobStorage.UploadAsync(blobPath, fileStream, "application/pdf", cancellationToken);

        document.FileName = fileName;
        document.BlobUri = blobPath;
        document.FileSizeBytes = fileSize;
        document.UploadDate = DateTime.UtcNow;
        document.Status = "uploaded";
        document.UpdatedById = userId;

        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(document), _localizer["Documents.FileAttachedSuccessfully"]);
    }

    public async Task<ServiceResult> UpdateMetadataAsync(int id, int userId, UpdateIepMetadataModel model, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (model.IepDate.HasValue)
            document.IepDate = model.IepDate.Value;

        if (model.MeetingType != null)
        {
            if (!ValidMeetingTypes.Contains(model.MeetingType))
                return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["IepDocuments.InvalidMeetingType"]);
            document.MeetingType = model.MeetingType.ToLowerInvariant();
        }

        if (model.Attendees != null)
            document.Attendees = model.Attendees.Trim();

        if (model.Notes != null)
            document.Notes = model.Notes.Trim();

        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["Documents.MetadataUpdatedSuccessfully"]);
    }

    public async Task<ServiceResult<IepDocumentModel>> UploadAsync(int childProfileId, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<IepDocumentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Children.NotFound"]);

        var blobPath = $"children/{childProfileId}/{Guid.NewGuid()}/{fileName}";
        await _blobStorage.UploadAsync(blobPath, fileStream, "application/pdf", cancellationToken);

        var entity = new IepDocument
        {
            ChildProfileId = childProfileId,
            FileName = fileName,
            BlobUri = blobPath,
            FileSizeBytes = fileSize,
            Status = "uploaded",
            CreatedById = userId,
            UpdatedById = userId
        };

        await _documentRepository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await EnsureCurrentIepAsync(childProfileId, entity.Id, cancellationToken);

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(entity), _localizer["IepDocuments.UploadedSuccessfully"]);
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Owner, cancellationToken))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!string.IsNullOrEmpty(document.BlobUri))
        {
            await _blobStorage.DeleteAsync(document.BlobUri, cancellationToken);
        }

        var child = await _childProfileRepository.GetByIdAsync(document.ChildProfileId, cancellationToken);
        if (child != null && child.CurrentIepDocumentId == id)
        {
            child.CurrentIepDocumentId = null;
            _childProfileRepository.Update(child);
        }

        document.IsActive = false;
        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["Documents.DeletedSuccessfully"]);
    }

    public async Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return null;

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        if (string.IsNullOrEmpty(document.BlobUri))
            return null;

        return await _blobStorage.GetDownloadUrlAsync(document.BlobUri);
    }

    private async Task EnsureCurrentIepAsync(int childProfileId, int newIepId, CancellationToken cancellationToken)
    {
        var child = await _childProfileRepository.GetByIdAsync(childProfileId, cancellationToken);
        if (child == null || child.CurrentIepDocumentId.HasValue)
            return;

        child.CurrentIepDocumentId = newIepId;
        _childProfileRepository.Update(child);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IepDocumentModel MapToModel(IepDocument entity) => new()
    {
        Id = entity.Id,
        ChildProfileId = entity.ChildProfileId,
        FileName = entity.FileName,
        UploadDate = entity.UploadDate,
        IepDate = entity.IepDate,
        MeetingType = entity.MeetingType,
        Attendees = entity.Attendees,
        Notes = entity.Notes,
        Status = entity.Status,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt
    };
}
