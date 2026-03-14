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
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;

    public IepDocumentService(
        IIepDocumentRepository documentRepository,
        IChildProfileRepository childProfileRepository,
        IBlobStorageService blobStorage,
        ApplicationDbContext context)
    {
        _documentRepository = documentRepository;
        _childProfileRepository = childProfileRepository;
        _blobStorage = blobStorage;
        _context = context;
    }

    public async Task<IEnumerable<IepDocumentModel>> GetByChildIdAsync(int childProfileId, int userId, CancellationToken cancellationToken = default)
    {
        var child = await _childProfileRepository.GetByIdForUserAsync(childProfileId, userId, cancellationToken);
        if (child == null)
            return [];

        var documents = await _documentRepository.GetByChildProfileIdAsync(childProfileId, cancellationToken);
        return documents.Select(MapToModel);
    }

    public async Task<IepDocumentModel?> GetByIdAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null || document.ChildProfile.UserId != userId)
            return null;

        return MapToModel(document);
    }

    public async Task<ServiceResult<IepDocumentModel>> UploadAsync(int childProfileId, int userId, string fileName, Stream fileStream, long fileSize, CancellationToken cancellationToken = default)
    {
        var child = await _childProfileRepository.GetByIdForUserAsync(childProfileId, userId, cancellationToken);
        if (child == null)
            return ServiceResult<IepDocumentModel>.FailureResult("Child profile not found.");

        var blobPath = $"users/{userId}/children/{childProfileId}/{Guid.NewGuid()}/{fileName}";
        var blobUri = await _blobStorage.UploadAsync(blobPath, fileStream, "application/pdf", cancellationToken);

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
        if (document == null || document.ChildProfile.UserId != userId)
            return ServiceResult.FailureResult("Document not found.");

        await _blobStorage.DeleteAsync(document.BlobUri, cancellationToken);

        document.IsActive = false;
        document.UpdatedById = userId;
        _documentRepository.Update(document);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Document deleted successfully.");
    }

    public async Task<string?> GetDownloadUrlAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(id, cancellationToken);
        if (document == null || document.ChildProfile.UserId != userId)
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
        Status = entity.Status,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt
    };
}
