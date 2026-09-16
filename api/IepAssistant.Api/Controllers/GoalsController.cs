using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Goals;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Goals as first-class records across documents/years, plus provider progress observations (plan 7,
/// decision 6). Declares only <c>[Authorize]</c> — per-resource authorization (Viewer+ to read,
/// Collaborator+ to write; a linked parent to read their child's current goals) is enforced inside
/// <see cref="IGoalRecordService"/>.
/// </summary>
[ApiController]
[Authorize]
public class GoalsController : ControllerBase
{
    private readonly IGoalRecordService _goals;

    public GoalsController(IGoalRecordService goals)
    {
        _goals = goals;
    }

    [HttpGet("api/educator/students/{id:int}/goals")]
    [ProducesResponseType(typeof(ApiResponse<List<GoalRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForStudent(int id, CancellationToken ct)
    {
        var result = await _goals.GetForStudentAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<GoalRecordDto>>.SuccessResponse(result.Data!.Select(GoalMappers.MapRecord).ToList()));
    }

    [HttpGet("api/educator/students/{id:int}/goals/history")]
    [ProducesResponseType(typeof(ApiResponse<List<GoalLineageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHistoryForStudent(int id, CancellationToken ct)
    {
        var result = await _goals.GetHistoryForStudentAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<GoalLineageDto>>.SuccessResponse(result.Data!.Select(GoalMappers.MapLineage).ToList()));
    }

    [HttpGet("api/children/{childId:int}/goals")]
    [ProducesResponseType(typeof(ApiResponse<List<GoalRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForChild(int childId, CancellationToken ct)
    {
        var result = await _goals.GetForChildAsync(User.GetUserId(), childId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<GoalRecordDto>>.SuccessResponse(result.Data!.Select(GoalMappers.MapRecord).ToList()));
    }

    [HttpPost("api/goals/{goalRecordId:int}/observations")]
    [ProducesResponseType(typeof(ApiResponse<GoalObservationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddObservation(int goalRecordId, [FromBody] CreateGoalObservationRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _goals.AddObservationAsync(User.GetUserId(), goalRecordId, new CreateGoalObservationModel
        {
            ObservedAt = request.ObservedAt,
            Value = request.Value,
            Unit = request.Unit,
            Note = request.Note
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = GoalMappers.MapObservation(result.Data!);
        return Created($"/api/goals/{goalRecordId}/observations", ApiResponse<GoalObservationDto>.SuccessResponse(dto));
    }

    [HttpPut("api/goals/{goalRecordId:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<GoalRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int goalRecordId, [FromBody] UpdateGoalStatusRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _goals.UpdateStatusAsync(User.GetUserId(), goalRecordId, new UpdateGoalStatusModel
        {
            Status = request.Status,
            Reason = request.Reason
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<GoalRecordDto>.SuccessResponse(GoalMappers.MapRecord(result.Data!)));
    }

    [HttpPost("api/documents/{instanceId:int}/goal-retirements")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordRetirement(int instanceId, [FromBody] CreateGoalRetirementRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _goals.RecordRetirementAsync(User.GetUserId(), instanceId, new CreateGoalRetirementModel
        {
            LineageId = request.LineageId,
            Reason = request.Reason
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return StatusCode(StatusCodes.Status201Created);
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
