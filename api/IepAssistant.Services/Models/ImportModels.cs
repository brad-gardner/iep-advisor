using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>An uploaded workbook handed to preview: metadata used for the rejection checks + the bytes.</summary>
public class ImportUploadModel
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Length { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class ImportCountsModel
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Error { get; set; }
}

public class ImportRowModel
{
    public int RowNumber { get; set; }
    public ImportRowOutcome Outcome { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<string> Changes { get; set; } = new();
}

public class ImportPreviewModel
{
    public int BatchId { get; set; }
    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; }
    public ImportCountsModel Counts { get; set; } = new();
    public List<ImportRowModel> Rows { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CommittedAt { get; set; }
}

public class ImportCommittedCountsModel
{
    public int New { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
}

public class ImportResultModel
{
    public int BatchId { get; set; }
    public ImportCommittedCountsModel Committed { get; set; } = new();
    public int Skipped { get; set; }
    public ImportBatchStatus Status { get; set; }
}

public class ImportBatchModel
{
    public int BatchId { get; set; }
    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; }
    public ImportCountsModel Counts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CommittedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}
