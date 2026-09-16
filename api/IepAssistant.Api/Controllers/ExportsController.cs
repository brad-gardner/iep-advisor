using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Documents;
using IepAssistant.Api.DTOs.Export;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// District and per-student data export (plan 7, decision 8). Spans three route prefixes
/// (<c>api/district/exports</c>, <c>api/exports/{id}</c>, <c>api/educator/students/{id}/export</c>) on
/// purpose, mirroring <see cref="MeetingsController"/>'s multi-prefix precedent. Enqueue endpoints return
/// 202 Accepted with the job id — <see cref="ExportWorker"/> builds the ZIP off <see cref="ExportQueue"/>
/// after this controller enqueues, exactly like <see cref="AuthoredDocumentVersionController"/>'s
/// finalize→PDF-queue split.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class ExportsController : ControllerBase
{
    private readonly IExportService _exports;
    private readonly ExportQueue _queue;

    public ExportsController(IExportService exports, ExportQueue queue)
    {
        _exports = exports;
        _queue = queue;
    }

    [HttpPost("district/exports")]
    [ProducesResponseType(typeof(ApiResponse<ExportJobIdDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnqueueDistrictExport(CancellationToken ct)
    {
        var result = await _exports.EnqueueDistrictExportAsync(User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        await _queue.EnqueueAsync(result.Data!.Id, CancellationToken.None);
        return Accepted(ApiResponse<ExportJobIdDto>.SuccessResponse(new ExportJobIdDto { JobId = result.Data!.Id }));
    }

    [HttpGet("district/exports")]
    [ProducesResponseType(typeof(ApiResponse<List<ExportJobDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListDistrictExports(CancellationToken ct)
    {
        var result = await _exports.ListDistrictExportsAsync(User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<ExportJobDto>>.SuccessResponse(result.Data!.Select(ExportMappers.MapJob).ToList()));
    }

    [HttpPost("educator/students/{id:int}/export")]
    [ProducesResponseType(typeof(ApiResponse<ExportJobIdDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnqueueStudentExport(int id, CancellationToken ct)
    {
        var result = await _exports.EnqueueStudentExportAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);

        await _queue.EnqueueAsync(result.Data!.Id, CancellationToken.None);
        return Accepted(ApiResponse<ExportJobIdDto>.SuccessResponse(new ExportJobIdDto { JobId = result.Data!.Id }));
    }

    [HttpGet("exports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ExportJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(int id, CancellationToken ct)
    {
        var result = await _exports.GetStatusAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<ExportJobDto>.SuccessResponse(ExportMappers.MapJob(result.Data!)));
    }

    [HttpGet("exports/{id:int}/download")]
    [ProducesResponseType(typeof(ApiResponse<AuthoredDocumentPdfDownloadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var result = await _exports.GetDownloadUrlAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<AuthoredDocumentPdfDownloadDto>.SuccessResponse(new AuthoredDocumentPdfDownloadDto { Url = result.Data! }));
    }

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
