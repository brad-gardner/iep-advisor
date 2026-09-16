using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Drafts;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Staff-side family draft sharing (State Document Template Engine, plan 6): recipient preview, share,
/// share history, withdraw, the converge view, and staff response listing/resolution. Declares only
/// <c>[Authorize]</c> — per-resource authorization (Collaborator+ to mutate, Viewer+ to read) is enforced
/// inside <see cref="IDraftSharingService"/>/<see cref="IDraftResponseService"/>.
/// </summary>
[ApiController]
[Authorize]
public class DraftSharingController : ControllerBase
{
    private readonly IDraftSharingService _sharing;
    private readonly IDraftResponseService _responses;

    public DraftSharingController(IDraftSharingService sharing, IDraftResponseService responses)
    {
        _sharing = sharing;
        _responses = responses;
    }

    [HttpGet("api/documents/{id:int}/share/preview")]
    [ProducesResponseType(typeof(ApiResponse<RecipientPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewRecipients(int id, CancellationToken ct)
    {
        var result = await _sharing.PreviewRecipientsAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<RecipientPreviewDto>.SuccessResponse(DraftSharingMappers.MapPreview(result.Data!)));
    }

    [HttpPost("api/documents/{id:int}/share")]
    [ProducesResponseType(typeof(ApiResponse<SharedDraftRevisionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Share(int id, [FromBody] ShareDraftRequest? request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _sharing.ShareAsync(User.GetUserId(), id, request?.Message, ct);
        if (!result.Success) return MapFailure<object>(result.Message);

        var dto = DraftSharingMappers.MapRevision(result.Data!);
        return CreatedAtAction(nameof(ListShares), new { id }, ApiResponse<SharedDraftRevisionDto>.SuccessResponse(dto));
    }

    [HttpGet("api/documents/{id:int}/shares")]
    [ProducesResponseType(typeof(ApiResponse<List<SharedDraftRevisionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListShares(int id, CancellationToken ct)
    {
        var result = await _sharing.ListForInstanceAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<List<SharedDraftRevisionDto>>.SuccessResponse(result.Data!.Select(DraftSharingMappers.MapRevision).ToList()));
    }

    [HttpPost("api/documents/{id:int}/shares/{rev:int}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<SharedDraftRevisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id, int rev, CancellationToken ct)
    {
        var result = await _sharing.WithdrawAsync(User.GetUserId(), id, rev, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<SharedDraftRevisionDto>.SuccessResponse(DraftSharingMappers.MapRevision(result.Data!)));
    }

    [HttpGet("api/documents/{id:int}/converge")]
    [ProducesResponseType(typeof(ApiResponse<ConvergeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Converge(int id, CancellationToken ct)
    {
        var result = await _sharing.GetConvergeAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<ConvergeDto>.SuccessResponse(DraftSharingMappers.MapConverge(result.Data!)));
    }

    [HttpGet("api/documents/{id:int}/responses")]
    [ProducesResponseType(typeof(ApiResponse<List<DraftResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListResponses(int id, [FromQuery] string? status, CancellationToken ct)
    {
        DraftResponseStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<DraftResponseStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(ApiResponse<object>.Error("Invalid status filter."));
            parsedStatus = parsed;
        }

        var result = await _responses.GetForInstanceAsync(User.GetUserId(), id, parsedStatus, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<List<DraftResponseDto>>.SuccessResponse(result.Data!.Select(DraftSharingMappers.MapResponse).ToList()));
    }

    [HttpPost("api/responses/{id:int}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<DraftResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveResponseRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _responses.ResolveAsync(User.GetUserId(), id, new ResolveDraftResponseModel
        {
            StaffReply = request.StaffReply,
            ResolvedInDraft = request.ResolvedInDraft
        }, ct);
        if (!result.Success) return MapFailure<object>(result.Message);
        return Ok(ApiResponse<DraftResponseDto>.SuccessResponse(DraftSharingMappers.MapResponse(result.Data!)));
    }

    private IActionResult MapFailure<T>(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("disabled for this district", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));
        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
