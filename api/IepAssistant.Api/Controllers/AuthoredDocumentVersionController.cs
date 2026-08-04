using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Documents;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Finalize + read endpoints for immutable authored document versions (State Document Template Engine,
/// Phase 4). Declares only <c>[Authorize]</c>; per-resource authorization is enforced inside
/// <see cref="IAuthoredDocumentVersionService"/> (Collaborator+ to finalize/retry, Viewer+ / linked-parent
/// to read). Mirrors <see cref="IepVersionController"/>. Enums serialize as strings.
/// </summary>
[ApiController]
[Authorize]
public class AuthoredDocumentVersionController : ControllerBase
{
    private readonly IAuthoredDocumentVersionService _service;
    private readonly AuthoredDocumentPdfQueue _pdfQueue;

    public AuthoredDocumentVersionController(IAuthoredDocumentVersionService service, AuthoredDocumentPdfQueue pdfQueue)
    {
        _service = service;
        _pdfQueue = pdfQueue;
    }

    // ---------------------------------------------------------------- Finalize

    [HttpPost("api/documents/{instanceId:int}/finalize")]
    [ProducesResponseType(typeof(ApiResponse<AuthoredDocumentVersionSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finalize(int instanceId, CancellationToken ct)
    {
        var result = await _service.FinalizeAsync(instanceId, User.GetUserId(), ct);
        if (!result.Success)
        {
            // A validation failure carries the COMPLETE list of missing/invalid fields (section + field
            // label + row index) so the UI can surface them all at once.
            if (result.Errors.Count > 0)
                return UnprocessableEntity(new ApiResponse<object> { Success = false, Message = result.Message, Errors = result.Errors });
            return MapFailure(result.Message);
        }

        // After-commit, failure-isolated: FinalizeAsync already committed the version (+ a Pending PDF row).
        // Enqueue the render now with CancellationToken.None — the version is durably committed, so a
        // client disconnect must not abort the enqueue (the startup reconcile would re-pick it up anyway,
        // but we also avoid throwing back a success that already happened).
        await _pdfQueue.EnqueueAsync(result.Data!.Id, CancellationToken.None);

        var dto = AuthoredDocumentVersionMappers.MapSummary(result.Data!);
        return CreatedAtAction(nameof(GetVersion), new { versionId = dto.Id }, ApiResponse<AuthoredDocumentVersionSummaryDto>.SuccessResponse(dto));
    }

    // ---------------------------------------------------------------- Educator reads

    [HttpGet("api/educator/students/{studentId:int}/authored-versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AuthoredDocumentVersionSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForStudent(int studentId, CancellationToken ct)
    {
        var result = await _service.ListVersionsForStudentAsync(studentId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<AuthoredDocumentVersionSummaryDto>>.SuccessResponse(
            result.Data!.Select(AuthoredDocumentVersionMappers.MapSummary)));
    }

    // ---------------------------------------------------------------- Parent reads

    [HttpGet("api/children/{childId:int}/authored-versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AuthoredDocumentVersionSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForChild(int childId, CancellationToken ct)
    {
        var result = await _service.ListForChildAsync(childId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<AuthoredDocumentVersionSummaryDto>>.SuccessResponse(
            result.Data!.Select(AuthoredDocumentVersionMappers.MapSummary)));
    }

    // ---------------------------------------------------------------- Shared full read

    [HttpGet("api/authored-versions/{versionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<AuthoredDocumentVersionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(int versionId, CancellationToken ct)
    {
        var result = await _service.GetVersionAsync(versionId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<AuthoredDocumentVersionDetailDto>.SuccessResponse(
            AuthoredDocumentVersionMappers.MapDetail(result.Data!)));
    }

    // ---------------------------------------------------------------- PDF status + retry

    [HttpGet("api/authored-versions/{versionId:int}/pdf")]
    [ProducesResponseType(typeof(ApiResponse<AuthoredDocumentPdfStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(int versionId, CancellationToken ct)
    {
        var result = await _service.GetPdfStatusAsync(versionId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<AuthoredDocumentPdfStatusDto>.SuccessResponse(
            AuthoredDocumentVersionMappers.MapPdfStatus(result.Data!)));
    }

    [HttpGet("api/authored-versions/{versionId:int}/pdf/download")]
    [ProducesResponseType(typeof(ApiResponse<AuthoredDocumentPdfDownloadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(int versionId, CancellationToken ct)
    {
        // Distinct from the status poll: this mints the SAS and records the FERPA Export audit, so it
        // is called only when the user actually downloads.
        var result = await _service.GetPdfDownloadUrlAsync(versionId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<AuthoredDocumentPdfDownloadDto>.SuccessResponse(
            new AuthoredDocumentPdfDownloadDto { Url = result.Data! }));
    }

    [HttpPost("api/authored-versions/{versionId:int}/pdf/retry")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryPdf(int versionId, CancellationToken ct)
    {
        var result = await _service.RequestPdfRetryAsync(versionId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        // The service committed the Pending flip; now enqueue the re-render (after-commit, isolated).
        await _pdfQueue.EnqueueAsync(result.Data, CancellationToken.None);

        return Ok(ApiResponse<object>.SuccessResponse(new { versionId, status = "Pending" }));
    }

    // ---------------------------------------------------------------- Helpers

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        // State conflicts (already finalizing, wrong state, or a concurrent-finalize race) → 409.
        if (message.Contains("already being finalized", StringComparison.OrdinalIgnoreCase)
            || message.Contains("at the same time", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot be finalized", StringComparison.OrdinalIgnoreCase))
            return Conflict(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
