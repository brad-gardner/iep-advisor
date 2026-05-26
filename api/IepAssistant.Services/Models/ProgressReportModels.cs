namespace IepAssistant.Services.Models;

public class ProgressReportModel
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

public class CreateProgressReportModel
{
    public DateTime? ReportingPeriodStart { get; set; }
    public DateTime? ReportingPeriodEnd { get; set; }
    public string? Notes { get; set; }
}
