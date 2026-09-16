using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Export;

public class ExportJobDto
{
    public int Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public int? SchoolStudentId { get; set; }
    public string? StudentName { get; set; }
    public int RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? SizeBytes { get; set; }
    public string? Error { get; set; }
    public int StudentCount { get; set; }
    public int FileCount { get; set; }
}

/// <summary>The queued job's id, returned 202 Accepted from both enqueue endpoints.</summary>
public class ExportJobIdDto
{
    public int JobId { get; set; }
}

internal static class ExportMappers
{
    public static ExportJobDto MapJob(ExportJobModel m) => new()
    {
        Id = m.Id,
        Scope = m.Scope.ToString(),
        DistrictId = m.DistrictId,
        SchoolStudentId = m.SchoolStudentId,
        StudentName = m.StudentName,
        RequestedByUserId = m.RequestedByUserId,
        RequestedByName = m.RequestedByName,
        Status = m.Status.ToString(),
        RequestedAt = m.RequestedAt,
        StartedAt = m.StartedAt,
        CompletedAt = m.CompletedAt,
        SizeBytes = m.SizeBytes,
        Error = m.Error,
        StudentCount = m.StudentCount,
        FileCount = m.FileCount
    };
}
