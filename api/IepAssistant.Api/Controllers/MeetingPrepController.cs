using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.MeetingPrep;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class MeetingPrepController : ControllerBase
{
    private readonly IMeetingPrepService _meetingPrepService;
    private readonly MeetingPrepQueue _queue;

    public MeetingPrepController(IMeetingPrepService meetingPrepService, MeetingPrepQueue queue)
    {
        _meetingPrepService = meetingPrepService;
        _queue = queue;
    }

    [HttpPost("api/children/{childId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromGoals(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.GenerateFromGoalsAsync(childId, userId, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Generation failed"));

        await _queue.EnqueueAsync(result.Data, cancellationToken);

        return Accepted(ApiResponse<object>.SuccessResponse(
            new { id = result.Data },
            "Meeting prep checklist generation started"));
    }

    [HttpPost("api/ieps/{iepId}/meeting-prep")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromIep(int iepId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.GenerateFromIepAsync(iepId, userId, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Generation failed"));

        await _queue.EnqueueAsync(result.Data, cancellationToken);

        return Accepted(ApiResponse<object>.SuccessResponse(
            new { id = result.Data },
            "Meeting prep checklist generation started"));
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
            return NotFound(ApiResponse<object>.Error("Checklist not found"));

        return Ok(ApiResponse<MeetingPrepChecklistModel>.SuccessResponse(checklist));
    }

    [HttpPut("api/meeting-prep/{id}/check")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckItem(int id, [FromBody] CheckItemDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var request = new CheckItemRequest
        {
            Section = dto.Section,
            Index = dto.Index,
            IsChecked = dto.IsChecked
        };

        var result = await _meetingPrepService.CheckItemAsync(id, userId, request, cancellationToken);

        if (!result.Success)
        {
            if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(ApiResponse<object>.Error(result.Message));
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Update failed"));
        }

        return Ok(ApiResponse<object>.SuccessResponse(null, "Item checked"));
    }

    [HttpDelete("api/meeting-prep/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _meetingPrepService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Delete failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Checklist deleted"));
    }
}
