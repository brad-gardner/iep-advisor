using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Staff view of the role-filtered evidence bundle used by prefill and AI assist.
///
/// Multilingual plan (2026-10-06) phase 5: failures map via the shared
/// <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/>, switching on each result's
/// <see cref="ServiceErrorKind"/> rather than matching (possibly Spanish) message text.
/// </summary>
[ApiController]
[Authorize]
public class StudentEvidenceController : ControllerBase
{
    private readonly IStudentEvidenceService _evidence;
    private readonly IStringLocalizer<Messages> _localizer;

    public StudentEvidenceController(IStudentEvidenceService evidence, IStringLocalizer<Messages> localizer)
    {
        _evidence = evidence;
        _localizer = localizer;
    }

    [HttpGet("api/educator/students/{studentId:int}/evidence")]
    [ProducesResponseType(typeof(ApiResponse<StudentEvidenceBundle>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int studentId, CancellationToken ct)
    {
        var result = await _evidence.BuildForStaffAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
        return Ok(ApiResponse<StudentEvidenceBundle>.SuccessResponse(result.Data));
    }
}
