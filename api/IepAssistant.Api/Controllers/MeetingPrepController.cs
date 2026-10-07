using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.MeetingPrep;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class MeetingPrepController : ControllerBase
{
    private readonly IMeetingPrepService _meetingPrepService;
    private readonly MeetingPrepQueue _queue;
    private readonly IStringLocalizer<Ai> _localizer;

    public MeetingPrepController(IMeetingPrepService meetingPrepService, MeetingPrepQueue queue, IStringLocalizer<Ai> localizer)
    {
        _meetingPrepService = meetingPrepService;
        _queue = queue;
        _localizer = localizer;
    }

    [HttpPost("api/children/{childId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromGoals(int childId, [FromBody] GenerateMeetingPrepRequest? request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.GenerateFromGoalsAsync(childId, userId, request?.MeetingDate, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        await _queue.EnqueueAsync(result.Data, cancellationToken);

        return Accepted(ApiResponse<object>.SuccessResponse(
            new { id = result.Data },
            _localizer["MeetingPrep.GenerationStarted"]));
    }

    [HttpPost("api/ieps/{iepId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromIep(int iepId, [FromBody] GenerateMeetingPrepRequest? request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.GenerateFromIepAsync(iepId, userId, request?.MeetingDate, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        await _queue.EnqueueAsync(result.Data, cancellationToken);

        return Accepted(ApiResponse<object>.SuccessResponse(
            new { id = result.Data },
            _localizer["MeetingPrep.GenerationStarted"]));
    }

    [HttpPost("api/etrs/{etrId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromEtr(int etrId, [FromBody] GenerateMeetingPrepRequest? request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.GenerateFromEtrAsync(etrId, userId, request?.MeetingDate, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        await _queue.EnqueueAsync(result.Data, cancellationToken);

        return Accepted(ApiResponse<object>.SuccessResponse(
            new { id = result.Data },
            _localizer["MeetingPrep.GenerationStarted"]));
    }

    [HttpGet("api/children/{childId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MeetingPrepChecklistModel>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByChild(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var checklists = await _meetingPrepService.GetByChildIdAsync(childId, userId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<MeetingPrepChecklistModel>>.SuccessResponse(checklists));
    }

    [HttpGet("api/meeting-prep/{id}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingPrepChecklistModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var checklist = await _meetingPrepService.GetByIdAsync(id, userId, cancellationToken);

        if (checklist == null)
            return NotFound(ApiResponse<object>.Error(_localizer["MeetingPrep.ChecklistNotFound"]));

        return Ok(ApiResponse<MeetingPrepChecklistModel>.SuccessResponse(checklist));
    }

    [HttpPut("api/meeting-prep/{id}/check")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckItem(int id, [FromBody] CheckItemDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["MeetingPrep.InvalidRequest"]));

        var userId = User.GetUserId();
        var request = new CheckItemRequest
        {
            Section = dto.Section,
            Index = dto.Index,
            IsChecked = dto.IsChecked
        };

        var result = await _meetingPrepService.CheckItemAsync(id, userId, request, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpDelete("api/meeting-prep/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }
}
