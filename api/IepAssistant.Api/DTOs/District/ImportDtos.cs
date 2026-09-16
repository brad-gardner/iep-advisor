using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.District;

public class ImportCountsDto
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Error { get; set; }
}

public class ImportRowDto
{
    public int RowNumber { get; set; }
    public ImportRowOutcome Outcome { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<string> Changes { get; set; } = new();
}

public class ImportPreviewDto
{
    public int BatchId { get; set; }
    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; }
    public ImportCountsDto Counts { get; set; } = new();
    public List<ImportRowDto> Rows { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CommittedAt { get; set; }
}

public class ImportCommittedCountsDto
{
    public int New { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
}

public class ImportResultDto
{
    public int BatchId { get; set; }
    public ImportCommittedCountsDto Committed { get; set; } = new();
    public int Skipped { get; set; }
    public ImportBatchStatus Status { get; set; }
}

public class ImportBatchDto
{
    public int BatchId { get; set; }
    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; }
    public ImportCountsDto Counts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CommittedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

public class CommitImportRequest
{
    public bool CommitValid { get; set; }
}
