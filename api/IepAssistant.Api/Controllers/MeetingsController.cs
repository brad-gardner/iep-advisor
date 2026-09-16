using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Meetings;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Meeting scheduling/lifecycle (plan 4). Routes span three surfaces on purpose (student-scoped create/list
/// under <c>educator/students/{studentId}</c>, meeting-scoped mutations under <c>meetings/{id}</c>, and the
/// parent/anonymous RSVP surfaces) so each mirrors the contract exactly rather than forcing one prefix.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingsController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    [HttpPost("educator/students/{studentId:int}/meetings")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int studentId, [FromBody] CreateMeetingRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _meetingService.CreateAsync(User.GetUserId(), studentId, new CreateMeetingModel
        {
            Type = request.Type,
            Title = request.Title,
            StartsAtUtc = request.StartsAtUtc!.Value,
            TimeZoneId = request.TimeZoneId,
            DurationMinutes = request.DurationMinutes,
            Location = request.Location,
            VideoUrl = request.VideoUrl,
            DocumentInstanceId = request.DocumentInstanceId,
            Notes = request.Notes,
            Participants = MapParticipantInputs(request.Participants)
        }, ct);

        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        var dto = MapMeeting(result.Data!);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, ApiResponse<MeetingDto>.SuccessResponse(dto));
    }

    [HttpGet("educator/students/{studentId:int}/meetings/default-participants")]
    [ProducesResponseType(typeof(ApiResponse<List<DefaultParticipantDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDefaultParticipants(int studentId, CancellationToken ct)
    {
        var result = await _meetingService.GetDefaultParticipantsAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return MapFailure<List<DefaultParticipantDto>>(result.Message);

        return Ok(ApiResponse<List<DefaultParticipantDto>>.SuccessResponse(result.Data!.Select(d => new DefaultParticipantDto
        {
            UserId = d.UserId, DisplayName = d.DisplayName, Email = d.Email, TeamRole = d.TeamRole, IsFamily = d.IsFamily, IsStudent = d.IsStudent
        }).ToList()));
    }

    [HttpGet("educator/students/{studentId:int}/meetings")]
    [ProducesResponseType(typeof(ApiResponse<List<MeetingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForStudent(int studentId, CancellationToken ct)
    {
        var result = await _meetingService.GetForStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return MapFailure<List<MeetingDto>>(result.Message);

        return Ok(ApiResponse<List<MeetingDto>>.SuccessResponse(result.Data!.Select(MapMeeting).ToList()));
    }

    [HttpGet("meetings/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _meetingService.GetAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpPut("meetings/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMeetingRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _meetingService.UpdateAsync(User.GetUserId(), id, new UpdateMeetingModel
        {
            Type = request.Type,
            Title = request.Title,
            StartsAtUtc = request.StartsAtUtc,
            TimeZoneId = request.TimeZoneId,
            DurationMinutes = request.DurationMinutes,
            Location = request.Location,
            VideoUrl = request.VideoUrl,
            DocumentInstanceId = request.DocumentInstanceId,
            Notes = request.Notes,
            Participants = request.Participants == null ? null : MapParticipantInputs(request.Participants)
        }, ct);

        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpPost("meetings/{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelMeetingRequest? request, CancellationToken ct)
    {
        var result = await _meetingService.CancelAsync(User.GetUserId(), id, request?.Reason, ct);
        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpPost("meetings/{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetMeetingStatusRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Status == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _meetingService.SetStatusAsync(User.GetUserId(), id, request.Status.Value, ct);
        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpPost("meetings/{id:int}/attendance")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendance(int id, [FromBody] AttendanceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var items = request.Attendance.Select(a => new AttendanceItemModel
        {
            ParticipantId = a.ParticipantId,
            Attended = a.Attended,
            ExcusalNote = a.ExcusalNote
        }).ToList();

        var result = await _meetingService.RecordAttendanceAsync(User.GetUserId(), id, items, ct);
        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpPost("meetings/{id:int}/rsvp")]
    [ProducesResponseType(typeof(ApiResponse<MeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rsvp(int id, [FromBody] RsvpRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Status == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _meetingService.RsvpAsync(User.GetUserId(), id, request.Status.Value, ct);
        if (!result.Success)
            return MapFailure<MeetingDto>(result.Message);

        return Ok(ApiResponse<MeetingDto>.SuccessResponse(MapMeeting(result.Data!)));
    }

    [HttpGet("meetings/mine")]
    [ProducesResponseType(typeof(ApiResponse<List<MeetingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _meetingService.ListMineAsync(User.GetUserId(), from, to, ct);
        if (!result.Success)
            return MapFailure<List<MeetingDto>>(result.Message);

        return Ok(ApiResponse<List<MeetingDto>>.SuccessResponse(result.Data!.Select(MapMeeting).ToList()));
    }

    [HttpGet("children/{childId:int}/meetings")]
    [ProducesResponseType(typeof(ApiResponse<List<MeetingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForChild(int childId, CancellationToken ct)
    {
        var result = await _meetingService.ListForChildAsync(User.GetUserId(), childId, ct);
        if (!result.Success)
            return MapFailure<List<MeetingDto>>(result.Message);

        return Ok(ApiResponse<List<MeetingDto>>.SuccessResponse(result.Data!.Select(MapMeeting).ToList()));
    }

    // ----------------------------------------------------------------- Anonymous email-link RSVP

    [AllowAnonymous]
    [HttpGet("meetings/rsvp")]
    [ProducesResponseType(typeof(ApiResponse<MeetingRsvpPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByToken([FromQuery] string token, CancellationToken ct)
    {
        var result = await _meetingService.GetByRsvpTokenAsync(token, ct);
        if (!result.Success)
            return MapFailure<MeetingRsvpPreviewDto>(result.Message);

        return Ok(ApiResponse<MeetingRsvpPreviewDto>.SuccessResponse(MapRsvpPreview(result.Data!)));
    }

    [AllowAnonymous]
    [HttpPost("meetings/rsvp")]
    [ProducesResponseType(typeof(ApiResponse<MeetingRsvpPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RsvpByToken([FromBody] TokenRsvpRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request.Status == null)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _meetingService.RsvpByTokenAsync(request.Token, request.Status.Value, ct);
        if (!result.Success)
            return MapFailure<MeetingRsvpPreviewDto>(result.Message);

        return Ok(ApiResponse<MeetingRsvpPreviewDto>.SuccessResponse(MapRsvpPreview(result.Data!)));
    }

    // ----------------------------------------------------------------- Mapping

    private static List<ParticipantInputModel>? MapParticipantInputs(List<ParticipantInputRequest>? requests) =>
        requests?.Select(r => new ParticipantInputModel
        {
            UserId = r.UserId,
            ExternalName = r.ExternalName,
            ExternalEmail = r.ExternalEmail,
            TeamRole = r.TeamRole,
            IsRequired = r.IsRequired
        }).ToList();

    private static MeetingDto MapMeeting(MeetingModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        StudentName = m.StudentName,
        Type = m.Type,
        Title = m.Title,
        StartsAtUtc = m.StartsAtUtc,
        TimeZoneId = m.TimeZoneId,
        DurationMinutes = m.DurationMinutes,
        Location = m.Location,
        VideoUrl = m.VideoUrl,
        Status = m.Status,
        DocumentInstanceId = m.DocumentInstanceId,
        Notes = m.Notes,
        Sequence = m.Sequence,
        CreatedByUserId = m.CreatedByUserId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Participants = m.Participants.Select(MapParticipant).ToList(),
        MyInviteStatus = m.MyInviteStatus,
        CanManage = m.CanManage
    };

    private static MeetingParticipantDto MapParticipant(MeetingParticipantModel p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        ExternalName = p.ExternalName,
        ExternalEmail = p.ExternalEmail,
        DisplayName = p.DisplayName,
        TeamRole = p.TeamRole,
        IsRequired = p.IsRequired,
        InviteStatus = p.InviteStatus,
        Attended = p.Attended,
        ExcusedAt = p.ExcusedAt,
        ExcusalNote = p.ExcusalNote,
        IsFamily = p.IsFamily,
        IsStudent = p.IsStudent
    };

    private static MeetingSummaryDto MapMeetingSummary(MeetingSummaryModel m) => new()
    {
        Id = m.Id,
        StudentFirstName = m.StudentFirstName,
        Type = m.Type,
        Title = m.Title,
        StartsAtUtc = m.StartsAtUtc,
        TimeZoneId = m.TimeZoneId,
        DurationMinutes = m.DurationMinutes,
        Location = m.Location,
        Status = m.Status
    };

    private static MeetingRsvpPreviewDto MapRsvpPreview(MeetingRsvpPreviewModel m) => new()
    {
        Meeting = MapMeetingSummary(m.Meeting),
        Status = m.Status
    };

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
