using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Meetings;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Structured meeting decisions and their surfacing as proposed edits on a draft (plan 7, decision 3).
/// Spans three route prefixes on purpose (meeting-scoped list/create, decision-scoped mutations, and the
/// document-scoped proposed-edits read) so each mirrors the contract exactly, matching
/// <see cref="MeetingsController"/>'s multi-prefix precedent.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class MeetingDecisionsController : ControllerBase
{
    private readonly IMeetingDecisionService _decisions;

    public MeetingDecisionsController(IMeetingDecisionService decisions)
    {
        _decisions = decisions;
    }

    [HttpGet("meetings/{id:int}/decisions")]
    [ProducesResponseType(typeof(ApiResponse<List<MeetingDecisionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForMeeting(int id, CancellationToken ct)
    {
        var result = await _decisions.GetForMeetingAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<List<MeetingDecisionDto>>(result.Message);
        return Ok(ApiResponse<List<MeetingDecisionDto>>.SuccessResponse(result.Data!.Select(MeetingBriefMappers.MapDecision).ToList()));
    }

    [HttpPost("meetings/{id:int}/decisions")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDecisionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int id, [FromBody] CreateMeetingDecisionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Outcome == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _decisions.CreateAsync(User.GetUserId(), id, new CreateMeetingDecisionModel
        {
            TargetFieldKey = request.TargetFieldKey,
            TargetRowId = request.TargetRowId,
            TargetLabel = request.TargetLabel,
            Text = request.Text,
            Outcome = request.Outcome.Value
        }, ct);
        if (!result.Success) return MapFailure<MeetingDecisionDto>(result.Message);

        var dto = MeetingBriefMappers.MapDecision(result.Data!);
        return Created($"/api/decisions/{dto.Id}", ApiResponse<MeetingDecisionDto>.SuccessResponse(dto));
    }

    [HttpPut("decisions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDecisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMeetingDecisionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Outcome == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _decisions.UpdateAsync(User.GetUserId(), id, new UpdateMeetingDecisionModel
        {
            Text = request.Text,
            Outcome = request.Outcome.Value
        }, ct);
        if (!result.Success) return MapFailure<MeetingDecisionDto>(result.Message);
        return Ok(ApiResponse<MeetingDecisionDto>.SuccessResponse(MeetingBriefMappers.MapDecision(result.Data!)));
    }

    [HttpDelete("decisions/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _decisions.DeleteAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return NoContent();
    }

    [HttpGet("documents/{instanceId:int}/proposed-edits")]
    [ProducesResponseType(typeof(ApiResponse<List<ProposedEditDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProposedEdits(int instanceId, CancellationToken ct)
    {
        var result = await _decisions.GetProposedEditsForInstanceAsync(User.GetUserId(), instanceId, ct);
        if (!result.Success) return MapFailure<List<ProposedEditDto>>(result.Message);
        return Ok(ApiResponse<List<ProposedEditDto>>.SuccessResponse(result.Data!.Select(MeetingBriefMappers.MapProposedEdit).ToList()));
    }

    [HttpPost("decisions/{id:int}/mark-applied")]
    [ProducesResponseType(typeof(ApiResponse<ProposedEditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkApplied(int id, CancellationToken ct)
    {
        var result = await _decisions.MarkAppliedAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<ProposedEditDto>(result.Message);
        return Ok(ApiResponse<ProposedEditDto>.SuccessResponse(MeetingBriefMappers.MapProposedEdit(result.Data!)));
    }

    private IActionResult MapFailure<T>(string? message)
    {
        message ??= "Request failed";
        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));
        return BadRequest(ApiResponse<object>.Error(message));
    }
}
