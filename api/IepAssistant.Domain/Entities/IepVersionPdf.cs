namespace IepAssistant.Domain.Entities;

/// <summary>
/// The rendered-PDF tracking row for an <see cref="IepVersion"/> (one-to-one). This is the ONE
/// mutable version-related table: the P5b render worker updates RenderStatus / BlobUri / Checksum
/// after rendering. The immutability interceptor intentionally excludes this entity.
/// </summary>
public class IepVersionPdf : BaseEntity, IAuditableEntity
{
    public int IepVersionId { get; set; }

    /// <summary>
    /// Multilingual plan (2026-10-06) phase 7: "en"/"es" (see <c>SupportedLanguages</c>), nullable for
    /// backward compatibility — every row created before this column existed is NULL and is treated as
    /// English everywhere this is queried (never re-backfilled, exactly like <c>MeetingBrief.Language</c>/
    /// the AI-artifact Language columns). One row per (IepVersionId, Language): the unique index on
    /// <see cref="IepVersionId"/> alone became a composite <c>(IepVersionId, Language)</c> index so an
    /// English and a Spanish render of the same version coexist as separate rows instead of overwriting
    /// each other.
    /// </summary>
    public string? Language { get; set; }

    public string? BlobUri { get; set; }
    public string? Checksum { get; set; }
    public DateTime? RenderedAt { get; set; }
    public PdfRenderStatus RenderStatus { get; set; } = PdfRenderStatus.Pending;
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
