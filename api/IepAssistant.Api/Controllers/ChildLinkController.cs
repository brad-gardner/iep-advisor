using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.ChildLinks;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/child-links")]
public class ChildLinkController : ControllerBase
{
    private readonly IChildLinkService _childLinkService;
    private readonly IFeatureFlags _featureFlags;

    public ChildLinkController(IChildLinkService childLinkService, IFeatureFlags featureFlags)
    {
        _childLinkService = childLinkService;
        _featureFlags = featureFlags;
    }

    [HttpGet("preview")]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkInvitePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Error("Token is required."));

        var result = await _childLinkService.PreviewInviteAsync(User.GetUserId(), token, ct);

        if (!result.Success)
            return MapFailure<ChildLinkInvitePreviewDto>(result.Message);

        var d = result.Data!;
        return Ok(ApiResponse<ChildLinkInvitePreviewDto>.SuccessResponse(new ChildLinkInvitePreviewDto
        {
            SchoolStudentId = d.SchoolStudentId,
            StudentFirstName = d.StudentFirstName,
            StudentLastName = d.StudentLastName,
            SchoolName = d.SchoolName,
            ExistingChildren = d.ExistingChildren.Select(c => new LinkableChildDto
            {
                ChildProfileId = c.ChildProfileId,
                FirstName = c.FirstName,
                LastName = c.LastName
            }).ToList()
        }));
    }

    [HttpPost("accept")]
    [ProducesResponseType(typeof(ApiResponse<AcceptedChildLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Accept([FromBody] AcceptChildLinkRequest request, CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _childLinkService.AcceptInviteAsync(
            User.GetUserId(), request.Token, request.LinkToChildProfileId, ct);

        if (!result.Success)
            return MapFailure<AcceptedChildLinkDto>(result.Message);

        var d = result.Data!;
        return Ok(ApiResponse<AcceptedChildLinkDto>.SuccessResponse(new AcceptedChildLinkDto
        {
            Id = d.Id,
            SchoolStudentId = d.SchoolStudentId,
            ChildProfileId = d.ChildProfileId,
            IsAccepted = d.IsAccepted,
            LinkedAt = d.LinkedAt
        }, result.Message));
    }

    private IActionResult MapFailure<T>(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        // "Invalid or expired", "different email address", etc. -> 400.
        return BadRequest(ApiResponse<object>.Error(message));
    }
}
