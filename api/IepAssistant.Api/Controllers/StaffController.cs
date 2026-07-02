using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Staff;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// P4 staff management (authenticated). Org authorization is resolved per-request server-side from the
/// caller's active StaffProfile via <c>IStaffInviteService</c>/<c>IOrgAccessService</c> — never from the
/// JWT role claim. Anonymous preview/accept lives on <see cref="StaffInviteController"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/district/staff")]
public class StaffController : ControllerBase
{
    private readonly IStaffInviteService _service;

    public StaffController(IStaffInviteService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<StaffListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaff(CancellationToken ct)
    {
        var result = await _service.ListAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<StaffListDto>.SuccessResponse(MapList(result.Data!)));
    }

    [HttpPost("invites")]
    [ProducesResponseType(typeof(ApiResponse<StaffInviteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInvite([FromBody] CreateStaffInviteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.InviteAsync(User.GetUserId(), new CreateStaffInviteModel
        {
            Email = request.Email,
            OrgRoleId = request.OrgRoleId,
            SchoolId = request.SchoolId
        }, ct);

        if (!result.Success)
            return MapFailure(result.Message);

        return CreatedAtAction(nameof(GetStaff), new { }, ApiResponse<StaffInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message));
    }

    [HttpDelete("invites/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvite(int id, CancellationToken ct)
    {
        var result = await _service.RevokeAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpPost("invites/{id}/resend")]
    [ProducesResponseType(typeof(ApiResponse<StaffInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendInvite(int id, CancellationToken ct)
    {
        var result = await _service.ResendAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<StaffInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message));
    }

    [HttpPost("{staffProfileId}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<DeactivateStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateStaff(int staffProfileId, CancellationToken ct)
    {
        var result = await _service.DeactivateStaffAsync(User.GetUserId(), staffProfileId, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        var data = result.Data!;
        return Ok(ApiResponse<DeactivateStaffResponseDto>.SuccessResponse(new DeactivateStaffResponseDto
        {
            SolelyOwnedStudentCount = data.SolelyOwnedStudentCount,
            SolelyOwnedStudents = data.SolelyOwnedStudents
                .Select(s => new DeactivatedStaffStudentDto { StudentId = s.StudentId, Name = s.Name })
                .ToList()
        }, result.Message));
    }

    [HttpPost("{staffProfileId}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateStaff(int staffProfileId, CancellationToken ct)
    {
        var result = await _service.ReactivateStaffAsync(User.GetUserId(), staffProfileId, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    // ----------------------------------------------------------------- mapping

    private static StaffInviteDto MapInvite(StaffInviteModel m) => new()
    {
        Id = m.Id,
        Email = m.Email,
        OrgRoleId = m.OrgRoleId,
        OrgRoleName = m.OrgRoleName,
        SchoolId = m.SchoolId,
        SchoolName = m.SchoolName,
        InviteExpiresAt = m.InviteExpiresAt,
        InviteUrl = m.InviteUrl
    };

    private static StaffListDto MapList(StaffListModel m) => new()
    {
        Members = m.Members.Select(x => new StaffMemberDto
        {
            StaffProfileId = x.StaffProfileId,
            UserId = x.UserId,
            FirstName = x.FirstName,
            LastName = x.LastName,
            Email = x.Email,
            OrgRoleId = x.OrgRoleId,
            OrgRoleName = x.OrgRoleName,
            SchoolId = x.SchoolId,
            SchoolName = x.SchoolName,
            IsActive = x.IsActive
        }).ToList(),
        PendingInvites = m.PendingInvites.Select(x => new StaffPendingInviteDto
        {
            Id = x.Id,
            Email = x.Email,
            OrgRoleId = x.OrgRoleId,
            OrgRoleName = x.OrgRoleName,
            SchoolId = x.SchoolId,
            SchoolName = x.SchoolName,
            InviteExpiresAt = x.InviteExpiresAt,
            Status = x.Status
        }).ToList()
    };

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
