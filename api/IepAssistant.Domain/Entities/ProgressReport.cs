namespace IepAssistant.Domain.Entities;

public class ProgressReport : BaseEntity, IAuditableEntity
{
    public int IepDocumentId { get; set; }
    public int ChildProfileId { get; set; }
    public string? FileName { get; set; }
    public string? BlobUri { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReportingPeriodStart { get; set; }
    public DateTime? ReportingPeriodEnd { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "created"; // created, uploaded, processing, parsed, error
    public string? RawText { get; set; } // extracted text after parsing
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public IepDocument IepDocument { get; set; } = null!;
    public ChildProfile ChildProfile { get; set; } = null!;
}
