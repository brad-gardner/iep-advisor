using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepAssist;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Educator AI assist for template documents. Inline assists return a suggestion (never auto-applied);
/// chat returns an ephemeral reply. Access is enforced in the service (Collaborator+); failures map via
/// the shared <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/>, switching on each result's
/// <see cref="ServiceErrorKind"/> rather than matching (possibly Spanish) message text.
/// </summary>
[ApiController]
[Authorize]
public class DocumentAssistController : ControllerBase
{
    private readonly IDocumentAssistService _service;
    private readonly IStringLocalizer<Messages> _localizer;

    public DocumentAssistController(IDocumentAssistService service, IStringLocalizer<Messages> localizer)
    {
        _service = service;
        _localizer = localizer;
    }

    [HttpPost("api/documents/{instanceId:int}/assist")]
    [ProducesResponseType(typeof(ApiResponse<AssistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assist(int instanceId, [FromBody] DocumentAssistRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<AssistKind>(request.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            return BadRequest(ApiResponse<object>.Error(_localizer["DocumentAssistApi.InvalidKind"]));

        if (request.FieldKey is not { } fieldKey || fieldKey == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error(_localizer["DocumentAssistApi.FieldKeyRequired"]));

        var result = await _service.AssistAsync(User.GetUserId(), instanceId, fieldKey, request.RowId, kind, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        var data = result.Data!;
        return Ok(ApiResponse<AssistResponse>.SuccessResponse(new AssistResponse
        {
            Suggestion = data.Suggestion,
            Rationale = data.Rationale,
            MissingBaseline = data.MissingBaseline,
            Citations = data.Citations.Select(c => new AssistCitationDto { EvidenceId = c.EvidenceId, SourceLabel = c.SourceLabel, Excerpt = c.Excerpt }).ToList()
        }));
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
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse { Reply = result.Data!.Reply }));
    }
}
