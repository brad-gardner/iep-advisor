using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Staff view of the role-filtered evidence bundle used by prefill and AI assist.</summary>
[ApiController]
[Authorize]
public class StudentEvidenceController : ControllerBase
{
    private readonly IStudentEvidenceService _evidence;

    public StudentEvidenceController(IStudentEvidenceService evidence)
    {
        _evidence = evidence;
    }

    [HttpGet("api/educator/students/{studentId:int}/evidence")]
    [ProducesResponseType(typeof(ApiResponse<StudentEvidenceBundle>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int studentId, CancellationToken ct)
    {
        var result = await _evidence.BuildForStaffAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(ApiResponse<object>.Error(result.Message))
                : StatusCode(403, ApiResponse<object>.Error(result.Message ?? "Forbidden"));
        return Ok(ApiResponse<StudentEvidenceBundle>.SuccessResponse(result.Data));
    }
}
