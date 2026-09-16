using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>Computed procedural deadlines (plan 4, decision 2) — read-only, never persisted.</summary>
[ApiController]
[Authorize]
[Route("api/educator")]
public class ObligationsController : ControllerBase
{
    private readonly IObligationService _obligationService;

    public ObligationsController(IObligationService obligationService)
    {
        _obligationService = obligationService;
    }

    [HttpGet("obligations/mine")]
    [ProducesResponseType(typeof(ApiResponse<List<ObligationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] ObligationStatus? status, CancellationToken ct)
    {
        var result = await _obligationService.GetMineAsync(User.GetUserId(), status, ct);
        if (!result.Success)
            return MapFailure<List<ObligationDto>>(result.Message);

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
            return MapFailure<List<ObligationDto>>(result.Message);

        return Ok(ApiResponse<List<ObligationDto>>.SuccessResponse(result.Data!.Select(ObligationDtoMapper.Map).ToList()));
    }

    [HttpGet("obligations")]
    [ProducesResponseType(typeof(ApiResponse<List<ObligationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForScope([FromQuery] int? schoolId, [FromQuery] ObligationStatus? status, CancellationToken ct)
    {
        var result = await _obligationService.GetForScopeAsync(User.GetUserId(), schoolId, status, ct);
        if (!result.Success)
            return MapFailure<List<ObligationDto>>(result.Message);

        return Ok(ApiResponse<List<ObligationDto>>.SuccessResponse(result.Data!.Select(ObligationDtoMapper.Map).ToList()));
    }

    private IActionResult MapFailure<T>(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
