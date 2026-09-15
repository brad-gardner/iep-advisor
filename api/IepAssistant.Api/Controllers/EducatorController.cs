using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Educator;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/educator")]
public class EducatorController : ControllerBase
{
    private readonly IEducatorService _educatorService;
    private readonly IChildLinkService _childLinkService;
    private readonly IStudentTeamService _teamService;

    public EducatorController(IEducatorService educatorService, IChildLinkService childLinkService, IStudentTeamService teamService)
    {
        _educatorService = educatorService;
        _childLinkService = childLinkService;
        _teamService = teamService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<EducatorProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var result = await _educatorService.GetMeAsync(User.GetUserId(), ct);

        if (!result.Success)
            return MapFailure<EducatorProfileDto>(result.Message);

        return Ok(ApiResponse<EducatorProfileDto>.SuccessResponse(MapProfile(result.Data!)));
    }

    /// <summary>
    /// Paged roster. <c>status</c> = Active (default) | Exited | Archived | All; <c>query</c> matches
    /// first/last name or external student id; <c>schoolId</c>/<c>grade</c> narrow the role-scoped set;
    /// <c>attention</c> = NoCaseManager | NoLinkedParent narrows to the dashboard's "needs attention" sets
    /// (server-side, so paging stays exact).
    /// </summary>
    [HttpGet("students")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<SchoolStudentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? query,
        [FromQuery] int? schoolId,
        [FromQuery] string? status,
        [FromQuery] GradeLevel? grade,
        [FromQuery] string? attention,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        StudentStatus? statusFilter = StudentStatus.Active;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("All", StringComparison.OrdinalIgnoreCase))
                statusFilter = null;
            else if (Enum.TryParse<StudentStatus>(status, ignoreCase: true, out var parsed))
                statusFilter = parsed;
            else
                return BadRequest(ApiResponse<object>.Error("Invalid status filter."));
        }

        StudentAttention? attentionFilter = null;
        if (!string.IsNullOrWhiteSpace(attention))
        {
            if (Enum.TryParse<StudentAttention>(attention, ignoreCase: true, out var parsedAttention) && Enum.IsDefined(parsedAttention))
                attentionFilter = parsedAttention;
            else
                return BadRequest(ApiResponse<object>.Error("Invalid attention filter."));
        }

        var result = await _educatorService.SearchStudentsAsync(User.GetUserId(), new StudentSearchQuery
        {
            Query = query,
            SchoolId = schoolId,
            Status = statusFilter,
            Grade = grade,
            Attention = attentionFilter,
            Page = page,
            PageSize = pageSize
        }, ct);

        if (!result.Success)
            return MapFailure<PagedResultDto<SchoolStudentDto>>(result.Message);

        return Ok(ApiResponse<PagedResultDto<SchoolStudentDto>>.SuccessResponse(new PagedResultDto<SchoolStudentDto>
        {
            Items = result.Data!.Items.Select(MapStudent).ToList(),
            Total = result.Data.Total,
            Page = result.Data.Page,
            PageSize = result.Data.PageSize
        }));
    }

    [HttpPost("students")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateSchoolStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.CreateStudentAsync(User.GetUserId(), new CreateSchoolStudentModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            StateCode = request.StateCode,
            ExternalStudentId = request.ExternalStudentId,
            GradeLevel = request.GradeLevel,
            DisabilityCategory = request.DisabilityCategory,
            HomeLanguage = request.HomeLanguage,
            IepDate = request.IepDate,
            AnnualReviewDueDate = request.AnnualReviewDueDate,
            EtrDate = request.EtrDate,
            ReevaluationDueDate = request.ReevaluationDueDate,
            SchoolId = request.SchoolId
        }, ct);

        if (!result.Success)
            return MapFailure<SchoolStudentDto>(result.Message);

        var dto = MapStudent(result.Data!);
        return CreatedAtAction(nameof(GetStudent), new { studentId = dto.Id },
            ApiResponse<SchoolStudentDto>.SuccessResponse(dto));
    }

    [HttpGet("students/{studentId}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudent(int studentId, CancellationToken ct)
    {
        var result = await _educatorService.GetStudentAsync(User.GetUserId(), studentId, ct);

        if (!result.Success)
            return MapFailure<SchoolStudentDto>(result.Message);

        return Ok(ApiResponse<SchoolStudentDto>.SuccessResponse(MapStudent(result.Data!)));
    }

    // ----------------------------------------------------------------- Lifecycle

    [HttpPut("students/{studentId}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStudent(int studentId, [FromBody] UpdateSchoolStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.UpdateStudentAsync(User.GetUserId(), studentId, new UpdateSchoolStudentModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            StateCode = request.StateCode,
            ExternalStudentId = request.ExternalStudentId,
            GradeLevel = request.GradeLevel,
            DisabilityCategory = request.DisabilityCategory,
            HomeLanguage = request.HomeLanguage,
            IepDate = request.IepDate,
            AnnualReviewDueDate = request.AnnualReviewDueDate,
            EtrDate = request.EtrDate,
            ReevaluationDueDate = request.ReevaluationDueDate
        }, ct);

        return StudentResult(result);
    }

    [HttpPost("students/{studentId}/exit")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExitStudent(int studentId, [FromBody] ExitStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.ExitStudentAsync(User.GetUserId(), studentId,
            new ExitStudentModel { ExitReason = request.ExitReason!.Value, ExitedAt = request.ExitedAt }, ct);
        return StudentResult(result);
    }

    [HttpPost("students/{studentId}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateStudent(int studentId, CancellationToken ct)
        => StudentResult(await _educatorService.ReactivateStudentAsync(User.GetUserId(), studentId, ct));

    [HttpPost("students/{studentId}/archive")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveStudent(int studentId, CancellationToken ct)
        => StudentResult(await _educatorService.ArchiveStudentAsync(User.GetUserId(), studentId, ct));

    [HttpPost("students/{studentId}/transfer")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransferStudent(int studentId, [FromBody] TransferStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        return StudentResult(await _educatorService.TransferStudentAsync(User.GetUserId(), studentId, request.NewSchoolId, ct));
    }

    [HttpPost("students/bulk/case-manager")]
    [ProducesResponseType(typeof(ApiResponse<BulkAssignResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignCaseManagerBulk([FromBody] BulkAssignCaseManagerRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.AssignCaseManagerBulkAsync(User.GetUserId(),
            new BulkAssignCaseManagerModel { StudentIds = request.StudentIds, UserId = request.UserId }, ct);
        if (!result.Success)
            return MapFailure<BulkAssignResultDto>(result.Message);

        return Ok(ApiResponse<BulkAssignResultDto>.SuccessResponse(new BulkAssignResultDto { Updated = result.Data!.Updated }));
    }

    // ----------------------------------------------------------------- IEP team

    [HttpGet("students/{studentId}/team")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StudentTeamMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTeam(int studentId, CancellationToken ct)
    {
        var result = await _teamService.GetTeamAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return MapFailure<IEnumerable<StudentTeamMemberDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<StudentTeamMemberDto>>.SuccessResponse(result.Data!.Select(MapTeamMember)));
    }

    /// <summary>Staff who can be added to this student's team (admin in scope or the current lead).</summary>
    [HttpGet("students/{studentId}/team/eligible")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<EligibleStaffDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibleTeamStaff(int studentId, CancellationToken ct)
    {
        var result = await _teamService.GetEligibleStaffAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return MapFailure<IEnumerable<EligibleStaffDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<EligibleStaffDto>>.SuccessResponse(result.Data!.Select(m => new EligibleStaffDto
        {
            StaffProfileId = m.StaffProfileId,
            UserId = m.UserId,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            OrgRoleId = m.OrgRoleId,
            OrgRoleName = m.OrgRoleName,
            SchoolId = m.SchoolId,
            SchoolName = m.SchoolName
        })));
    }

    [HttpPost("students/{studentId}/team")]
    [ProducesResponseType(typeof(ApiResponse<StudentTeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTeamMember(int studentId, [FromBody] AddTeamMemberRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _teamService.AddMemberAsync(User.GetUserId(), studentId, new AddTeamMemberModel
        {
            StaffProfileId = request.StaffProfileId,
            TeamRole = request.TeamRole!.Value,
            IsLead = request.IsLead,
            AccessRole = request.AccessRole
        }, ct);
        return TeamMemberResult(result);
    }

    [HttpPut("students/{studentId}/team/{memberId}")]
    [ProducesResponseType(typeof(ApiResponse<StudentTeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTeamMember(int studentId, int memberId, [FromBody] UpdateTeamMemberRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _teamService.UpdateMemberAsync(User.GetUserId(), studentId, memberId,
            new UpdateTeamMemberModel { TeamRole = request.TeamRole, AccessRole = request.AccessRole }, ct);
        return TeamMemberResult(result);
    }

    [HttpPost("students/{studentId}/team/{memberId}/lead")]
    [ProducesResponseType(typeof(ApiResponse<StudentTeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTeamLead(int studentId, int memberId, CancellationToken ct)
        => TeamMemberResult(await _teamService.SetLeadAsync(User.GetUserId(), studentId, memberId, ct));

    /// <summary>Removes (deactivates) a member. Returns the standard envelope, like the sibling revoke routes, so clients can read <c>success</c>.</summary>
    [HttpDelete("students/{studentId}/team/{memberId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTeamMember(int studentId, int memberId, CancellationToken ct)
    {
        var result = await _teamService.RemoveMemberAsync(User.GetUserId(), studentId, memberId, ct);
        if (!result.Success)
            return MapFailure<object>(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    [HttpPost("students/{studentId}/invite-parent")]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteParent(int studentId, [FromBody] InviteParentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _childLinkService.InviteParentAsync(User.GetUserId(), studentId, request.ParentEmail, ct);

        if (!result.Success)
            return MapFailure<ChildLinkDto>(result.Message);

        return Ok(ApiResponse<ChildLinkDto>.SuccessResponse(MapLink(result.Data!), result.Message));
    }

    [HttpGet("students/{studentId}/links")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChildLinkDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLinks(int studentId, CancellationToken ct)
    {
        var result = await _childLinkService.GetLinksForStudentAsync(User.GetUserId(), studentId, ct);

        if (!result.Success)
            return MapFailure<IEnumerable<ChildLinkDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<ChildLinkDto>>.SuccessResponse(result.Data!.Select(MapLink)));
    }

    [HttpDelete("students/{studentId}/links/{linkId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeLink(int studentId, int linkId, CancellationToken ct)
    {
        var result = await _childLinkService.RevokeLinkAsync(User.GetUserId(), studentId, linkId, ct);

        if (!result.Success)
            return MapFailure<object>(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    // ----------------------------------------------------------------- Staff assignment

    [HttpGet("students/{studentId}/staff-access")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StudentStaffAccessDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaffAccess(int studentId, CancellationToken ct)
    {
        var result = await _educatorService.GetStudentStaffAccessAsync(User.GetUserId(), studentId, ct);
        if (!result.Success)
            return MapFailure<IEnumerable<StudentStaffAccessDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<StudentStaffAccessDto>>.SuccessResponse(result.Data!.Select(MapStaffAccess)));
    }

    [HttpPost("students/{studentId}/staff-access")]
    [ProducesResponseType(typeof(ApiResponse<StudentStaffAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantStaffAccess(int studentId, [FromBody] GrantStudentStaffAccessRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        AccessRole accessRole = AccessRole.Collaborator;
        if (!string.IsNullOrWhiteSpace(request.AccessRole) &&
            !Enum.TryParse(request.AccessRole, ignoreCase: true, out accessRole))
            return BadRequest(ApiResponse<object>.Error("Invalid access role."));

        var result = await _educatorService.GrantStudentStaffAccessAsync(User.GetUserId(), studentId,
            new GrantStudentStaffAccessModel { StaffProfileId = request.StaffProfileId, AccessRole = accessRole }, ct);
        if (!result.Success)
            return MapFailure<StudentStaffAccessDto>(result.Message);

        return Ok(ApiResponse<StudentStaffAccessDto>.SuccessResponse(MapStaffAccess(result.Data!)));
    }

    [HttpDelete("students/{studentId}/staff-access/{accessId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeStaffAccess(int studentId, int accessId, CancellationToken ct)
    {
        var result = await _educatorService.RevokeStudentStaffAccessAsync(User.GetUserId(), studentId, accessId, ct);
        if (!result.Success)
            return MapFailure<object>(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    private static StudentStaffAccessDto MapStaffAccess(StudentStaffAccessModel m) => new()
    {
        AccessId = m.AccessId,
        StaffProfileId = m.StaffProfileId,
        UserId = m.UserId,
        FirstName = m.FirstName,
        LastName = m.LastName,
        Email = m.Email,
        OrgRoleName = m.OrgRoleName,
        AccessRole = m.AccessRole.ToString(),
        GrantedAt = m.GrantedAt
    };

    private static ChildLinkDto MapLink(ChildLinkModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        ChildProfileId = m.ChildProfileId,
        InviteEmail = m.InviteEmail,
        IsActive = m.IsActive,
        IsAccepted = m.IsAccepted,
        AcceptedAt = m.AcceptedAt,
        LinkedAt = m.LinkedAt,
        InviteExpiresAt = m.InviteExpiresAt,
        CreatedAt = m.CreatedAt
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

    private static EducatorProfileDto MapProfile(EducatorProfileModel m) => new()
    {
        StaffProfileId = m.StaffProfileId,
        UserId = m.UserId,
        OrgRoleId = m.OrgRoleId,
        OrgRoleName = m.OrgRoleName,
        DistrictId = m.DistrictId,
        DistrictName = m.DistrictName,
        SchoolId = m.SchoolId,
        SchoolName = m.SchoolName,
        IsActive = m.IsActive,
        StateCode = m.StateCode,
        Title = m.Title,
        Credentials = m.Credentials
    };

    private IActionResult StudentResult(ServiceResult<SchoolStudentModel> result)
    {
        if (!result.Success)
            return MapFailure<SchoolStudentDto>(result.Message);
        return Ok(ApiResponse<SchoolStudentDto>.SuccessResponse(MapStudent(result.Data!)));
    }

    private IActionResult TeamMemberResult(ServiceResult<StudentTeamMemberModel> result)
    {
        if (!result.Success)
            return MapFailure<StudentTeamMemberDto>(result.Message);
        return Ok(ApiResponse<StudentTeamMemberDto>.SuccessResponse(MapTeamMember(result.Data!)));
    }

    private static StudentTeamMemberDto MapTeamMember(StudentTeamMemberModel m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        StaffProfileId = m.StaffProfileId,
        FirstName = m.FirstName,
        LastName = m.LastName,
        Email = m.Email,
        OrgRoleName = m.OrgRoleName,
        TeamRole = m.TeamRole,
        IsLead = m.IsLead,
        AccessRole = m.AccessRole,
        IsActive = m.IsActive,
        AddedAt = m.AddedAt
    };

    private static SchoolStudentDto MapStudent(SchoolStudentModel m) => new()
    {
        Id = m.Id,
        SchoolId = m.SchoolId,
        SchoolName = m.SchoolName,
        FirstName = m.FirstName,
        LastName = m.LastName,
        DateOfBirth = m.DateOfBirth,
        StateCode = m.StateCode,
        ExternalStudentId = m.ExternalStudentId,
        GradeLevel = m.GradeLevel,
        DisabilityCategory = m.DisabilityCategory,
        LegacyDisabilityText = m.LegacyDisabilityText,
        HomeLanguage = m.HomeLanguage,
        Status = m.Status,
        ExitedAt = m.ExitedAt,
        ExitReason = m.ExitReason,
        CaseManagerUserId = m.CaseManagerUserId,
        CaseManagerName = m.CaseManagerName,
        IepDate = m.IepDate,
        AnnualReviewDueDate = m.AnnualReviewDueDate,
        EtrDate = m.EtrDate,
        ReevaluationDueDate = m.ReevaluationDueDate,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt
    };
}
