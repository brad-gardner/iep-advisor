using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Contributions;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Parent "about my child" notes; shared ones are readable by the linked school team.</summary>
[ApiController]
[Authorize]
public class ParentContributionsController : ControllerBase
{
    private readonly IParentContributionService _service;

    public ParentContributionsController(IParentContributionService service)
    {
        _service = service;
    }

    [HttpGet("api/children/{childId:int}/contributions")]
    public async Task<IActionResult> GetForChild(int childId, CancellationToken ct)
    {
        var result = await _service.GetForChildAsync(childId, User.GetUserId(), ct);
        if (!result.Success) return NotFound(ApiResponse<object>.Error(result.Message ?? "Not found"));
        return Ok(ApiResponse<List<ParentContributionDto>>.SuccessResponse(result.Data!.Select(Map).ToList()));
    }

    [HttpPost("api/children/{childId:int}/contributions")]
    public async Task<IActionResult> Create(int childId, [FromBody] SaveParentContributionRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error("Invalid kind."));
        var result = await _service.CreateAsync(childId, User.GetUserId(), new SaveParentContributionModel { Kind = kind, Text = request.Text, IsShared = request.IsShared }, ct);
        if (!result.Success) return BadRequest(ApiResponse<object>.Error(result.Message ?? "Creation failed"));
        return Created($"/api/contributions/{result.Data!.Id}", ApiResponse<ParentContributionDto>.SuccessResponse(Map(result.Data)));
    }

    [HttpPut("api/contributions/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveParentContributionRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error("Invalid kind."));
        var result = await _service.UpdateAsync(id, User.GetUserId(), new SaveParentContributionModel { Kind = kind, Text = request.Text, IsShared = request.IsShared }, ct);
        if (!result.Success)
            return result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(ApiResponse<object>.Error(result.Message))
                : BadRequest(ApiResponse<object>.Error(result.Message ?? "Update failed"));
        return Ok(ApiResponse<ParentContributionDto>.SuccessResponse(Map(result.Data!)));
    }

    [HttpDelete("api/contributions/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, User.GetUserId(), ct);
        if (!result.Success) return NotFound(ApiResponse<object>.Error(result.Message ?? "Not found"));
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    /// <summary>Staff: shared family notes for a school student.</summary>
    [HttpGet("api/educator/students/{studentId:int}/contributions")]
    public async Task<IActionResult> GetSharedForStudent(int studentId, CancellationToken ct)
    {
        var result = await _service.GetSharedForSchoolStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return StatusCode(403, ApiResponse<object>.Error(result.Message ?? "Forbidden"));
        return Ok(ApiResponse<List<ParentContributionDto>>.SuccessResponse(result.Data!.Select(Map).ToList()));
    }

    private static bool TryParseKind(string value, out ParentContributionKind kind)
        => Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    private static ParentContributionDto Map(ParentContributionModel m) => new()
    {
        Id = m.Id, ChildProfileId = m.ChildProfileId, Kind = m.Kind.ToString(), Text = m.Text, IsShared = m.IsShared,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };
}
