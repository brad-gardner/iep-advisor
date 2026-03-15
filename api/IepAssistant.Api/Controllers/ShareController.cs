using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Share;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class ShareController : ControllerBase
{
    private readonly IShareService _shareService;

    public ShareController(IShareService shareService)
    {
        _shareService = shareService;
    }

    [HttpPost("api/children/{childId}/share")]
    [ProducesResponseType(typeof(ApiResponse<ChildAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Invite(int childId, [FromBody] CreateInviteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        if (!Enum.TryParse<AccessRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(ApiResponse<object>.Error("Invalid role. Must be 'Viewer' or 'Collaborator'."));

        var userId = User.GetUserId();
        var result = await _shareService.InviteAsync(childId, userId, request.Email, role, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Invite failed"));

        var dto = MapToDto(result.Data!);
        return Ok(ApiResponse<ChildAccessDto>.SuccessResponse(dto, result.Message));
    }

    [HttpGet("api/children/{childId}/access")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChildAccessDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccessList(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessList = await _shareService.GetAccessListAsync(childId, userId, cancellationToken);
        var dtos = accessList.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<ChildAccessDto>>.SuccessResponse(dtos));
    }

    [HttpDelete("api/children/{childId}/access/{accessId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeAccess(int childId, int accessId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _shareService.RevokeAccessAsync(childId, accessId, userId, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Revoke failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpPost("api/invites/accept")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var result = await _shareService.AcceptInviteAsync(userId, request.Token, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Accept failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    private static ChildAccessDto MapToDto(ChildAccessModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        UserId = model.UserId,
        UserEmail = model.UserEmail,
        UserName = model.UserName,
        InviteEmail = model.InviteEmail,
        Role = model.Role,
        AcceptedAt = model.AcceptedAt,
        IsPending = model.IsPending,
        CreatedAt = model.CreatedAt
    };
}
