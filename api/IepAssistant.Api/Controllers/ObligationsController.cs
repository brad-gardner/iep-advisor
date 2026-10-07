using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>Computed procedural deadlines (plan 4, decision 2) — read-only, never persisted.
///
/// Multilingual plan (2026-10-06) phase 5: failures map via the shared
/// <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/>, switching on each result's
/// <see cref="Services.Models.ServiceErrorKind"/> rather than matching (possibly Spanish) message text.
/// </summary>
[ApiController]
[Authorize]
[Route("api/educator")]
public class ObligationsController : ControllerBase
{
    private readonly IObligationService _obligationService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ObligationsController(IObligationService obligationService, IStringLocalizer<Messages> localizer)
    {
        _obligationService = obligationService;
        _localizer = localizer;
    }

    [HttpGet("obligations/mine")]
    [ProducesResponseType(typeof(ApiResponse<List<ObligationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] ObligationStatus? status, CancellationToken ct)
    {
        var result = await _obligationService.GetMineAsync(User.GetUserId(), status, ct);
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

        return Ok(ApiResponse<List<ObligationDto>>.SuccessResponse(result.Data!.Select(ObligationDtoMapper.Map).ToList()));
    }

    [HttpGet("students/{studentId:int}/obligations")]
    [ProducesResponseType(typeof(ApiResponse<List<ObligationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForStudent(int studentId, CancellationToken ct)
    {
        var result = await _obligationService.GetForStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

        return Ok(ApiResponse<List<ObligationDto>>.SuccessResponse(result.Data!.Select(ObligationDtoMapper.Map).ToList()));
    }

    [HttpGet("obligations")]
    [ProducesResponseType(typeof(ApiResponse<List<ObligationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForScope([FromQuery] int? schoolId, [FromQuery] ObligationStatus? status, CancellationToken ct)
    {
        var result = await _obligationService.GetForScopeAsync(User.GetUserId(), schoolId, status, ct);
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

        return Ok(ApiResponse<List<ObligationDto>>.SuccessResponse(result.Data!.Select(ObligationDtoMapper.Map).ToList()));
    }
}
