using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepAssist;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Educator AI assist (P6b). Inline assists return a suggestion (never auto-applied); chat returns
/// an ephemeral reply. Access is
/// enforced in the service (Collaborator+ SchoolStudentAccess); failures map permission→403,
/// not-found→404, "temporarily unavailable"→503, else 400.
/// </summary>
[ApiController]
[Authorize]
public class IepAssistController : ControllerBase
{
    private readonly IIepAssistService _service;

    public IepAssistController(IIepAssistService service)
    {
        _service = service;
    }

    [HttpPost("api/iep-drafts/{draftId}/goals/{goalId}/assist")]
    [ProducesResponseType(typeof(ApiResponse<AssistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssistGoal(int draftId, int goalId, [FromBody] AssistRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error("Invalid kind."));

        var result = await _service.AssistGoalAsync(User.GetUserId(), draftId, goalId, kind, ct);
        return MapAssist(result);
    }

    [HttpPost("api/iep-drafts/{draftId}/sections/{sectionId}/assist")]
    [ProducesResponseType(typeof(ApiResponse<AssistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssistSection(int draftId, int sectionId, [FromBody] AssistRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error("Invalid kind."));

        var result = await _service.AssistSectionAsync(User.GetUserId(), draftId, sectionId, kind, ct);
        return MapAssist(result);
    }

    [HttpPost("api/iep-drafts/{draftId}/service-lines/{serviceLineId}/assist")]
    [ProducesResponseType(typeof(ApiResponse<AssistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssistServiceLine(int draftId, int serviceLineId, [FromBody] AssistRequest request, CancellationToken ct)
    {
        if (!TryParseKind(request.Kind, out var kind)) return BadRequest(ApiResponse<object>.Error("Invalid kind."));

        var result = await _service.AssistServiceLineAsync(User.GetUserId(), draftId, serviceLineId, kind, ct);
        return MapAssist(result);
    }

    [HttpPost("api/iep-drafts/{draftId}/chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(int draftId, [FromBody] ChatRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var messages = (request.Messages ?? new List<ChatMessageDto>())
            .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
            .ToList();

        var result = await _service.ChatAsync(User.GetUserId(), draftId, messages, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse { Reply = result.Data!.Reply }));
    }

    // ---------------------------------------------------------------- Helpers

    private static bool TryParseKind(string? value, out AssistKind kind)
        => Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    private IActionResult MapAssist(ServiceResult<AssistResultModel> result)
    {
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<AssistResponse>.SuccessResponse(new AssistResponse { Suggestion = result.Data!.Suggestion }));
    }

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        if (message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase))
            return StatusCode(503, ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
