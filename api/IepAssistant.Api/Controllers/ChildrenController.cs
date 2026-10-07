using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Children;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChildrenController : ControllerBase
{
    private readonly IChildProfileService _childProfileService;
    private readonly IAccessService _accessService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ChildrenController(IChildProfileService childProfileService, IAccessService accessService, IStringLocalizer<Messages> localizer)
    {
        _childProfileService = childProfileService;
        _accessService = accessService;
        _localizer = localizer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChildProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profiles = await _childProfileService.GetByUserIdAsync(userId, cancellationToken);
        var dtos = new List<ChildProfileDto>();
        foreach (var profile in profiles)
        {
            var role = await _accessService.GetRoleAsync(profile.Id, userId, cancellationToken);
            dtos.Add(MapToDto(profile, role?.ToString().ToLowerInvariant()));
        }
        return Ok(ApiResponse<IEnumerable<ChildProfileDto>>.SuccessResponse(dtos));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ChildProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await _childProfileService.GetByIdForUserAsync(id, userId, cancellationToken);

        if (profile == null)
            return NotFound(ApiResponse<object>.Error(_localizer["ChildrenApi.NotFound"]));

        var role = await _accessService.GetRoleAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<ChildProfileDto>.SuccessResponse(MapToDto(profile, role?.ToString().ToLowerInvariant())));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ChildProfileDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateChildProfileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var userId = User.GetUserId();
        var model = new CreateChildProfileModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            GradeLevel = request.GradeLevel,
            DisabilityCategory = request.DisabilityCategory,
            SchoolDistrict = request.SchoolDistrict
        };

        var result = await _childProfileService.CreateAsync(userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["ChildrenApi.CreationFailed"].Value));

        var dto = MapToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<ChildProfileDto>.SuccessResponse(dto, result.Message));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateChildProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new UpdateChildProfileModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            GradeLevel = request.GradeLevel,
            DisabilityCategory = request.DisabilityCategory,
            SchoolDistrict = request.SchoolDistrict
        };

        var result = await _childProfileService.UpdateAsync(id, userId, model, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["ChildrenApi.UpdateFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _childProfileService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["ChildrenApi.DeleteFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpPut("{childId}/current-iep/{iepId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCurrentIep(int childId, int iepId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _childProfileService.SetCurrentIepAsync(childId, iepId, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["ChildrenApi.SetCurrentIepFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    private static ChildProfileDto MapToDto(ChildProfileModel model, string? role = null) => new()
    {
        Id = model.Id,
        FirstName = model.FirstName,
        LastName = model.LastName,
        DateOfBirth = model.DateOfBirth,
        GradeLevel = model.GradeLevel,
        DisabilityCategory = model.DisabilityCategory,
        SchoolDistrict = model.SchoolDistrict,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt,
        Role = role,
        CurrentIepDocumentId = model.CurrentIepDocumentId
    };
}
