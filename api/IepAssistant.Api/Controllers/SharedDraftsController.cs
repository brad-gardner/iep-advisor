using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Drafts;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Parent-side shared drafts (State Document Template Engine, plan 6): the reading list, one revision's
/// frozen values, cached AI explanations, private Q&amp;A (never staff-visible), per-item responses, and
/// the "reviewed" acknowledgement. Declares only <c>[Authorize]</c> — per-resource authorization (an
/// accepted, active <c>ChildLink</c> via <see cref="IAccessService"/>) is enforced inside each service.
/// </summary>
[ApiController]
[Authorize]
public class SharedDraftsController : ControllerBase
{
    private readonly IDraftSharingService _sharing;
    private readonly IDraftExplanationService _explanations;
    private readonly IDraftQuestionService _questions;
    private readonly IDraftResponseService _responses;

    public SharedDraftsController(
        IDraftSharingService sharing,
        IDraftExplanationService explanations,
        IDraftQuestionService questions,
        IDraftResponseService responses)
    {
        _sharing = sharing;
        _explanations = explanations;
        _questions = questions;
        _responses = responses;
    }

    [HttpGet("api/children/{childId:int}/shared-drafts")]
    [ProducesResponseType(typeof(ApiResponse<List<SharedDraftRevisionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForChild(int childId, CancellationToken ct)
    {
        var result = await _sharing.ListForParentAsync(User.GetUserId(), childId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<SharedDraftRevisionDto>>.SuccessResponse(result.Data!.Select(DraftSharingMappers.MapRevision).ToList()));
    }

    [HttpGet("api/shared-drafts/{rev:int}")]
    [ProducesResponseType(typeof(ApiResponse<SharedDraftRevisionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int rev, CancellationToken ct)
    {
        var result = await _sharing.GetForParentAsync(User.GetUserId(), rev, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<SharedDraftRevisionDetailDto>.SuccessResponse(DraftSharingMappers.MapDetail(result.Data!)));
    }

    [HttpGet("api/shared-drafts/{rev:int}/explanations")]
    [ProducesResponseType(typeof(ApiResponse<DraftExplanationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetExplanations(int rev, CancellationToken ct)
    {
        var result = await _explanations.GetOrGenerateAsync(User.GetUserId(), rev, ct);
        if (!result.Success) return MapExplanationFailure(result.Message);
        return Ok(ApiResponse<DraftExplanationDto>.SuccessResponse(DraftSharingMappers.MapExplanation(result.Data!)));
    }

    [HttpPost("api/shared-drafts/{rev:int}/ask")]
    [ProducesResponseType(typeof(ApiResponse<DraftAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ask(int rev, [FromBody] AskQuestionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _questions.AskAsync(User.GetUserId(), rev, new AskDraftQuestionModel
        {
            Question = request.Question,
            TargetFieldKey = request.TargetFieldKey,
            TargetRowId = request.TargetRowId
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<DraftAnswerDto>.SuccessResponse(DraftSharingMappers.MapAnswer(result.Data!)));
    }

    [HttpGet("api/shared-drafts/{rev:int}/notes")]
    [ProducesResponseType(typeof(ApiResponse<List<ParentDraftNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotes(int rev, CancellationToken ct)
    {
        var result = await _questions.GetNotesAsync(User.GetUserId(), rev, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<ParentDraftNoteDto>>.SuccessResponse(result.Data!.Select(DraftSharingMappers.MapNote).ToList()));
    }

    [HttpDelete("api/notes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNote(int id, CancellationToken ct)
    {
        var result = await _questions.DeleteNoteAsync(User.GetUserId(), id, ct);
        if (!result.Success) return NotFound(ApiResponse<object>.Error(result.Message ?? "Not found"));
        return NoContent();
    }

    [HttpPost("api/shared-drafts/{rev:int}/responses")]
    [ProducesResponseType(typeof(ApiResponse<DraftResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateResponse(int rev, [FromBody] CreateResponseRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _responses.CreateAsync(User.GetUserId(), rev, new CreateDraftResponseModel
        {
            Kind = request.Kind,
            Text = request.Text,
            TargetFieldKey = request.TargetFieldKey,
            TargetRowId = request.TargetRowId
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = DraftSharingMappers.MapResponse(result.Data!);
        return Created($"/api/shared-drafts/{rev}/responses", ApiResponse<DraftResponseDto>.SuccessResponse(dto));
    }

    [HttpGet("api/shared-drafts/{rev:int}/responses")]
    [ProducesResponseType(typeof(ApiResponse<List<DraftResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResponses(int rev, CancellationToken ct)
    {
        var result = await _responses.GetForParentAsync(User.GetUserId(), rev, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<DraftResponseDto>>.SuccessResponse(result.Data!.Select(DraftSharingMappers.MapResponse).ToList()));
    }

    [HttpPost("api/shared-drafts/{rev:int}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<SharedDraftRevisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(int rev, CancellationToken ct)
    {
        var result = await _sharing.AcknowledgeAsync(User.GetUserId(), rev, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<SharedDraftRevisionDto>.SuccessResponse(DraftSharingMappers.MapRevision(result.Data!)));
    }

    private IActionResult MapExplanationFailure(string? message)
    {
        if (message != null && message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Error(message));
        return MapFailure(message);
    }

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
