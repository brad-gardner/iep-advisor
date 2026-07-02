using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.District;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/district")]
public class DistrictController : ControllerBase
{
    private readonly IDistrictService _districtService;

    public DistrictController(IDistrictService districtService)
    {
        _districtService = districtService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DistrictOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var result = await _districtService.GetOverviewAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure<DistrictOverviewDto>(result.Message);

        return Ok(ApiResponse<DistrictOverviewDto>.SuccessResponse(MapOverview(result.Data!)));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<DistrictDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _districtService.GetDashboardAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure<DistrictDashboardDto>(result.Message);

        return Ok(ApiResponse<DistrictDashboardDto>.SuccessResponse(MapDashboard(result.Data!)));
    }

    [HttpGet("schools")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DistrictSchoolDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSchools(CancellationToken ct)
    {
        var result = await _districtService.GetSchoolsAsync(User.GetUserId(), ct);
        if (!result.Success)
            return MapFailure<IEnumerable<DistrictSchoolDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<DistrictSchoolDto>>.SuccessResponse(result.Data!.Select(MapSchool)));
    }

    [HttpPost("schools")]
    [ProducesResponseType(typeof(ApiResponse<DistrictSchoolDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _districtService.CreateSchoolAsync(User.GetUserId(), new CreateSchoolModel
        {
            Name = request.Name,
            StateCode = request.StateCode
        }, ct);

        if (!result.Success)
            return MapFailure<DistrictSchoolDto>(result.Message);

        var dto = MapSchool(result.Data!);
        return CreatedAtAction(nameof(GetSchools), new { }, ApiResponse<DistrictSchoolDto>.SuccessResponse(dto));
    }

    [HttpPut("schools/{id}")]
    [ProducesResponseType(typeof(ApiResponse<DistrictSchoolDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSchool(int id, [FromBody] UpdateSchoolRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _districtService.UpdateSchoolAsync(User.GetUserId(), id, new UpdateSchoolModel
        {
            Name = request.Name,
            StateCode = request.StateCode
        }, ct);

        if (!result.Success)
            return MapFailure<DistrictSchoolDto>(result.Message);

        return Ok(ApiResponse<DistrictSchoolDto>.SuccessResponse(MapSchool(result.Data!)));
    }

    [HttpDelete("schools/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateSchool(int id, CancellationToken ct)
    {
        var result = await _districtService.DeactivateSchoolAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure<object>(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, result.Message));
    }

    private static DistrictOverviewDto MapOverview(DistrictOverviewModel m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        StateCode = m.StateCode,
        ActiveSchoolCount = m.ActiveSchoolCount,
        ActiveStaffCount = m.ActiveStaffCount
    };

    private static DistrictSchoolDto MapSchool(DistrictSchoolModel m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        StateCode = m.StateCode,
        ActiveStudentCount = m.ActiveStudentCount,
        ActiveStaffCount = m.ActiveStaffCount
    };

    private static DistrictDashboardDto MapDashboard(DistrictDashboardModel m) => new()
    {
        Schools = m.Schools.Select(s => new DashboardSchoolDto
        {
            Id = s.Id,
            Name = s.Name,
            ActiveStudentCount = s.ActiveStudentCount
        }).ToList(),
        StaffSummary = new DashboardStaffSummaryDto
        {
            ActiveCount = m.StaffSummary.ActiveCount,
            DeactivatedCount = m.StaffSummary.DeactivatedCount,
            InvitedCount = m.StaffSummary.InvitedCount
        },
        InvitesNeedingAttention = m.InvitesNeedingAttention.Select(i => new DashboardInviteDto
        {
            Id = i.Id,
            Email = i.Email,
            OrgRoleId = i.OrgRoleId,
            OrgRoleName = i.OrgRoleName,
            SchoolId = i.SchoolId,
            SchoolName = i.SchoolName,
            InviteExpiresAt = i.InviteExpiresAt,
            Status = i.Status
        }).ToList(),
        StudentsWithoutStaff = m.StudentsWithoutStaff.Select(s => new DashboardStudentDto
        {
            SchoolStudentId = s.SchoolStudentId,
            FirstName = s.FirstName,
            LastName = s.LastName,
            SchoolName = s.SchoolName
        }).ToList(),
        StudentsWithoutParent = m.StudentsWithoutParent.Select(s => new DashboardNoParentStudentDto
        {
            SchoolStudentId = s.SchoolStudentId,
            FirstName = s.FirstName,
            LastName = s.LastName,
            SchoolName = s.SchoolName,
            ParentInvitePending = s.ParentInvitePending
        }).ToList()
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
