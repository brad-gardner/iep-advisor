using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepAssist;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Educator AI assist for template documents. Inline assists return a suggestion (never auto-applied);
/// chat returns an ephemeral reply. Access is enforced in the service (Collaborator+); failures map
/// permission→403, not-found→404, "temporarily unavailable"→503, else 400.
/// </summary>
[ApiController]
[Authorize]
public class DocumentAssistController : ControllerBase
{
    private readonly IDocumentAssistService _service;

    public DocumentAssistController(IDocumentAssistService service)
    {
        _service = service;
    }

    [HttpPost("api/documents/{instanceId:int}/assist")]
    [ProducesResponseType(typeof(ApiResponse<AssistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assist(int instanceId, [FromBody] DocumentAssistRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<AssistKind>(request.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            return BadRequest(ApiResponse<object>.Error("Invalid kind."));

        if (request.FieldKey is not { } fieldKey || fieldKey == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("fieldKey is required."));

        var result = await _service.AssistAsync(User.GetUserId(), instanceId, fieldKey, request.RowId, kind, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<AssistResponse>.SuccessResponse(new AssistResponse { Suggestion = result.Data!.Suggestion }));
    }

    [HttpPost("api/documents/{instanceId:int}/chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(int instanceId, [FromBody] ChatRequest request, CancellationToken ct)
    {
        var messages = request.Messages
            .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
            .ToList();

        var result = await _service.ChatAsync(User.GetUserId(), instanceId, messages, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse { Reply = result.Data!.Reply }));
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
