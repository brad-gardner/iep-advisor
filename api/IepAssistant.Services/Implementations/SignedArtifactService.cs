using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Print/sign artifact upload + status (see <see cref="ISignedArtifactService"/>, plan 7, decision 4).
/// Uploading a signed copy moves the (otherwise-immutable) version's
/// <see cref="AuthoredDocumentVersion.SignatureStatus"/> — the interceptor's one carved-out mutable
/// column — to the uploader-declared value.
/// </summary>
public class SignedArtifactService : ISignedArtifactService
{
    private const string VersionNotFoundMessage = "Document version not found.";
    private const string ArtifactNotFoundMessage = "Signed artifact not found.";
    private const string PermissionMessage = "You do not have permission to access this document version.";
    private const string PdfOnlyMessage = "The signed artifact must be a PDF.";
    private const string TooLargeMessage = "The signed artifact must be 20 MB or smaller.";
    private const string InvalidStatusMessage = "signatureStatus must be PartiallySigned or Signed.";
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blob;
    private readonly IAuditLogger _audit;

    public SignedArtifactService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        IBlobStorageService blob,
        IAuditLogger audit)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _blob = blob;
        _audit = audit;
    }

    public async Task<ServiceResult<SignedArtifactModel>> UploadAsync(int userId, int versionId, UploadSignedArtifactModel model, CancellationToken ct = default)
    {
        if (model.SignatureStatus is not (SignatureStatus.PartiallySigned or SignatureStatus.Signed))
            return ServiceResult<SignedArtifactModel>.FailureResult(InvalidStatusMessage);
        if (!string.IsNullOrWhiteSpace(model.ContentType) && !string.Equals(model.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return ServiceResult<SignedArtifactModel>.FailureResult(PdfOnlyMessage);
        if (model.SizeBytes > MaxFileBytes)
            return ServiceResult<SignedArtifactModel>.FailureResult(TooLargeMessage);

        var version = await _context.AuthoredDocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version == null)
            return ServiceResult<SignedArtifactModel>.FailureResult(VersionNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, version.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<SignedArtifactModel>.FailureResult(PermissionMessage);

        if (!await PdfUploadGuard.LooksLikePdfAsync(model.FileStream, ct))
            return ServiceResult<SignedArtifactModel>.FailureResult(PdfOnlyMessage);

        var fileName = PdfUploadGuard.SafeFileName(model.FileName, "signed.pdf");
        var blobPath = $"signed-artifacts/{versionId}/{Guid.NewGuid():N}.pdf";
        await _blob.UploadAsync(blobPath, model.FileStream, "application/pdf", ct);

        var now = DateTime.UtcNow;
        var artifact = new SignedArtifact
        {
            AuthoredDocumentVersionId = versionId,
            BlobPath = blobPath,
            FileName = fileName,
            ContentType = "application/pdf",
            SizeBytes = model.SizeBytes,
            UploadedByUserId = userId,
            UploadedAt = now,
            SignerSummary = string.IsNullOrWhiteSpace(model.SignerSummary) ? null : model.SignerSummary.Trim(),
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.SignedArtifacts.AddAsync(artifact, ct);

        // The ONE mutable column on AuthoredDocumentVersion (see ImmutableVersionInterceptor).
        version.SignatureStatus = model.SignatureStatus;
        version.UpdatedById = userId;

        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "AuthoredDocumentVersion", versionId);

        return ServiceResult<SignedArtifactModel>.SuccessResult(await MapAsync(artifact.Id, ct));
    }

    public async Task<ServiceResult<List<SignedArtifactModel>>> ListAsync(int userId, int versionId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<List<SignedArtifactModel>>.FailureResult(VersionNotFoundMessage);
        if (!await CanReadStudentAsync(userId, header.SchoolStudentId, ct))
            return ServiceResult<List<SignedArtifactModel>>.FailureResult(PermissionMessage);

        var rows = await MapQuery(_context.SignedArtifacts.AsNoTracking().Where(a => a.AuthoredDocumentVersionId == versionId))
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);

        return ServiceResult<List<SignedArtifactModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<string>> GetDownloadUrlAsync(int userId, int artifactId, CancellationToken ct = default)
    {
        var artifact = await _context.SignedArtifacts.AsNoTracking()
            .Where(a => a.Id == artifactId)
            .Select(a => new { a.BlobPath, a.AuthoredDocumentVersionId })
            .FirstOrDefaultAsync(ct);
        if (artifact == null)
            return ServiceResult<string>.FailureResult(ArtifactNotFoundMessage);

        var header = await LoadVersionHeaderAsync(artifact.AuthoredDocumentVersionId, ct);
        if (header == null)
            return ServiceResult<string>.FailureResult(VersionNotFoundMessage);
        if (!await CanReadStudentAsync(userId, header.SchoolStudentId, ct))
            return ServiceResult<string>.FailureResult(PermissionMessage);

        var url = await _blob.GetDownloadUrlAsync(artifact.BlobPath);
        _audit.Record(AuditAction.Export, userId, "SignedArtifact", artifactId);
        return ServiceResult<string>.SuccessResult(url);
    }

    // ---------------------------------------------------------------- Access + mapping

    private sealed record VersionHeader(int SchoolStudentId);

    private async Task<VersionHeader?> LoadVersionHeaderAsync(int versionId, CancellationToken ct) =>
        await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new VersionHeader(v.SchoolStudentId))
            .FirstOrDefaultAsync(ct);

    /// <summary>Educator with org access (Viewer+) OR a linked parent — the same rule the version read paths use.</summary>
    private async Task<bool> CanReadStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        if (await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return true;
        return await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, userId, studentId, AccessRole.Viewer, ct) != null;
    }

    private static IQueryable<SignedArtifactModel> MapQuery(IQueryable<SignedArtifact> query) =>
        query.Select(a => new SignedArtifactModel
        {
            Id = a.Id,
            AuthoredDocumentVersionId = a.AuthoredDocumentVersionId,
            FileName = a.FileName,
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes,
            UploadedByUserId = a.UploadedByUserId,
            UploadedByName = null,
            UploadedAt = a.UploadedAt,
            SignerSummary = a.SignerSummary
        });

    private async Task<SignedArtifactModel> MapAsync(int artifactId, CancellationToken ct)
    {
        var model = await MapQuery(_context.SignedArtifacts.AsNoTracking().Where(a => a.Id == artifactId)).FirstAsync(ct);
        model.UploadedByName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == model.UploadedByUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync(ct);
        return model;
    }
}
