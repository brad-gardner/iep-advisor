using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Educator;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/educator")]
public class EducatorController : ControllerBase
{
    private readonly IEducatorService _educatorService;
    private readonly IFeatureFlags _featureFlags;

    public EducatorController(IEducatorService educatorService, IFeatureFlags featureFlags)
    {
        _educatorService = educatorService;
        _featureFlags = featureFlags;
    }

    [HttpPost("onboard")]
    [ProducesResponseType(typeof(ApiResponse<EducatorProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Onboard([FromBody] OnboardEducatorRequest request, CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.OnboardAsync(User.GetUserId(), new OnboardEducatorModel
        {
            DistrictName = request.DistrictName,
            SchoolName = request.SchoolName,
            StateCode = request.StateCode
        }, ct);

        if (!result.Success)
            return MapFailure<EducatorProfileDto>(result.Message);

        return Ok(ApiResponse<EducatorProfileDto>.SuccessResponse(MapProfile(result.Data!)));
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<EducatorProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        var result = await _educatorService.GetMeAsync(User.GetUserId(), ct);

        if (!result.Success)
            return MapFailure<EducatorProfileDto>(result.Message);

        return Ok(ApiResponse<EducatorProfileDto>.SuccessResponse(MapProfile(result.Data!)));
    }

    [HttpGet("students")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<SchoolStudentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        var result = await _educatorService.GetStudentsAsync(User.GetUserId(), ct);

        if (!result.Success)
            return MapFailure<IEnumerable<SchoolStudentDto>>(result.Message);

        return Ok(ApiResponse<IEnumerable<SchoolStudentDto>>.SuccessResponse(result.Data!.Select(MapStudent)));
    }

    [HttpPost("students")]
    [ProducesResponseType(typeof(ApiResponse<SchoolStudentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateSchoolStudentRequest request, CancellationToken ct)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _educatorService.CreateStudentAsync(User.GetUserId(), new CreateSchoolStudentModel
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            StateCode = request.StateCode,
            GradeLevel = request.GradeLevel,
            DisabilityCategory = request.DisabilityCategory
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
        if (!_featureFlags.IsEnabled(FeatureFlags.SchoolSide))
            return NotFound();

        var result = await _educatorService.GetStudentAsync(User.GetUserId(), studentId, ct);

        if (!result.Success)
            return MapFailure<SchoolStudentDto>(result.Message);

        return Ok(ApiResponse<SchoolStudentDto>.SuccessResponse(MapStudent(result.Data!)));
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

    private static EducatorProfileDto MapProfile(EducatorProfileModel m) => new()
    {
        TeacherProfileId = m.TeacherProfileId,
        UserId = m.UserId,
        SchoolId = m.SchoolId,
        SchoolName = m.SchoolName,
        DistrictId = m.DistrictId,
        DistrictName = m.DistrictName,
        StateCode = m.StateCode,
        Title = m.Title,
        Credentials = m.Credentials
    };

    private static SchoolStudentDto MapStudent(SchoolStudentModel m) => new()
    {
        Id = m.Id,
        SchoolId = m.SchoolId,
        FirstName = m.FirstName,
        LastName = m.LastName,
        DateOfBirth = m.DateOfBirth,
        StateCode = m.StateCode,
        GradeLevel = m.GradeLevel,
        DisabilityCategory = m.DisabilityCategory,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt
    };
}
