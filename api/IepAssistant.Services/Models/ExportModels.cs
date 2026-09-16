using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class ExportJobModel
{
    public int Id { get; set; }
    public ExportScope Scope { get; set; }
    public int DistrictId { get; set; }
    public int? SchoolStudentId { get; set; }
    public string? StudentName { get; set; }
    public int RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public ExportJobStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? SizeBytes { get; set; }
    public string? Error { get; set; }
    public int StudentCount { get; set; }
    public int FileCount { get; set; }
}

/// <summary>One entry of an export manifest's <c>files</c> array — every file in the archive, with its sha256 for tamper evidence.</summary>
public class ExportManifestFileModel
{
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Bytes { get; set; }
}
