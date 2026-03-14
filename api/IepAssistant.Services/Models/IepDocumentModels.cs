namespace IepAssistant.Services.Models;

public class IepDocumentModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public DateTime? IepDate { get; set; }
    public string? MeetingType { get; set; }
    public string? Attendees { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateIepDocumentModel
{
    public DateTime IepDate { get; set; }
    public string MeetingType { get; set; } = string.Empty;
    public string? Attendees { get; set; }
    public string? Notes { get; set; }
}

public class UpdateIepMetadataModel
{
    public DateTime? IepDate { get; set; }
    public string? MeetingType { get; set; }
    public string? Attendees { get; set; }
    public string? Notes { get; set; }
}
