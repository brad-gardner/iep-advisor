namespace IepAssistant.Domain.Entities;

public class IepDocument : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public string? FileName { get; set; }
    public string? BlobUri { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public DateTime? IepDate { get; set; }
    public string? MeetingType { get; set; }  // initial, annual_review, amendment, reevaluation
    public string? Attendees { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "created";  // created, uploaded, processing, parsed, error
    public long FileSizeBytes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
}
