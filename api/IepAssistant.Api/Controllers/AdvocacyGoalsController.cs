using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.AdvocacyGoals;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class AdvocacyGoalsController : ControllerBase
{
    private readonly IParentAdvocacyGoalService _goalService;
    private readonly IStringLocalizer<Messages> _localizer;

    public AdvocacyGoalsController(IParentAdvocacyGoalService goalService, IStringLocalizer<Messages> localizer)
    {
        _goalService = goalService;
        _localizer = localizer;
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
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var userId = User.GetUserId();
        var model = new CreateAdvocacyGoalModel
        {
            GoalText = request.GoalText,
            Category = request.Category
        };

        var result = await _goalService.CreateAsync(childId, userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.CreationFailed"].Value));

        var dto = MapToDto(result.Data!);
        return Created($"/api/advocacy-goals/{dto.Id}", ApiResponse<AdvocacyGoalDto>.SuccessResponse(dto, _localizer["AdvocacyGoalsApi.Created"]));
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

        // Multilingual plan Phase 3: status came from matching translated text ("not found"); the service
        // now sets ErrorKind.NotFound/Validation explicitly, so use the shared kind-based mapper instead.
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["DocumentsApi.UpdateFailed"]);

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["AdvocacyGoalsApi.Updated"]));
    }

    [HttpDelete("api/advocacy-goals/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _goalService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.DeleteFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["AdvocacyGoalsApi.Deleted"]));
    }

    [HttpPut("api/children/{childId}/advocacy-goals/reorder")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(int childId, [FromBody] ReorderAdvocacyGoalsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var userId = User.GetUserId();
        var items = request.Items.Select(i => new ReorderAdvocacyGoalItem { Id = i.Id, DisplayOrder = i.DisplayOrder }).ToList();

        var result = await _goalService.ReorderAsync(childId, userId, items, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["AdvocacyGoalsApi.ReorderFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["AdvocacyGoalsApi.Reordered"]));
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
