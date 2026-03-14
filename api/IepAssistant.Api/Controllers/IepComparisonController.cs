using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class IepComparisonController : ControllerBase
{
    private readonly IIepComparisonService _comparisonService;

    public IepComparisonController(IIepComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    [HttpGet("api/children/{childId}/iep-timeline")]
    [ProducesResponseType(typeof(ApiResponse<TimelineResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _comparisonService.GetTimelineAsync(childId, userId, cancellationToken);

        if (result == null)
            return NotFound(ApiResponse<object>.Error("Child not found"));

        return Ok(ApiResponse<TimelineResult>.SuccessResponse(result));
    }

    [HttpGet("api/ieps/{id}/compare/{otherId}")]
    [ProducesResponseType(typeof(ApiResponse<ComparisonResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compare(int id, int otherId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _comparisonService.CompareAsync(id, otherId, userId, cancellationToken);

        if (result == null)
            return NotFound(ApiResponse<object>.Error("IEP documents not found or access denied"));

        return Ok(ApiResponse<ComparisonResult>.SuccessResponse(result));
    }
}
