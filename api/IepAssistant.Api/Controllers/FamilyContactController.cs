using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.FamilyContact;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Offline family participation for a student (plan 7, decision 7). Declares only <c>[Authorize]</c> —
/// per-resource authorization (Viewer+ to read, Collaborator+ to write) is enforced inside
/// <see cref="IFamilyContactService"/>.
///
/// Multilingual plan (2026-10-06) phase 5: failures map via the shared
/// <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/>, switching on each result's
/// <see cref="ServiceErrorKind"/> rather than matching (possibly Spanish) message text.
/// </summary>
[ApiController]
[Authorize]
[Route("api/educator/students/{id:int}")]
public class FamilyContactController : ControllerBase
{
    private readonly IFamilyContactService _familyContact;
    private readonly IStringLocalizer<Messages> _localizer;

    public FamilyContactController(IFamilyContactService familyContact, IStringLocalizer<Messages> localizer)
    {
        _familyContact = familyContact;
        _localizer = localizer;
    }

    [HttpGet("contact-attempts")]
    [ProducesResponseType(typeof(ApiResponse<List<FamilyContactAttemptDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContactAttempts(int id, CancellationToken ct)
    {
        var result = await _familyContact.GetContactAttemptsAsync(User.GetUserId(), id, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Ok(ApiResponse<List<FamilyContactAttemptDto>>.SuccessResponse(result.Data!.Select(FamilyContactMappers.MapAttempt).ToList()));
    }

    [HttpPost("contact-attempts")]
    [ProducesResponseType(typeof(ApiResponse<FamilyContactAttemptDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordContactAttempt(int id, [FromBody] CreateFamilyContactAttemptRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Method == null || request.Outcome == null)
            return BadRequest(ApiResponse<object>.Error(_localizer["Api.InvalidRequest"]));

        var result = await _familyContact.RecordContactAttemptAsync(User.GetUserId(), id, new CreateFamilyContactAttemptModel
        {
            AttemptedAt = request.AttemptedAt,
            Method = request.Method.Value,
            Outcome = request.Outcome.Value,
            Note = request.Note
        }, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

        var dto = FamilyContactMappers.MapAttempt(result.Data!);
        return Created($"/api/educator/students/{id}/contact-attempts", ApiResponse<FamilyContactAttemptDto>.SuccessResponse(dto));
    }

    [HttpGet("offline-input")]
    [ProducesResponseType(typeof(ApiResponse<List<OfflineFamilyInputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOfflineInput(int id, CancellationToken ct)
    {
        var result = await _familyContact.GetOfflineInputAsync(User.GetUserId(), id, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Ok(ApiResponse<List<OfflineFamilyInputDto>>.SuccessResponse(result.Data!.Select(FamilyContactMappers.MapInput).ToList()));
    }

    [HttpPost("offline-input")]
    [ProducesResponseType(typeof(ApiResponse<OfflineFamilyInputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordOfflineInput(int id, [FromBody] CreateOfflineFamilyInputRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Method == null)
            return BadRequest(ApiResponse<object>.Error(_localizer["Api.InvalidRequest"]));

        var result = await _familyContact.RecordOfflineInputAsync(User.GetUserId(), id, new CreateOfflineFamilyInputModel
        {
            DocumentInstanceId = request.DocumentInstanceId,
            ReceivedAt = request.ReceivedAt,
            Method = request.Method.Value,
            Summary = request.Summary
        }, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

        var dto = FamilyContactMappers.MapInput(result.Data!);
        return Created($"/api/educator/students/{id}/offline-input", ApiResponse<OfflineFamilyInputDto>.SuccessResponse(dto));
    }
}
