using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ClosedXML.Excel;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>One parsed data row: its 1-based worksheet row number and canonical column → cell text.</summary>
internal sealed class ImportSheetRow
{
    public int RowNumber { get; init; }
    public Dictionary<string, string> Cells { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string Get(string column) => Cells.TryGetValue(column, out var v) ? v : string.Empty;
}

/// <summary>
/// Workbook I/O shared by the student and staff importers. Reading never evaluates formulas: a formula
/// cell contributes only its cached value. Text cells are read verbatim (leading zeros survive), numbers
/// are rendered without scientific notation (or through their explicit number format, so "00123" stays
/// "00123"), dates become ISO <c>yyyy-MM-dd</c>. Upload rejections (size, extension/content type incl.
/// <c>.xlsm</c>, macro payload, decompression budget, row cap) live here so both importers apply identical rules.
/// </summary>
internal static class ImportWorkbook
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 5000;

    /// <summary>
    /// Decompression budget checked BEFORE ClosedXML materializes the workbook: declared uncompressed
    /// size of all zip entries, the entry count, and the size of any single sheet / shared-string part.
    /// Deflate reaches ~1000:1 on repetitive XML, so the 5 MB transport cap alone does not bound memory.
    /// </summary>
    public const long MaxUncompressedBytes = 50L * 1024 * 1024;
    public const int MaxZipEntries = 200;
    public const long MaxPartBytes = 25L * 1024 * 1024;

    /// <summary>Longest cell text kept (the rest is dropped) so a pathological cell cannot bloat PayloadJson.</summary>
    public const int MaxCellLength = 1000;

    private const string TooLargeToReadMessage = "The workbook is too large to import. Remove unused sheets, formatting or data and try again.";
    public const string ClearToken = "CLEAR";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] AcceptedContentTypes = { XlsxContentType, "application/octet-stream" };
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy-M-d", "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy", "yyyy/MM/dd", "d-MMM-yyyy", "MMM d, yyyy", "MMMM d, yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "M/d/yyyy H:mm", "M/d/yyyy h:mm:ss tt"
    };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Returns a user-facing rejection for a bad upload, or null when it may be parsed.</summary>
    public static string? ValidateUpload(ImportUploadModel upload)
    {
        if (upload.Content.Length == 0 || upload.Length <= 0)
            return "Choose a file to upload.";
        if (upload.Length > MaxBytes || upload.Content.LongLength > MaxBytes)
            return "The file is larger than 5 MB.";

        var extension = Path.GetExtension(upload.FileName ?? string.Empty);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase)
                ? "Macro-enabled workbooks (.xlsm) are not accepted. Save the file as .xlsx and try again."
                : "Only .xlsx workbooks are accepted.";

        var contentType = (upload.ContentType ?? string.Empty).Split(';')[0].Trim();
        if (contentType.Length > 0 && !AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return "Only .xlsx workbooks are accepted.";

        // Defense in depth: a macro workbook renamed to .xlsx still carries its VBA project.
        try
        {
            using var zip = new ZipArchive(new MemoryStream(upload.Content), ZipArchiveMode.Read, leaveOpen: false);
            if (zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                return "Macro-enabled workbooks are not accepted. Save the file as .xlsx and try again.";
            if (!zip.Entries.Any(e => e.FullName.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase)))
                return "The file could not be read as an Excel workbook (.xlsx).";
            if (ExceedsDecompressionBudget(zip))
                return TooLargeToReadMessage;
        }
        catch (InvalidDataException)
        {
            return "The file could not be read as an Excel workbook (.xlsx).";
        }

        return null;
    }

    /// <summary>Declared (uncompressed) sizes are in the central directory, so this costs no inflation.</summary>
    internal static bool ExceedsDecompressionBudget(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxZipEntries)
            return true;
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            total += entry.Length;
            if (total > MaxUncompressedBytes)
                return true;
            var name = entry.FullName;
            var isBulkPart = name.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase)
                             || (name.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (isBulkPart && entry.Length > MaxPartBytes)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reads <paramref name="sheetName"/>: header row 1 (trailing <c>*</c> and spaces ignored, case-insensitive),
    /// data from row 2; blank rows are skipped; unknown columns ignored. Fails on a missing sheet, missing
    /// required columns, or more than <see cref="MaxRows"/> data rows.
    /// </summary>
    public static (List<ImportSheetRow>? Rows, string? Error) ReadSheet(byte[] content, string sheetName, IReadOnlyList<string> columns, IReadOnlyList<string[]> requiredColumnGroups)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content), new LoadOptions { RecalculateAllFormulas = false });
        }
        catch (Exception)
        {
            return (null, "The file could not be read as an Excel workbook (.xlsx).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
                return (null, $"The workbook must contain a sheet named '{sheetName}'.");

            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = NormalizeHeader(CellText(sheet.Cell(1, c)));
                if (header.Length == 0) continue;
                var canonical = columns.FirstOrDefault(k => string.Equals(NormalizeHeader(k), header, StringComparison.OrdinalIgnoreCase));
                if (canonical != null && !columnIndex.ContainsKey(canonical))
                    columnIndex[canonical] = c;
            }

            foreach (var group in requiredColumnGroups)
            {
                if (!group.Any(columnIndex.ContainsKey))
                    return (null, $"Missing required column: {string.Join(" or ", group)}.");
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxRows)
                return (null, $"The sheet has more than {MaxRows:N0} data rows. Split the file and try again.");

            var rows = new List<ImportSheetRow>();
            for (var r = 2; r <= lastRow; r++)
            {
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var any = false;
                foreach (var (column, index) in columnIndex)
                {
                    var text = CellText(sheet.Cell(r, index));
                    cells[column] = text;
                    if (text.Length > 0) any = true;
                }
                if (any)
                    rows.Add(new ImportSheetRow { RowNumber = r, Cells = cells });
            }
            return (rows, null);
        }
    }

    /// <summary>Cell → text without formula evaluation (see class remarks); text is capped at <see cref="MaxCellLength"/>.</summary>
    public static string CellText(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        switch (value.Type)
        {
            case XLDataType.Blank:
                return string.Empty;
            case XLDataType.Text:
                return Truncate(value.GetText().Trim(), MaxCellLength);
            case XLDataType.Boolean:
                return value.GetBoolean() ? "TRUE" : "FALSE";
            case XLDataType.DateTime:
                return value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case XLDataType.TimeSpan:
                return value.GetTimeSpan().ToString();
            case XLDataType.Error:
                return string.Empty;
            case XLDataType.Number:
            {
                var number = value.GetNumber();
                var format = cell.Style.NumberFormat.Format;
                var hasCustomFormat = !cell.HasFormula
                                      && !string.IsNullOrEmpty(format)
                                      && !string.Equals(format, "General", StringComparison.OrdinalIgnoreCase);
                if (hasCustomFormat)
                {
                    // Explicit number formats such as "00000" carry the leading zeros the user intended.
                    var formatted = cell.GetFormattedString().Trim();
                    if (formatted.Length > 0 && !formatted.Contains('E', StringComparison.OrdinalIgnoreCase))
                        return formatted;
                }
                return number.ToString("0.############", CultureInfo.InvariantCulture);
            }
            default:
                return Truncate(value.ToString().Trim(), MaxCellLength);
        }
    }

    public static string NormalizeHeader(string header)
        => new string(header.Where(ch => !char.IsWhiteSpace(ch) && ch != '*' && ch != '_').ToArray());

    /// <summary>Bounds user-supplied text to a stored column length (row keys, names, messages).</summary>
    public static string Truncate(string? text, int max)
        => string.IsNullOrEmpty(text) ? string.Empty : (text.Length <= max ? text : text.Substring(0, max));

    public static bool IsClear(string text) => string.Equals(text.Trim(), ClearToken, StringComparison.Ordinal);

    /// <summary>Parses a date cell text (ISO or common US formats, or an Excel serial). Null when blank.</summary>
    public static (DateTime? Value, bool Ok) ParseDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, true);
        var t = text.Trim();
        if (DateTime.TryParseExact(t, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return (exact.Date, true);
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 0 and < 100000)
            return (DateTime.FromOADate(serial).Date, true);
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
            return (loose.Date, true);
        return (null, false);
    }

    public static string FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    /// <summary>Error report: the canonical columns (in order) + an <c>Error</c> column, one row per error row.</summary>
    public static byte[] BuildErrorWorkbook(string sheetName, IReadOnlyList<string> columns, IEnumerable<(Dictionary<string, string> Payload, string Error)> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);
        for (var c = 0; c < columns.Count; c++)
            sheet.Cell(1, c + 1).SetValue(columns[c]);
        sheet.Cell(1, columns.Count + 1).SetValue("Error");
        sheet.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var (payload, error) in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(payload.TryGetValue(columns[c], out var v) ? v : string.Empty);
            }
            sheet.Cell(r, columns.Count + 1).SetValue(error);
            r++;
        }
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] ToBytes(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Writes a vertical list under a header in the given column of the Values sheet.</summary>
    public static void WriteValuesColumn(IXLWorksheet sheet, int column, string header, IEnumerable<string> values)
    {
        sheet.Cell(1, column).SetValue(header);
        sheet.Cell(1, column).Style.Font.Bold = true;
        var r = 2;
        foreach (var value in values)
        {
            var cell = sheet.Cell(r++, column);
            cell.Style.NumberFormat.Format = "@";
            cell.SetValue(value);
        }
    }

    public static string SerializePayload(Dictionary<string, string> payload) => JsonSerializer.Serialize(payload, JsonOptions);

    public static Dictionary<string, string> DeserializePayload(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) is { } d
            ? new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static string SerializeChanges(List<string> changes) => JsonSerializer.Serialize(changes, JsonOptions);

    public static List<string> DeserializeChanges(string json)
        => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();

    public static ImportPreviewModel MapPreview(ImportBatch batch, IEnumerable<ImportRow> rows) => new()
    {
        BatchId = batch.Id,
        Kind = batch.Kind,
        FileName = batch.FileName,
        Status = batch.Status,
        Counts = MapCounts(batch),
        Rows = rows.OrderBy(r => r.RowNumber).Select(r => new ImportRowModel
        {
            RowNumber = r.RowNumber,
            Outcome = r.Outcome,
            Key = r.Key,
            DisplayName = r.DisplayName,
            Message = r.Message,
            Changes = DeserializeChanges(r.ChangesJson)
        }).ToList(),
        CreatedAt = batch.CreatedAt,
        CommittedAt = batch.CommittedAt
    };

    public static ImportCountsModel MapCounts(ImportBatch batch) => new()
    {
        Total = batch.TotalCount,
        New = batch.NewCount,
        Updated = batch.UpdatedCount,
        Unchanged = batch.UnchangedCount,
        Error = batch.ErrorCount
    };

    public static void SetCounts(ImportBatch batch, IReadOnlyCollection<ImportRow> rows)
    {
        batch.TotalCount = rows.Count;
        batch.NewCount = rows.Count(r => r.Outcome == ImportRowOutcome.New);
        batch.UpdatedCount = rows.Count(r => r.Outcome == ImportRowOutcome.Updated);
        batch.UnchangedCount = rows.Count(r => r.Outcome == ImportRowOutcome.Unchanged);
        batch.ErrorCount = rows.Count(r => r.Outcome == ImportRowOutcome.Error);
    }

    /// <summary>Marks every row whose key repeats within the file as an error (both/all copies).</summary>
    public static void FlagDuplicateKeys(List<ImportRow> rows, string keyLabel)
    {
        foreach (var group in rows.Where(r => r.Key.Length > 0).GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var rowNumbers = string.Join(", ", group.Select(r => r.RowNumber).OrderBy(n => n));
            foreach (var row in group)
            {
                row.Outcome = ImportRowOutcome.Error;
                row.Message = $"Duplicate {keyLabel} in file (rows {rowNumbers}).";
                row.ChangesJson = "[]";
            }
        }
    }
}
