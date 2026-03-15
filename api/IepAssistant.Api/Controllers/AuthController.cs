using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IMfaService _mfaService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IAccountService _accountService;

    public AuthController(IAuthService authService, IMfaService mfaService, IPasswordResetService passwordResetService, IAccountService accountService)
    {
        _authService = authService;
        _mfaService = mfaService;
        _passwordResetService = passwordResetService;
        _accountService = accountService;
    }

    /// <summary>
    /// Authenticate user and return JWT token (or MFA pending token if MFA enabled)
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result == null)
            return Unauthorized(ApiResponse<object>.Error("Invalid email or password"));

        if (result.RequiresMfa)
        {
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                requiresMfa = true,
                mfaPendingToken = result.MfaPendingToken
            }));
        }

        var authResult = result.AuthResult!;
        var response = new LoginResponse
        {
            Token = authResult.Token,
            ExpiresAt = authResult.ExpiresAt,
            User = new UserDto
            {
                Id = authResult.User.Id,
                Email = authResult.User.Email,
                FirstName = authResult.User.FirstName,
                LastName = authResult.User.LastName,
                State = authResult.User.State,
                Role = authResult.User.Role,
                IsActive = authResult.User.IsActive,
                OnboardingCompleted = authResult.User.OnboardingCompleted,
                CreatedAt = authResult.User.CreatedAt
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
            LastName = request.LastName,
            InviteCode = request.InviteCode
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
            Role = user.Role,
            IsActive = user.IsActive,
            OnboardingCompleted = user.OnboardingCompleted,
            CreatedAt = user.CreatedAt
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
            Role = user.Role,
            IsActive = user.IsActive,
            OnboardingCompleted = user.OnboardingCompleted,
            CreatedAt = user.CreatedAt
        };

        return Ok(ApiResponse<UserDto>.SuccessResponse(dto, "Profile updated successfully"));
    }

    /// <summary>
    /// Initiate MFA setup — returns otpauth URI and manual entry key
    /// </summary>
    [Authorize]
    [HttpPost("mfa/setup")]
    [ProducesResponseType(typeof(ApiResponse<MfaSetupResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MfaSetup(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var result = await _mfaService.SetupAsync(userId, cancellationToken);
        return Ok(ApiResponse<MfaSetupResult>.SuccessResponse(result));
    }

    /// <summary>
    /// Verify MFA setup with a TOTP code — enables MFA and returns recovery codes
    /// </summary>
    [Authorize]
    [HttpPost("mfa/verify-setup")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MfaVerifySetup([FromBody] MfaVerifySetupRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var result = await _mfaService.VerifySetupAsync(userId, request.Code, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Verification failed"));

        return Ok(ApiResponse<List<string>>.SuccessResponse(result.Data!, result.Message));
    }

    /// <summary>
    /// Verify MFA code during login — returns full JWT
    /// </summary>
    [AllowAnonymous]
    [HttpPost("mfa/verify")]
    [EnableRateLimiting("mfa")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MfaVerify([FromBody] MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        var userId = _authService.ValidateMfaPendingToken(request.MfaPendingToken);
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Error("Invalid or expired MFA token"));

        var valid = await _mfaService.ValidateCodeAsync(userId.Value, request.Code, cancellationToken);
        if (!valid)
            return Unauthorized(ApiResponse<object>.Error("Invalid MFA code"));

        var authResult = await _authService.CompleteMfaLoginAsync(userId.Value, cancellationToken);
        if (authResult == null)
            return Unauthorized(ApiResponse<object>.Error("Login failed"));

        var response = new LoginResponse
        {
            Token = authResult.Token,
            ExpiresAt = authResult.ExpiresAt,
            User = new UserDto
            {
                Id = authResult.User.Id,
                Email = authResult.User.Email,
                FirstName = authResult.User.FirstName,
                LastName = authResult.User.LastName,
                State = authResult.User.State,
                Role = authResult.User.Role,
                IsActive = authResult.User.IsActive,
                OnboardingCompleted = authResult.User.OnboardingCompleted,
                CreatedAt = authResult.User.CreatedAt
            }
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Use a recovery code during login — returns full JWT
    /// </summary>
    [AllowAnonymous]
    [HttpPost("mfa/recovery")]
    [EnableRateLimiting("mfa")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MfaRecovery([FromBody] MfaRecoveryRequest request, CancellationToken cancellationToken)
    {
        var userId = _authService.ValidateMfaPendingToken(request.MfaPendingToken);
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Error("Invalid or expired MFA token"));

        var valid = await _mfaService.ValidateRecoveryCodeAsync(userId.Value, request.RecoveryCode, cancellationToken);
        if (!valid)
            return Unauthorized(ApiResponse<object>.Error("Invalid recovery code"));

        var authResult = await _authService.CompleteMfaLoginAsync(userId.Value, cancellationToken);
        if (authResult == null)
            return Unauthorized(ApiResponse<object>.Error("Login failed"));

        var response = new LoginResponse
        {
            Token = authResult.Token,
            ExpiresAt = authResult.ExpiresAt,
            User = new UserDto
            {
                Id = authResult.User.Id,
                Email = authResult.User.Email,
                FirstName = authResult.User.FirstName,
                LastName = authResult.User.LastName,
                State = authResult.User.State,
                Role = authResult.User.Role,
                IsActive = authResult.User.IsActive,
                OnboardingCompleted = authResult.User.OnboardingCompleted,
                CreatedAt = authResult.User.CreatedAt
            }
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Request a password reset email
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        await _passwordResetService.InitiateResetAsync(request.Email, cancellationToken);

        // Always return 202 regardless of whether the email exists
        return Accepted(ApiResponse<object>.SuccessResponse(null, "If an account with that email exists, a reset link has been sent."));
    }

    /// <summary>
    /// Reset password using a reset token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Password reset failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    /// <summary>
    /// Disable MFA — requires password and current TOTP code
    /// </summary>
    [Authorize]
    [HttpPost("mfa/disable")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MfaDisable([FromBody] MfaDisableRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var result = await _mfaService.DisableAsync(userId, request.Password, request.Code, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to disable MFA"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    /// <summary>
    /// Export all user data as JSON
    /// </summary>
    [Authorize]
    [HttpGet("data-export")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DataExport(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var data = await _accountService.ExportDataAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(data));
    }

    /// <summary>
    /// Schedule account for deletion (30-day grace period)
    /// </summary>
    [Authorize]
    [HttpPost("delete-account")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _accountService.ScheduleDeletionAsync(userId, request.Password, request.MfaCode, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to schedule deletion"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    /// <summary>
    /// Cancel a pending account deletion
    /// </summary>
    [Authorize]
    [HttpPost("cancel-deletion")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelDeletion(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var result = await _accountService.CancelDeletionAsync(userId, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to cancel deletion"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    /// <summary>
    /// Mark onboarding as completed
    /// </summary>
    [Authorize]
    [HttpPost("complete-onboarding")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        var result = await _authService.CompleteOnboardingAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }
}
