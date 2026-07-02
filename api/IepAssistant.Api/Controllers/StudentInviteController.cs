using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.StudentInvites;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// P7a student role + invite + consent. Covers parent-initiated and educator-initiated invites plus
/// the invited student's preview + consent-gated accept.
/// </summary>
[ApiController]
[Authorize]
public class StudentInviteController : ControllerBase
{
    private readonly IStudentInviteService _service;

    public StudentInviteController(IStudentInviteService service)
    {
        _service = service;
    }

    // ----------------------------------------------------------------- Parent: invite student

    [HttpPost("/api/children/{childId}/invite-student")]
    [ProducesResponseType(typeof(ApiResponse<StudentInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteFromParent(int childId, [FromBody] InviteStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.InviteFromParentAsync(User.GetUserId(), childId, request.StudentEmail, ct);
        return result.Success
            ? Ok(ApiResponse<StudentInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message))
            : MapFailure(result.Message);
    }

    // ----------------------------------------------------------------- Educator: invite student

    [HttpPost("/api/educator/students/{studentId}/invite-student")]
    [ProducesResponseType(typeof(ApiResponse<StudentInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteFromEducator(int studentId, [FromBody] InviteStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.InviteFromEducatorAsync(User.GetUserId(), studentId, request.StudentEmail, ct);
        return result.Success
            ? Ok(ApiResponse<StudentInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message))
            : MapFailure(result.Message);
    }

    // ----------------------------------------------------------------- Student: preview

    [HttpGet("/api/student-invites/preview")]
    [ProducesResponseType(typeof(ApiResponse<StudentInvitePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Error("Token is required."));

        var result = await _service.PreviewInviteAsync(User.GetUserId(), token, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        var d = result.Data!;
        return Ok(ApiResponse<StudentInvitePreviewDto>.SuccessResponse(new StudentInvitePreviewDto
        {
            InviteSource = d.InviteSource,
            LinkedToFirstName = d.LinkedToFirstName,
            SchoolName = d.SchoolName,
            InviteExpiresAt = d.InviteExpiresAt
        }));
    }

    // ----------------------------------------------------------------- Student: accept (consent-gated)

    [HttpPost("/api/student-invites/accept")]
    [ProducesResponseType(typeof(ApiResponse<AcceptedStudentInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Accept([FromBody] AcceptStudentInviteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AcceptInviteAsync(User.GetUserId(), request.Token, request.ConsentAccepted, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        var d = result.Data!;
        return Ok(ApiResponse<AcceptedStudentInviteDto>.SuccessResponse(new AcceptedStudentInviteDto
        {
            StudentProfileId = d.StudentProfileId,
            ChildProfileId = d.ChildProfileId,
            SchoolStudentId = d.SchoolStudentId,
            ConsentAcceptedAt = d.ConsentAcceptedAt
        }, result.Message));
    }

    private static StudentInviteDto MapInvite(StudentInviteModel m) => new()
    {
        Id = m.Id,
        InviteEmail = m.InviteEmail,
        ChildProfileId = m.ChildProfileId,
        SchoolStudentId = m.SchoolStudentId,
        IsAccepted = m.IsAccepted,
        InviteExpiresAt = m.InviteExpiresAt
    };

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        // "Invalid or expired", "different email", "Consent is required", "already linked" → 400.
        return BadRequest(ApiResponse<object>.Error(message));
    }
}
