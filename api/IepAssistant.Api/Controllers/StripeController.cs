using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Stripe;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class StripeController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IConfiguration _config;

    public StripeController(ISubscriptionService subscriptionService, IConfiguration config)
    {
        _subscriptionService = subscriptionService;
        _config = config;
    }

    [HttpPost("api/stripe/create-checkout-session")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:5173";
        if (!request.SuccessUrl.StartsWith(frontendUrl, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Invalid redirect URL"));
        if (!request.CancelUrl.StartsWith(frontendUrl, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Invalid redirect URL"));

        try
        {
            var userId = User.GetUserId();
            var url = await _subscriptionService.CreateCheckoutSessionAsync(userId, request.SuccessUrl, request.CancelUrl, ct);
            return Ok(ApiResponse<object>.SuccessResponse(new { url }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }

    [HttpPost("api/stripe/create-portal-session")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePortalSession([FromBody] CreatePortalRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:5173";
        if (!request.ReturnUrl.StartsWith(frontendUrl, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Invalid redirect URL"));

        try
        {
            var userId = User.GetUserId();
            var url = await _subscriptionService.CreatePortalSessionAsync(userId, request.ReturnUrl, ct);
            return Ok(ApiResponse<object>.SuccessResponse(new { url }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }

    [HttpGet("api/subscription/status")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionStatusModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var status = await _subscriptionService.GetStatusAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionStatusModel>.SuccessResponse(status));
    }

    [HttpPost("api/subscription/redeem-invite")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RedeemInvite([FromBody] RedeemInviteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var result = await _subscriptionService.RedeemBetaCodeAsync(userId, request.Code, ct);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to redeem invite code"));

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpPost("api/admin/beta-codes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateBetaCodes([FromBody] GenerateBetaCodesRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _subscriptionService.GenerateBetaCodesAsync(request.Count, request.ExpiresAt, ct);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to generate codes"));

        return Ok(ApiResponse<List<string>>.SuccessResponse(result.Data!, "Beta codes generated"));
    }

    [HttpGet("api/admin/beta-codes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BetaCodeModel>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBetaCodes(CancellationToken ct)
    {
        var codes = await _subscriptionService.ListBetaCodesAsync(ct);
        return Ok(ApiResponse<IEnumerable<BetaCodeModel>>.SuccessResponse(codes));
    }

    [HttpPost("api/admin/invite-beta-user")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteBetaUser([FromBody] InviteBetaUserRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        // Generate 1 beta code
        var result = await _subscriptionService.GenerateBetaCodesAsync(1, null, ct);
        if (!result.Success || result.Data == null || result.Data.Count == 0)
            return BadRequest(ApiResponse<object>.Error("Failed to generate invite code"));

        var code = result.Data[0];

        // Send the invite email with the code baked into the signup link
        var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        await emailService.SendBetaInviteEmailAsync(request.Email, code, ct);

        return Ok(ApiResponse<object>.SuccessResponse(new { code }, $"Beta invite sent to {request.Email}"));
    }
}
