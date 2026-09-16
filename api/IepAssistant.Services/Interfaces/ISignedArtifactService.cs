using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Print/sign artifact upload and status (plan 7, decision 4). Typed-name e-sign is not claimed
/// — an artifact is a scanned/uploaded copy of a printed, physically-signed document.</summary>
public interface ISignedArtifactService
{
    /// <summary>Collaborator+. Sets the version's SignatureStatus to the uploader-declared value.</summary>
    Task<ServiceResult<SignedArtifactModel>> UploadAsync(int userId, int versionId, UploadSignedArtifactModel model, CancellationToken ct = default);

    /// <summary>Viewer+ (educator-with-access or linked parent), newest first.</summary>
    Task<ServiceResult<List<SignedArtifactModel>>> ListAsync(int userId, int versionId, CancellationToken ct = default);

    /// <summary>Mints a short-lived download URL and records a FERPA Export audit entry.</summary>
    Task<ServiceResult<string>> GetDownloadUrlAsync(int userId, int artifactId, CancellationToken ct = default);
}
