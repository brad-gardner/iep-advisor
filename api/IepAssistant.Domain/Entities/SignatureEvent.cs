namespace IepAssistant.Domain.Entities;

/// <summary>How a <see cref="SignatureEvent"/> was captured. <see cref="Typed"/> is reserved for a future
/// in-app e-sign feature and is never created today — plan 7 explicitly does not claim typed-name e-sign.</summary>
public enum SignatureMethod
{
    Wet,
    Typed
}

/// <summary>
/// STUB table for a future in-app e-signature feature (plan 7, decision 4) — no service, no endpoint, no
/// UI reads or writes this today. Kept only so the schema shape exists ahead of that work; do not wire
/// this up as part of the print/sign flow, which uses <see cref="SignedArtifact"/> instead.
/// </summary>
public class SignatureEvent : BaseEntity, IAuditableEntity
{
    public int AuthoredDocumentVersionId { get; set; }

    public string SignerName { get; set; } = string.Empty;
    public string? SignerRole { get; set; }
    public DateTime SignedAt { get; set; }
    public SignatureMethod Method { get; set; } = SignatureMethod.Wet;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public AuthoredDocumentVersion AuthoredDocumentVersion { get; set; } = null!;
}
