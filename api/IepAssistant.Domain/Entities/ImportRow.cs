namespace IepAssistant.Domain.Entities;

public enum ImportRowOutcome
{
    New,
    Updated,
    Unchanged,
    Error
}

/// <summary>
/// One data row of an <see cref="ImportBatch"/>: its preview outcome, the matching key (StudentId or
/// Email), a human message for errors, the human-readable change list and the parsed cell values
/// (<see cref="PayloadJson"/>) so commit never re-reads the workbook.
/// </summary>
public class ImportRow : BaseEntity
{
    public int BatchId { get; set; }

    /// <summary>1-based worksheet row number (header is row 1, so data starts at 2).</summary>
    public int RowNumber { get; set; }

    public ImportRowOutcome Outcome { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Message { get; set; }

    /// <summary>JSON string array of "Field: old → new" descriptions (empty for New/Unchanged/Error).</summary>
    public string ChangesJson { get; set; } = "[]";

    /// <summary>JSON object of the parsed row (column → cell text), used by commit.</summary>
    public string PayloadJson { get; set; } = "{}";

    public ImportBatch Batch { get; set; } = null!;
}
