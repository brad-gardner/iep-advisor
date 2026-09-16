namespace IepAssistant.Domain.Entities;

/// <summary>
/// An uploaded scan/photo of a print-and-signed copy of a finalized <see cref="AuthoredDocumentVersion"/>
/// (plan 7, decision 4 — print/sign, NOT in-app e-sign). Uploading one moves the version's
/// <see cref="AuthoredDocumentVersion.SignatureStatus"/> to PartiallySigned or Signed, as declared by the
/// uploader. Immutable once uploaded (a correction is a new artifact, never an edit).
/// </summary>
public class SignedArtifact : BaseEntity, IAuditableEntity
{
    public int AuthoredDocumentVersionId { get; set; }

    public string BlobPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>Free-text summary of who signed (e.g. "Parent + case manager"), never structured signer identity.</summary>
    public string? SignerSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public AuthoredDocumentVersion AuthoredDocumentVersion { get; set; } = null!;
}
