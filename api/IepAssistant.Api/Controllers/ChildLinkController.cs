using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.ChildLinks;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/child-links")]
public class ChildLinkController : ControllerBase
{
    private readonly IChildLinkService _childLinkService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ChildLinkController(IChildLinkService childLinkService, IStringLocalizer<Messages> localizer)
    {
        _childLinkService = childLinkService;
        _localizer = localizer;
    }

    [HttpGet("preview")]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkInvitePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Error(_localizer["ChildLinksApi.TokenRequired"]));

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
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

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

    [HttpGet("/api/children/{childId}/school-links")]
    [ProducesResponseType(typeof(ApiResponse<List<ChildSchoolLinkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildSchoolLinks(int childId, CancellationToken ct)
    {
        var result = await _childLinkService.GetChildSchoolLinksAsync(User.GetUserId(), childId, ct);
        if (!result.Success)
            return MapFailure<List<ChildSchoolLinkDto>>(result.Message);

        var dtos = result.Data!.Select(l => new ChildSchoolLinkDto
        {
            Id = l.Id,
            SchoolStudentId = l.SchoolStudentId,
            SchoolName = l.SchoolName,
            StudentFirstName = l.StudentFirstName,
            StudentLastName = l.StudentLastName,
            LinkedAt = l.LinkedAt
        }).ToList();
        return Ok(ApiResponse<List<ChildSchoolLinkDto>>.SuccessResponse(dtos));
    }

    private IActionResult MapFailure<T>(string? message)
    {
        message ??= _localizer["Api.RequestFailed"].Value;

        // Status routing is keyed off substrings of the (now-localized) message text. The Spanish
        // translations of every "no permission"/"not found" message in this controller's services
        // deliberately include "permiso"/"no encontrad" (stem covers both "encontrado"/"encontrada"
        // grammatical gender) for exactly this reason — see Messages.es.resx (ChildLinks.* area).
        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permiso", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no encontrad", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        // "Invalid or expired", "different email address", etc. -> 400.
        return BadRequest(ApiResponse<object>.Error(message));
    }
}
