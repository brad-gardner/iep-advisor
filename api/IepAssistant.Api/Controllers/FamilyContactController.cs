using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.FamilyContact;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Offline family participation for a student (plan 7, decision 7). Declares only <c>[Authorize]</c> —
/// per-resource authorization (Viewer+ to read, Collaborator+ to write) is enforced inside
/// <see cref="IFamilyContactService"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/educator/students/{id:int}")]
public class FamilyContactController : ControllerBase
{
    private readonly IFamilyContactService _familyContact;

    public FamilyContactController(IFamilyContactService familyContact)
    {
        _familyContact = familyContact;
    }

    [HttpGet("contact-attempts")]
    [ProducesResponseType(typeof(ApiResponse<List<FamilyContactAttemptDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContactAttempts(int id, CancellationToken ct)
    {
        var result = await _familyContact.GetContactAttemptsAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<FamilyContactAttemptDto>>.SuccessResponse(result.Data!.Select(FamilyContactMappers.MapAttempt).ToList()));
    }

    [HttpPost("contact-attempts")]
    [ProducesResponseType(typeof(ApiResponse<FamilyContactAttemptDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordContactAttempt(int id, [FromBody] CreateFamilyContactAttemptRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Method == null || request.Outcome == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _familyContact.RecordContactAttemptAsync(User.GetUserId(), id, new CreateFamilyContactAttemptModel
        {
            AttemptedAt = request.AttemptedAt,
            Method = request.Method.Value,
            Outcome = request.Outcome.Value,
            Note = request.Note
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = FamilyContactMappers.MapAttempt(result.Data!);
        return Created($"/api/educator/students/{id}/contact-attempts", ApiResponse<FamilyContactAttemptDto>.SuccessResponse(dto));
    }

    [HttpGet("offline-input")]
    [ProducesResponseType(typeof(ApiResponse<List<OfflineFamilyInputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOfflineInput(int id, CancellationToken ct)
    {
        var result = await _familyContact.GetOfflineInputAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<List<OfflineFamilyInputDto>>.SuccessResponse(result.Data!.Select(FamilyContactMappers.MapInput).ToList()));
    }

    [HttpPost("offline-input")]
    [ProducesResponseType(typeof(ApiResponse<OfflineFamilyInputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordOfflineInput(int id, [FromBody] CreateOfflineFamilyInputRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Method == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _familyContact.RecordOfflineInputAsync(User.GetUserId(), id, new CreateOfflineFamilyInputModel
        {
            DocumentInstanceId = request.DocumentInstanceId,
            ReceivedAt = request.ReceivedAt,
            Method = request.Method.Value,
            Summary = request.Summary
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = FamilyContactMappers.MapInput(result.Data!);
        return Created($"/api/educator/students/{id}/offline-input", ApiResponse<OfflineFamilyInputDto>.SuccessResponse(dto));
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
