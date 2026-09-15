using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.District;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// XLSX roster / staff import (plan 3). DistrictAdmin and SchoolAdmin only (enforced in the services).
/// Uploads are capped at 5 MB before the body is read; the services additionally reject non-.xlsx
/// files (incl. macro-enabled .xlsm), macro payloads, missing columns and more than 5,000 rows.
/// </summary>
[ApiController]
[Authorize]
[Route("api/district/imports")]
public class DistrictImportsController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly IRosterImportService _rosterImports;
    private readonly IStaffImportService _staffImports;

    public DistrictImportsController(IRosterImportService rosterImports, IStaffImportService staffImports)
    {
        _rosterImports = rosterImports;
        _staffImports = staffImports;
    }

    [HttpGet("students/template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentTemplate(CancellationToken ct)
    {
        var result = await _rosterImports.GenerateTemplateAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return File(result.Data!, XlsxContentType, "students-import-template.xlsx");
    }

    [HttpGet("staff/template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaffTemplate(CancellationToken ct)
    {
        var result = await _staffImports.GenerateTemplateAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return File(result.Data!, XlsxContentType, "staff-import-template.xlsx");
    }

    [HttpPost("students/preview")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewStudents(IFormFile? file, CancellationToken ct)
    {
        var (upload, error) = await ReadUploadAsync(file, ct);
        if (error != null)
            return BadRequest(ApiResponse<object>.Error(error));

        var result = await _rosterImports.PreviewAsync(User.GetUserId(), upload!, ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return Ok(ApiResponse<ImportPreviewDto>.SuccessResponse(MapPreview(result.Data!)));
    }

    [HttpPost("staff/preview")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewStaff(IFormFile? file, CancellationToken ct)
    {
        var (upload, error) = await ReadUploadAsync(file, ct);
        if (error != null)
            return BadRequest(ApiResponse<object>.Error(error));

        var result = await _staffImports.PreviewAsync(User.GetUserId(), upload!, ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return Ok(ApiResponse<ImportPreviewDto>.SuccessResponse(MapPreview(result.Data!)));
    }

    [HttpPost("{batchId:int}/commit")]
    [ProducesResponseType(typeof(ApiResponse<ImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Commit(int batchId, [FromBody] CommitImportRequest request, CancellationToken ct)
    {
        // The batch's kind decides which pipeline applies it.
        var batch = await _rosterImports.GetBatchSummaryAsync(User.GetUserId(), batchId, ct);
        if (!batch.Success)
            return MapFailure(batch.Message);

        var result = batch.Data!.Kind == Domain.Entities.ImportKind.Staff
            ? await _staffImports.CommitAsync(User.GetUserId(), batchId, request.CommitValid, ct)
            : await _rosterImports.CommitAsync(User.GetUserId(), batchId, request.CommitValid, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<ImportResultDto>.SuccessResponse(new ImportResultDto
        {
            BatchId = result.Data!.BatchId,
            Committed = new ImportCommittedCountsDto
            {
                New = result.Data.Committed.New,
                Updated = result.Data.Committed.Updated,
                Unchanged = result.Data.Committed.Unchanged
            },
            Skipped = result.Data.Skipped,
            Status = result.Data.Status
        }));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ImportBatchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        var result = await _rosterImports.GetHistoryAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<ImportBatchDto>>.SuccessResponse(result.Data!.Select(b => new ImportBatchDto
        {
            BatchId = b.BatchId,
            Kind = b.Kind,
            FileName = b.FileName,
            Status = b.Status,
            Counts = MapCounts(b.Counts),
            CreatedAt = b.CreatedAt,
            CommittedAt = b.CommittedAt,
            CreatedByName = b.CreatedByName
        })));
    }

    [HttpGet("{batchId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatch(int batchId, CancellationToken ct)
    {
        var result = await _rosterImports.GetBatchAsync(User.GetUserId(), batchId, ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return Ok(ApiResponse<ImportPreviewDto>.SuccessResponse(MapPreview(result.Data!)));
    }

    [HttpGet("{batchId:int}/errors.xlsx")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetErrors(int batchId, CancellationToken ct)
    {
        var result = await _rosterImports.BuildErrorWorkbookAsync(User.GetUserId(), batchId, ct);
        if (!result.Success)
            return MapFailure(result.Message);
        return File(result.Data!, XlsxContentType, $"import-{batchId}-errors.xlsx");
    }

    // ----------------------------------------------------------------- helpers

    private static async Task<(ImportUploadModel? Upload, string? Error)> ReadUploadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return (null, "Choose a file to upload.");
        if (file.Length > MaxUploadBytes)
            return (null, "The file is larger than 5 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase)
                ? "Macro-enabled workbooks (.xlsm) are not accepted. Save the file as .xlsx and try again."
                : "Only .xlsx workbooks are accepted.");

        var contentType = (file.ContentType ?? string.Empty).Split(';')[0].Trim();
        if (contentType.Length > 0
            && !contentType.Equals(XlsxContentType, StringComparison.OrdinalIgnoreCase)
            && !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return (null, "Only .xlsx workbooks are accepted.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return (new ImportUploadModel
        {
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            Length = file.Length,
            Content = ms.ToArray()
        }, null);
    }

    private static ImportPreviewDto MapPreview(ImportPreviewModel m) => new()
    {
        BatchId = m.BatchId,
        Kind = m.Kind,
        FileName = m.FileName,
        Status = m.Status,
        Counts = MapCounts(m.Counts),
        Rows = m.Rows.Select(r => new ImportRowDto
        {
            RowNumber = r.RowNumber,
            Outcome = r.Outcome,
            Key = r.Key,
            DisplayName = r.DisplayName,
            Message = r.Message,
            Changes = r.Changes
        }).ToList(),
        CreatedAt = m.CreatedAt,
        CommittedAt = m.CommittedAt
    };

    private static ImportCountsDto MapCounts(ImportCountsModel c) => new()
    {
        Total = c.Total,
        New = c.New,
        Updated = c.Updated,
        Unchanged = c.Unchanged,
        Error = c.Error
    };

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
