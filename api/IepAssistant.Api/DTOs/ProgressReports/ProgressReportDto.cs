namespace IepAssistant.Api.DTOs.ProgressReports;

public class ProgressReportDto
{
    public int Id { get; set; }
    public int IepDocumentId { get; set; }
    public int ChildProfileId { get; set; }
    public string? FileName { get; set; }
    public DateTime UploadDate { get; set; }
    public DateTime? ReportingPeriodStart { get; set; }
    public DateTime? ReportingPeriodEnd { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProgressReportRequest
{
    public DateTime? ReportingPeriodStart { get; set; }
    public DateTime? ReportingPeriodEnd { get; set; }
    public string? Notes { get; set; }
}
