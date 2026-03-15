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

    private static readonly HashSet<string> ValidMeetingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "initial", "annual_review", "amendment", "reevaluation"
    };

    public IepDocumentService(
        IIepDocumentRepository documentRepository,
        IChildProfileRepository childProfileRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context)
    {
        _documentRepository = documentRepository;
        _childProfileRepository = childProfileRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
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
            return ServiceResult<IepDocumentModel>.FailureResult("Child profile not found.");

        if (!ValidMeetingTypes.Contains(model.MeetingType))
            return ServiceResult<IepDocumentModel>.FailureResult("Invalid meeting type. Must be: initial, annual_review, amendment, or reevaluation.");

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

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(entity), "IEP created successfully.");
    }

    public async Task<ServiceResult<IepDocumentModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult<IepDocumentModel>.FailureResult("Document not found.");

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<IepDocumentModel>.FailureResult("Document not found.");

        if (document.Status == "processing")
            return ServiceResult<IepDocumentModel>.FailureResult("Cannot replace file while document is being processed. Please wait for processing to complete.");

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

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(document), "File attached successfully.");
    }

    public async Task<ServiceResult> UpdateMetadataAsync(int id, int userId, UpdateIepMetadataModel model, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult.FailureResult("Document not found.");

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Document not found.");

        if (model.IepDate.HasValue)
            document.IepDate = model.IepDate.Value;

        if (model.MeetingType != null)
        {
            if (!ValidMeetingTypes.Contains(model.MeetingType))
                return ServiceResult.FailureResult("Invalid meeting type.");
            document.MeetingType = model.MeetingType.ToLowerInvariant();
        }

        if (model.Attendees != null)
            document.Attendees = model.Attendees.Trim();

        if (model.Notes != null)
            document.Notes = model.Notes.Trim();

        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Metadata updated successfully.");
    }

    public async Task<ServiceResult<IepDocumentModel>> UploadAsync(int childProfileId, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<IepDocumentModel>.FailureResult("Child profile not found.");

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

        return ServiceResult<IepDocumentModel>.SuccessResult(MapToModel(entity), "IEP document uploaded successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult.FailureResult("Document not found.");

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Owner, cancellationToken))
            return ServiceResult.FailureResult("Document not found.");

        if (!string.IsNullOrEmpty(document.BlobUri))
        {
            await _blobStorage.DeleteAsync(document.BlobUri, cancellationToken);
        }

        document.IsActive = false;
        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Document deleted successfully.");
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
