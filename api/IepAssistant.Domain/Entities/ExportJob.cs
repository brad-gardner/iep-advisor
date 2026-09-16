namespace IepAssistant.Domain.Entities;

/// <summary>
/// A background export job (plan 7, decision 8): builds a ZIP of a district's or one student's finalized
/// PDFs, signed artifacts, values, goals, meetings, contact attempts, staff-visible responses, and an
/// audit extract, uploaded via <c>IBlobStorageService</c> and downloaded through a short-lived URL.
/// Claimed and processed by <c>ExportWorker</c> (BackgroundService), mirroring the
/// <c>AuthoredDocumentPdfWorker</c> claim pattern.
/// </summary>
public class ExportJob : BaseEntity, IAuditableEntity
{
    public ExportScope Scope { get; set; }
    public int DistrictId { get; set; }

    /// <summary>Set only when <see cref="Scope"/> is <see cref="ExportScope.Student"/>.</summary>
    public int? SchoolStudentId { get; set; }

    public int RequestedByUserId { get; set; }
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Queued;

    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? BlobPath { get; set; }
    public long? SizeBytes { get; set; }
    public string? Error { get; set; }

    public int StudentCount { get; set; }
    public int FileCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public District District { get; set; } = null!;
    public SchoolStudent? SchoolStudent { get; set; }
}
