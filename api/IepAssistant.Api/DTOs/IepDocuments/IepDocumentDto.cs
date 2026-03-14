namespace IepAssistant.Api.DTOs.IepDocuments;

public class IepDocumentDto
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
    public DateTime CreatedAt { get; set; }
}
