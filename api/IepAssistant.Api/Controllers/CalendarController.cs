using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Calendar;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Meetings;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Staff/parent calendar aggregation + ICS export (plan 4, decision 4).</summary>
[ApiController]
[Authorize]
[Route("api/calendar")]
public class CalendarController : ControllerBase
{
    private const string IcsContentType = "text/calendar; charset=utf-8";

    private readonly ICalendarService _calendarService;

    public CalendarController(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<CalendarItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _calendarService.GetMineAsync(User.GetUserId(), from, to, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<List<CalendarItemDto>>.SuccessResponse(result.Data!.Select(MapItem).ToList()));
    }

    [HttpGet("feed")]
    [ProducesResponseType(typeof(ApiResponse<CalendarFeedDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeed(CancellationToken ct)
    {
        var result = await _calendarService.GetOrCreateFeedAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<CalendarFeedDto>.SuccessResponse(MapFeed(result.Data!)));
    }

    [HttpPost("feed/regenerate")]
    [ProducesResponseType(typeof(ApiResponse<CalendarFeedDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerateFeed(CancellationToken ct)
    {
        var result = await _calendarService.RegenerateFeedAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<CalendarFeedDto>.SuccessResponse(MapFeed(result.Data!)));
    }

    [AllowAnonymous]
    [HttpGet("feed/{token}.ics")]
    public async Task<IActionResult> GetFeedByToken(string token, CancellationToken ct)
    {
        var result = await _calendarService.GetFeedByTokenAsync(token, ct);
        if (!result.Success)
            return NotFound();

        return File(result.Data!, IcsContentType, "calendar.ics");
    }

    [HttpGet("/api/meetings/{id:int}.ics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeetingIcs(int id, CancellationToken ct)
    {
        var result = await _calendarService.GetMeetingIcsAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return File(result.Data!, IcsContentType, $"meeting-{id}.ics");
    }

    private CalendarFeedDto MapFeed(CalendarFeedModel m) => new()
    {
        Url = $"{Request.Scheme}://{Request.Host}/api/calendar/feed/{m.Token}.ics",
        CreatedAt = m.CreatedAt
    };

    private static CalendarItemDto MapItem(CalendarItemModel i) => new()
    {
        Kind = i.Kind,
        Date = i.Date,
        Meeting = i.Meeting == null ? null : MapMeeting(i.Meeting),
        Obligation = i.Obligation == null ? null : MapObligation(i.Obligation)
    };

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
        Participants = m.Participants.Select(p => new MeetingParticipantDto
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
        }).ToList(),
        MyInviteStatus = m.MyInviteStatus,
        CanManage = m.CanManage
    };

    private static ObligationDto MapObligation(ObligationModel o) => new()
    {
        Kind = o.Kind,
        DueDate = o.DueDate,
        Status = o.Status,
        SourceLabel = o.SourceLabel,
        OwnerUserId = o.OwnerUserId,
        OwnerName = o.OwnerName,
        SchoolStudentId = o.SchoolStudentId,
        StudentName = o.StudentName,
        DaysUntilDue = o.DaysUntilDue,
        RuleProfile = o.RuleProfile
    };

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
