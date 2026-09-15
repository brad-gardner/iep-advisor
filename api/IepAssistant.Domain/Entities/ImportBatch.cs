namespace IepAssistant.Domain.Entities;

public enum ImportKind
{
    Students,
    Staff
}

public enum ImportBatchStatus
{
    Previewed,

    /// <summary>Transient: a commit is in flight (claimed with a conditional update so a second commit is refused).</summary>
    Committing,
    Committed,
    Discarded
}

/// <summary>
/// One uploaded roster/staff workbook (plan 3, decision 7). Preview parses and validates every row,
/// persists the parsed payload per <see cref="ImportRow"/> and the outcome counts, and the batch can then
/// be committed exactly once (<see cref="Status"/> Previewed → Committed). The uploaded file itself is never
/// stored — only the parsed cell values.
/// </summary>
public class ImportBatch : BaseEntity, IAuditableEntity
{
    public int DistrictId { get; set; }
    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Previewed;
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime? CommittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public District District { get; set; } = null!;
    public ICollection<ImportRow> Rows { get; set; } = new List<ImportRow>();
}
