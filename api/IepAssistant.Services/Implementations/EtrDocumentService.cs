using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EtrDocumentService : IEtrDocumentService
{
    private readonly IEtrDocumentRepository _documentRepository;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;

    private static readonly HashSet<string> ValidEvaluationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "initial", "reevaluation", "transfer", "other"
    };

    private static readonly HashSet<string> ValidDocumentStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft", "final"
    };

    public EtrDocumentService(
        IEtrDocumentRepository documentRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context)
    {
        _documentRepository = documentRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
    }

    public async Task<IEnumerable<EtrDocumentModel>> GetByChildIdAsync(int childProfileId, int userId, CancellationToken cancellationToken = default)
    {
        var role = await _accessService.GetRoleAsync(childProfileId, userId, cancellationToken);
        if (role == null)
            return [];

        var documents = await _documentRepository.GetByChildProfileIdAsync(childProfileId, cancellationToken);
        return documents.Select(MapToModel);
    }

    public async Task<IEnumerable<EtrDocumentListItemModel>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetAllByUserAsync(userId, cancellationToken);
        return documents.Select(MapToListItemModel);
    }

    public async Task<EtrDocumentModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return null;

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        return MapToModel(document);
    }

    public async Task<ServiceResult<EtrDocumentModel>> CreateAsync(int childProfileId, int userId, CreateEtrDocumentModel model, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<EtrDocumentModel>.FailureResult("Child profile not found.");

        if (string.IsNullOrWhiteSpace(model.EvaluationType) || !ValidEvaluationTypes.Contains(model.EvaluationType))
            return ServiceResult<EtrDocumentModel>.FailureResult("Invalid evaluation type. Must be: initial, reevaluation, transfer, or other.");

        if (string.IsNullOrWhiteSpace(model.DocumentState) || !ValidDocumentStates.Contains(model.DocumentState))
            return ServiceResult<EtrDocumentModel>.FailureResult("Invalid document state. Must be: draft or final.");

        var entity = new EtrDocument
        {
            ChildProfileId = childProfileId,
            EvaluationDate = model.EvaluationDate,
            EvaluationType = model.EvaluationType.ToLowerInvariant(),
            DocumentState = model.DocumentState.ToLowerInvariant(),
            Notes = model.Notes?.Trim(),
            Status = "created",
            CreatedById = userId,
            UpdatedById = userId
        };

        await _documentRepository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<EtrDocumentModel>.SuccessResult(MapToModel(entity), "ETR created successfully.");
    }

    public async Task<ServiceResult> UpdateMetadataAsync(int id, int userId, UpdateEtrMetadataModel model, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult.FailureResult("Document not found.");

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Document not found.");

        if (model.EvaluationDate.HasValue)
            document.EvaluationDate = model.EvaluationDate.Value;

        if (model.EvaluationType != null)
        {
            if (!ValidEvaluationTypes.Contains(model.EvaluationType))
                return ServiceResult.FailureResult("Invalid evaluation type.");
            document.EvaluationType = model.EvaluationType.ToLowerInvariant();
        }

        if (model.DocumentState != null)
        {
            if (!ValidDocumentStates.Contains(model.DocumentState))
                return ServiceResult.FailureResult("Invalid document state.");
            document.DocumentState = model.DocumentState.ToLowerInvariant();
        }

        if (model.Notes != null)
            document.Notes = model.Notes.Trim();

        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Metadata updated successfully.");
    }

    public async Task<ServiceResult<EtrDocumentModel>> AttachFileAsync(int id, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null)
            return ServiceResult<EtrDocumentModel>.FailureResult("Document not found.");

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<EtrDocumentModel>.FailureResult("Document not found.");

        if (document.Status == "processing")
            return ServiceResult<EtrDocumentModel>.FailureResult("Cannot replace file while document is being processed. Please wait for processing to complete.");

        if (!string.IsNullOrEmpty(document.BlobUri))
        {
            await _blobStorage.DeleteAsync(document.BlobUri, cancellationToken);
        }

        var blobPath = $"etrs/{document.ChildProfileId}/{Guid.NewGuid()}/{fileName}";
        await _blobStorage.UploadAsync(blobPath, fileStream, "application/pdf", cancellationToken);

        document.FileName = fileName;
        document.BlobUri = blobPath;
        document.FileSizeBytes = fileSize;
        document.UploadDate = DateTime.UtcNow;
        document.Status = "uploaded";
        document.UpdatedById = userId;

        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<EtrDocumentModel>.SuccessResult(MapToModel(document), "File attached successfully.");
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

    private static EtrDocumentListItemModel MapToListItemModel(EtrDocument entity) => new()
    {
        Id = entity.Id,
        ChildProfileId = entity.ChildProfileId,
        FileName = entity.FileName,
        UploadDate = entity.UploadDate,
        EvaluationDate = entity.EvaluationDate,
        EvaluationType = entity.EvaluationType,
        DocumentState = entity.DocumentState,
        Notes = entity.Notes,
        Status = entity.Status,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt,
        ChildId = entity.ChildProfileId,
        ChildFirstName = entity.ChildProfile?.FirstName ?? string.Empty,
        ChildLastName = entity.ChildProfile?.LastName
    };

    private static EtrDocumentModel MapToModel(EtrDocument entity) => new()
    {
        Id = entity.Id,
        ChildProfileId = entity.ChildProfileId,
        FileName = entity.FileName,
        UploadDate = entity.UploadDate,
        EvaluationDate = entity.EvaluationDate,
        EvaluationType = entity.EvaluationType,
        DocumentState = entity.DocumentState,
        Notes = entity.Notes,
        Status = entity.Status,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt
    };
}
