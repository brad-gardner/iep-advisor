using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.StudentInvites;
using IepAssistant.Api.Extensions;
using IepAssistant.Services;
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
    private readonly IStringLocalizer<Messages> _localizer;

    public StudentInviteController(IStudentInviteService service, IStringLocalizer<Messages> localizer)
    {
        _service = service;
        _localizer = localizer;
    }

    // ----------------------------------------------------------------- Parent: invite student

    [HttpPost("/api/children/{childId}/invite-student")]
    [ProducesResponseType(typeof(ApiResponse<StudentInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteFromParent(int childId, [FromBody] InviteStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var result = await _service.InviteFromParentAsync(User.GetUserId(), childId, request.StudentEmail, ct);
        return result.Success
            ? Ok(ApiResponse<StudentInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message))
            : this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
    }

    // ----------------------------------------------------------------- Educator: invite student

    [HttpPost("/api/educator/students/{studentId}/invite-student")]
    [ProducesResponseType(typeof(ApiResponse<StudentInviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteFromEducator(int studentId, [FromBody] InviteStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var result = await _service.InviteFromEducatorAsync(User.GetUserId(), studentId, request.StudentEmail, ct);
        return result.Success
            ? Ok(ApiResponse<StudentInviteDto>.SuccessResponse(MapInvite(result.Data!), result.Message))
            : this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);
    }

    // ----------------------------------------------------------------- Student: preview

    [HttpGet("/api/student-invites/preview")]
    [ProducesResponseType(typeof(ApiResponse<StudentInvitePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Error(_localizer["StudentInvitesApi.TokenRequired"]));

        var result = await _service.PreviewInviteAsync(User.GetUserId(), token, ct);
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

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
            return BadRequest(ApiResponse<object>.Error(_localizer["AuthApi.InvalidRequest"]));

        var result = await _service.AcceptInviteAsync(User.GetUserId(), request.Token, request.ConsentAccepted, ct);
        // Multilingual plan Phase 3: status came from matching translated text ("permission", "not
        // found"); StudentInviteService now sets ErrorKind explicitly, so use the shared kind-based mapper.
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["Api.RequestFailed"]);

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
}
