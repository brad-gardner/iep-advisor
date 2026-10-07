using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Contributions;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Parent "about my child" notes; shared ones are readable by the linked school team.
///
/// Multilingual plan (2026-10-06) phase 5: failures map via the shared
/// <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/>, switching on each result's
/// <see cref="ServiceErrorKind"/> (which <see cref="ParentContributionService"/> now sets explicitly
/// per call site to match this controller's pre-existing per-endpoint status choices) rather than
/// matching (possibly Spanish) message text.</summary>
[ApiController]
[Authorize]
public class ParentContributionsController : ControllerBase
{
    private readonly IParentContributionService _service;
    private readonly IStringLocalizer<Messages> _localizer;

    public ParentContributionsController(IParentContributionService service, IStringLocalizer<Messages> localizer)
    {
        _service = service;
        _localizer = localizer;
    }

    [HttpGet("api/children/{childId:int}/contributions")]
    public async Task<IActionResult> GetForChild(int childId, CancellationToken ct)
    {
        var result = await _service.GetForChildAsync(childId, User.GetUserId(), ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.NotFound"]);
        return Ok(ApiResponse<List<ParentContributionDto>>.SuccessResponse(result.Data!.Select(Map).ToList()));
    }

    [HttpPost("api/children/{childId:int}/contributions")]
    public async Task<IActionResult> Create(int childId, [FromBody] SaveParentContributionRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error(_localizer["Contributions.InvalidKind"]));
        var result = await _service.CreateAsync(childId, User.GetUserId(), new SaveParentContributionModel { Kind = kind, Text = request.Text, IsShared = request.IsShared }, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Created($"/api/contributions/{result.Data!.Id}", ApiResponse<ParentContributionDto>.SuccessResponse(Map(result.Data)));
    }

    [HttpPut("api/contributions/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveParentContributionRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error(_localizer["Contributions.InvalidKind"]));
        var result = await _service.UpdateAsync(id, User.GetUserId(), new SaveParentContributionModel { Kind = kind, Text = request.Text, IsShared = request.IsShared }, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Ok(ApiResponse<ParentContributionDto>.SuccessResponse(Map(result.Data!)));
    }

    [HttpDelete("api/contributions/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, User.GetUserId(), ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.NotFound"]);
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    /// <summary>Staff: shared family notes for a school student.</summary>
    [HttpGet("api/educator/students/{studentId:int}/contributions")]
    public async Task<IActionResult> GetSharedForStudent(int studentId, CancellationToken ct)
    {
        var result = await _service.GetSharedForSchoolStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
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
