using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IepAssistant.Api.DTOs.Auth;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Staff;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// P4 anonymous staff-invite accept flow: preview the invite and accept it (create account + sign in).
/// Both endpoints are <c>[AllowAnonymous]</c> and rate-limited with the shared login policy. Accept mints
/// a JWT (same <see cref="LoginResponse"/> shape as login/register-district) so the frontend auto-logs-in.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/staff-invites")]
public class StaffInviteController : ControllerBase
{
    private readonly IStaffInviteService _service;

    public StaffInviteController(IStaffInviteService service)
    {
        _service = service;
    }

    [HttpGet("preview")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<StaffInvitePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Error("Token is required."));

        var preview = await _service.PreviewAsync(token, ct);
        if (preview == null)
            return BadRequest(ApiResponse<object>.Error("Token is required."));

        return Ok(ApiResponse<StaffInvitePreviewDto>.SuccessResponse(new StaffInvitePreviewDto
        {
            DistrictName = preview.DistrictName,
            SchoolName = preview.SchoolName,
            RoleName = preview.RoleName,
            Email = preview.Email,
            Status = preview.Status
        }));
    }

    [HttpPost("accept")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Accept([FromBody] AcceptStaffInviteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AcceptAsync(new AcceptStaffInviteModel
        {
            Token = request.Token,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Password = request.Password
        }, ct);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Could not accept invite."));

        var auth = result.AuthResult!;
        var response = new LoginResponse
        {
            Token = auth.Token,
            ExpiresAt = auth.ExpiresAt,
            User = new UserDto
            {
                Id = auth.User.Id,
                Email = auth.User.Email,
                FirstName = auth.User.FirstName,
                LastName = auth.User.LastName,
                State = auth.User.State,
                Role = auth.User.Role,
                IsActive = auth.User.IsActive,
                OnboardingCompleted = auth.User.OnboardingCompleted,
                CreatedAt = auth.User.CreatedAt
            }
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(response));
    }
}
