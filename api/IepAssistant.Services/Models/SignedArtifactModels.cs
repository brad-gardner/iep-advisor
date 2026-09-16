using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class SignedArtifactModel
{
    public int Id { get; set; }
    public int AuthoredDocumentVersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? SignerSummary { get; set; }
}

/// <summary>Upload input. <see cref="SignatureStatus"/> must be PartiallySigned or Signed — the uploader
/// declares the resulting version-level status; Unsigned is not a valid target of an upload.</summary>
public class UploadSignedArtifactModel
{
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? SignerSummary { get; set; }
    public SignatureStatus SignatureStatus { get; set; }
}
