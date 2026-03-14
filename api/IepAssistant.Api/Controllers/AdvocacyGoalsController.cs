using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.AdvocacyGoals;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class AdvocacyGoalsController : ControllerBase
{
    private readonly IParentAdvocacyGoalService _goalService;

    public AdvocacyGoalsController(IParentAdvocacyGoalService goalService)
    {
        _goalService = goalService;
    }

    [HttpGet("api/children/{childId}/advocacy-goals")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AdvocacyGoalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByChild(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var goals = await _goalService.GetByChildIdAsync(childId, userId, cancellationToken);
        var dtos = goals.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<AdvocacyGoalDto>>.SuccessResponse(dtos));
    }

    [HttpPost("api/children/{childId}/advocacy-goals")]
    [ProducesResponseType(typeof(ApiResponse<AdvocacyGoalDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateAdvocacyGoalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var model = new CreateAdvocacyGoalModel
        {
            GoalText = request.GoalText,
            Category = request.Category
        };

        var result = await _goalService.CreateAsync(childId, userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Creation failed"));

        var dto = MapToDto(result.Data!);
        return Created($"/api/advocacy-goals/{dto.Id}", ApiResponse<AdvocacyGoalDto>.SuccessResponse(dto, "Advocacy goal created successfully"));
    }

    [HttpPut("api/advocacy-goals/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdvocacyGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new UpdateAdvocacyGoalModel
        {
            GoalText = request.GoalText,
            Category = request.Category
        };

        var result = await _goalService.UpdateAsync(id, userId, model, cancellationToken);

        if (!result.Success)
        {
            if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(ApiResponse<object>.Error(result.Message));
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Update failed"));
        }

        return Ok(ApiResponse<object>.SuccessResponse(null, "Advocacy goal updated successfully"));
    }

    [HttpDelete("api/advocacy-goals/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _goalService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Delete failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Advocacy goal deleted successfully"));
    }

    [HttpPut("api/children/{childId}/advocacy-goals/reorder")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(int childId, [FromBody] ReorderAdvocacyGoalsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var items = request.Items.Select(i => new ReorderAdvocacyGoalItem { Id = i.Id, DisplayOrder = i.DisplayOrder }).ToList();

        var result = await _goalService.ReorderAsync(childId, userId, items, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Reorder failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Goals reordered successfully"));
    }

    private static AdvocacyGoalDto MapToDto(ParentAdvocacyGoalModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        GoalText = model.GoalText,
        Category = model.Category,
        DisplayOrder = model.DisplayOrder,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
