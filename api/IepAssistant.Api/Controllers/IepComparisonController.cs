using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class IepComparisonController : ControllerBase
{
    private readonly IIepComparisonService _comparisonService;
    private readonly IStringLocalizer<Messages> _localizer;

    public IepComparisonController(IIepComparisonService comparisonService, IStringLocalizer<Messages> localizer)
    {
        _comparisonService = comparisonService;
        _localizer = localizer;
    }

    [HttpGet("api/children/{childId}/iep-timeline")]
    [ProducesResponseType(typeof(ApiResponse<TimelineResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _comparisonService.GetTimelineAsync(childId, userId, cancellationToken);

        if (result == null)
            return NotFound(ApiResponse<object>.Error(_localizer["IepComparisonApi.ChildNotFound"]));

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
            return NotFound(ApiResponse<object>.Error(_localizer["IepComparisonApi.DocumentsNotFoundOrAccessDenied"]));

        return Ok(ApiResponse<ComparisonResult>.SuccessResponse(result));
    }
}
