using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepVersions;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Finalize + read endpoints for immutable IepVersion snapshots (P5a). Gated behind
/// <c>Feature:SchoolSide</c>. Educators reach versions through the SchoolId-bound access pattern;
/// linked parents reach them through an active accepted ChildLink + AccessService.
/// </summary>
[ApiController]
[Authorize]
public class IepVersionController : ControllerBase
{
    private readonly IIepVersionService _service;
    private readonly IFeatureFlags _featureFlags;

    public IepVersionController(IIepVersionService service, IFeatureFlags featureFlags)
    {
        _service = service;
        _featureFlags = featureFlags;
    }

    // ---------------------------------------------------------------- Finalize

    [HttpPost("api/iep-drafts/{draftId}/finalize")]
    [ProducesResponseType(typeof(ApiResponse<IepVersionSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Finalize(int draftId, [FromBody] FinalizeIepDraftRequest? request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.FinalizeAsync(User.GetUserId(), draftId, request?.EffectiveDate, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = IepVersionMappers.MapSummary(result.Data!);
        return CreatedAtAction(nameof(GetVersion), new { versionId = dto.Id }, ApiResponse<IepVersionSummaryDto>.SuccessResponse(dto));
    }

    // ---------------------------------------------------------------- Educator reads

    [HttpGet("api/educator/students/{studentId}/iep-versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IepVersionSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForStudent(int studentId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.ListForStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<IepVersionSummaryDto>>.SuccessResponse(result.Data!.Select(IepVersionMappers.MapSummary)));
    }

    // ---------------------------------------------------------------- Parent reads

    [HttpGet("api/children/{childId}/iep-versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IepVersionSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForChild(int childId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.ListForChildAsync(User.GetUserId(), childId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<IepVersionSummaryDto>>.SuccessResponse(result.Data!.Select(IepVersionMappers.MapSummary)));
    }

    // ---------------------------------------------------------------- Shared full read

    [HttpGet("api/iep-versions/{versionId}")]
    [ProducesResponseType(typeof(ApiResponse<IepVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(int versionId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.GetVersionAsync(User.GetUserId(), versionId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IepVersionDto>.SuccessResponse(IepVersionMappers.MapFull(result.Data!)));
    }

    // ---------------------------------------------------------------- Helpers

    private bool Enabled => _featureFlags.IsEnabled(FeatureFlags.SchoolSide);

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
