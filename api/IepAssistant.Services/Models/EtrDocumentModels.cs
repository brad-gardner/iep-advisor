namespace IepAssistant.Services.Models;

public class EtrDocumentModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string? FileName { get; set; }
    public DateTime UploadDate { get; set; }
    public DateTime? EvaluationDate { get; set; }
    public string? EvaluationType { get; set; }
    public string DocumentState { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEtrDocumentModel
{
    public DateTime EvaluationDate { get; set; }
    public string EvaluationType { get; set; } = string.Empty;
    public string DocumentState { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpdateEtrMetadataModel
{
    public DateTime? EvaluationDate { get; set; }
    public string? EvaluationType { get; set; }
    public string? DocumentState { get; set; }
    public string? Notes { get; set; }
}
