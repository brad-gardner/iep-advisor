namespace IepAssistant.Domain.Entities;

public class IepDocument : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public DateTime? IepDate { get; set; }
    public string Status { get; set; } = "uploaded";  // uploaded, processing, parsed, error
    public long FileSizeBytes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
}
