using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.District;
using IepAssistant.Api.DTOs.Home;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>One server-computed "what needs me now" home per role (plan 5). Any authenticated user may call it.</summary>
[ApiController]
[Authorize]
[Route("api/home")]
public class HomeController : ControllerBase
{
    private readonly IHomeService _homeService;

    public HomeController(IHomeService homeService)
    {
        _homeService = homeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HomeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _homeService.GetForUserAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure<HomeDto>(result.Message);

        return Ok(ApiResponse<HomeDto>.SuccessResponse(MapHome(result.Data!)));
    }

    // ----------------------------------------------------------------- Mapping

    private static HomeDto MapHome(HomeModel m) => new()
    {
        Kind = m.Kind,
        GeneratedAt = m.GeneratedAt,
        Staff = m.Staff == null ? null : MapStaffHome(m.Staff),
        Parent = m.Parent == null ? null : MapParentHome(m.Parent),
        Student = m.Student == null ? null : MapStudentHome(m.Student)
    };

    private static StaffHomeDto MapStaffHome(StaffHomeModel m) => new()
    {
        Variant = m.Variant,
        DisplayName = m.DisplayName,
        ScopeLabel = m.ScopeLabel,
        WeekStart = m.WeekStart,
        WeekEnd = m.WeekEnd,
        MeetingsThisWeek = m.MeetingsThisWeek.Select(MapMeeting).ToList(),
        Obligations = m.Obligations.Select(ObligationDtoMapper.Map).ToList(),
        Drafts = m.Drafts.Select(MapDraft).ToList(),
        SharedDraftsAwaitingFamily = m.SharedDraftsAwaitingFamily.Select(MapSharedDraft).ToList(),
        FamilyResponsesToReview = m.FamilyResponsesToReview.Select(MapSharedDraft).ToList(),
        ProviderRequestsIOwe = m.ProviderRequestsIOwe.Select(MapProviderRequest).ToList(),
        RosterAttention = m.RosterAttention == null ? null : new RosterAttentionDto
        {
            NoLead = m.RosterAttention.NoLead,
            NoFamily = m.RosterAttention.NoFamily,
            UnknownDates = m.RosterAttention.UnknownDates,
            OverdueAnnual = m.RosterAttention.OverdueAnnual,
            OverdueReeval = m.RosterAttention.OverdueReeval,
            Due30 = m.RosterAttention.Due30
        },
        OverdueByCaseManager = m.OverdueByCaseManager?.Select(r => new CaseManagerRowDto
        {
            StudentId = r.StudentId,
            StudentName = r.StudentName,
            CaseManagerName = r.CaseManagerName,
            Kind = r.Kind,
            DueDate = r.DueDate,
            Status = r.Status
        }).ToList(),
        OverdueByCaseManagerTotal = m.OverdueByCaseManagerTotal,
        UnsignedFinalized = m.UnsignedFinalized?.Select(u => new HomeUnsignedDto
        {
            VersionId = u.VersionId,
            StudentId = u.StudentId,
            StudentName = u.StudentName,
            FinalizedAt = u.FinalizedAt
        }).ToList(),
        // Shared with DistrictController.MapComplianceSummary so the DistrictAdmin home's compliance
        // summary is mapped identically to the board's (review-fix contract, todos/078).
        ComplianceSummary = m.ComplianceSummary == null ? null : DistrictController.MapComplianceSummary(m.ComplianceSummary)
    };

    private static ParentHomeDto MapParentHome(ParentHomeModel m) => new()
    {
        DisplayName = m.DisplayName,
        NextMeeting = m.NextMeeting == null ? null : new ParentNextMeetingDto
        {
            Id = m.NextMeeting.Id,
            Title = m.NextMeeting.Title,
            Type = m.NextMeeting.Type,
            StartsAtUtc = m.NextMeeting.StartsAtUtc,
            TimeZoneId = m.NextMeeting.TimeZoneId,
            DurationMinutes = m.NextMeeting.DurationMinutes,
            StudentId = m.NextMeeting.StudentId,
            StudentName = m.NextMeeting.StudentName,
            MyInviteStatus = m.NextMeeting.MyInviteStatus,
            Status = m.NextMeeting.Status,
            ChildId = m.NextMeeting.ChildId,
            ChildName = m.NextMeeting.ChildName,
            DaysUntil = m.NextMeeting.DaysUntil
        },
        DocumentsToReview = m.DocumentsToReview.Select(d => new ParentDocumentDto
        {
            Kind = d.Kind,
            Id = d.Id,
            ChildId = d.ChildId,
            ChildName = d.ChildName,
            DocumentTypeDisplayName = d.DocumentTypeDisplayName,
            VersionNumber = d.VersionNumber,
            Date = d.Date,
            LinkPath = d.LinkPath
        }).ToList(),
        Children = m.Children.Select(c => new ParentChildDto
        {
            ChildId = c.ChildId,
            ChildName = c.ChildName,
            HasSchoolLink = c.HasSchoolLink,
            StudentId = c.StudentId
        }).ToList(),
        RecentProgressReports = m.RecentProgressReports.Select(r => new ParentProgressReportDto
        {
            Id = r.Id,
            ChildId = r.ChildId,
            ChildName = r.ChildName,
            Title = r.Title,
            CreatedAt = r.CreatedAt
        }).ToList(),
        SetupNotices = new List<string>(m.SetupNotices)
    };

    private static StudentHomeDto MapStudentHome(StudentHomeModel m) => new()
    {
        DisplayName = m.DisplayName,
        NextMeeting = m.NextMeeting == null ? null : MapMeeting(m.NextMeeting),
        WorkspaceNudge = m.WorkspaceNudge,
        LinkedStudentId = m.LinkedStudentId
    };

    private static HomeMeetingDto MapMeeting(HomeMeetingModel m) => new()
    {
        Id = m.Id,
        Title = m.Title,
        Type = m.Type,
        StartsAtUtc = m.StartsAtUtc,
        TimeZoneId = m.TimeZoneId,
        DurationMinutes = m.DurationMinutes,
        StudentId = m.StudentId,
        StudentName = m.StudentName,
        MyInviteStatus = m.MyInviteStatus,
        Status = m.Status
    };

    private static HomeDraftDto MapDraft(HomeDraftModel d) => new()
    {
        InstanceId = d.InstanceId,
        StudentId = d.StudentId,
        StudentName = d.StudentName,
        DocumentTypeKey = d.DocumentTypeKey,
        DocumentTypeDisplayName = d.DocumentTypeDisplayName,
        LastEditedAt = d.LastEditedAt,
        CompletenessPercent = d.CompletenessPercent,
        RequiredMissing = d.RequiredMissing
    };

    private static HomeSharedDraftDto MapSharedDraft(HomeSharedDraftModel d) => new()
    {
        InstanceId = d.InstanceId,
        StudentId = d.StudentId,
        StudentName = d.StudentName,
        SharedAt = d.SharedAt,
        RespondedAt = d.RespondedAt
    };

    private static HomeProviderRequestDto MapProviderRequest(HomeProviderRequestModel r) => new()
    {
        Id = r.Id,
        StudentId = r.StudentId,
        StudentName = r.StudentName,
        DueDate = r.DueDate
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
