namespace IepAssistant.Domain.Entities;

/// <summary>
/// The rendered-PDF tracking row for an <see cref="IepVersion"/> (one-to-one). This is the ONE
/// mutable version-related table: the P5b render worker updates RenderStatus / BlobUri / Checksum
/// after rendering. The immutability interceptor intentionally excludes this entity.
/// </summary>
public class IepVersionPdf : BaseEntity, IAuditableEntity
{
    public int IepVersionId { get; set; }
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
