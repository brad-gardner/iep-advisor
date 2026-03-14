using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Auth;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Authenticate user and return JWT token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result == null)
            return Unauthorized(ApiResponse<object>.Error("Invalid email or password"));

        var response = new LoginResponse
        {
            Token = result.Token,
            ExpiresAt = result.ExpiresAt,
            User = new UserDto
            {
                Id = result.User.Id,
                Email = result.User.Email,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                State = result.User.State,
                Role = result.User.Role
            }
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var model = new RegisterModel
        {
            Email = request.Email,
            Password = request.Password,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _authService.RegisterAsync(model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Registration failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "User registered successfully"));
    }

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var user = await _authService.GetUserByIdAsync(userId, cancellationToken);

        if (user == null)
            return NotFound(ApiResponse<object>.Error("User not found"));

        var dto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            State = user.State,
            Role = user.Role
        };

        return Ok(ApiResponse<UserDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var model = new UpdateProfileModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            State = request.State
        };

        var result = await _authService.UpdateProfileAsync(userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Update failed"));

        var user = await _authService.GetUserByIdAsync(userId, cancellationToken);
        var dto = new UserDto
        {
            Id = user!.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            State = user.State,
            Role = user.Role
        };

        return Ok(ApiResponse<UserDto>.SuccessResponse(dto, "Profile updated successfully"));
    }
}
