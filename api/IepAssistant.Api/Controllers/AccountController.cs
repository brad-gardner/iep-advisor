using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IepAssistant.Api.DTOs.Account;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Anonymous account-lifecycle endpoints reachable without a session (pilot-gates plan, phase 2). Kept
/// separate from <see cref="AuthController"/> — which hosts the authenticated
/// <c>POST /api/auth/cancel-deletion</c> — precisely because this controller's whole purpose is to be
/// callable by someone who no longer HAS a valid session (see <see cref="IAccountService.CancelDeletionByTokenAsync"/>).
/// </summary>
[ApiController]
[Route("api/account")]
[AllowAnonymous]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Cancels a pending account deletion using the signed link emailed at request time. The requesting
    /// account is deactivated (and its sessions revoked) the moment deletion is scheduled, so this is the
    /// only reachable way to cancel — see <see cref="IAccountService.CancelDeletionByTokenAsync"/>.
    /// </summary>
    [HttpPost("cancel-deletion")]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelDeletion([FromBody] CancelDeletionByTokenRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _accountService.CancelDeletionByTokenAsync(request.Token, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Unable to cancel deletion"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }
}
