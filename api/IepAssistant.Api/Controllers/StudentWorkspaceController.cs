using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.StudentWorkspace;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// P8a student self-advocacy workspace behind Feature:StudentWorkspace — every action returns 404 when the
/// flag is off. The student owns CRUD over their own entries (private until shared) plus a suggest-only AI
/// interview. Educators and parents may read SHAREABLE entries only. Failures map permission→403,
/// not-found→404, "temporarily unavailable"→503, else 400.
/// </summary>
[ApiController]
[Authorize]
public class StudentWorkspaceController : ControllerBase
{
    private readonly IStudentWorkspaceService _service;
    private readonly IFeatureFlags _featureFlags;

    public StudentWorkspaceController(IStudentWorkspaceService service, IFeatureFlags featureFlags)
    {
        _service = service;
        _featureFlags = featureFlags;
    }

    private bool Enabled => _featureFlags.IsEnabled(FeatureFlags.StudentWorkspace);

    // ---------------------------------------------------------------- Student: my workspace

    [HttpGet("api/student-workspace")]
    [ProducesResponseType(typeof(ApiResponse<StudentWorkspaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyWorkspace(CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.GetMyWorkspaceAsync(User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<StudentWorkspaceDto>.SuccessResponse(MapWorkspace(result.Data!)));
    }

    // ---------------------------------------------------------------- Student: add entry

    [HttpPost("api/student-workspace/entries")]
    [ProducesResponseType(typeof(ApiResponse<StudentWorkspaceEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddEntry([FromBody] CreateWorkspaceEntryRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryParseKind(request.EntryKind, out var kind))
            return BadRequest(ApiResponse<object>.Error("Invalid entry kind."));

        var result = await _service.AddEntryAsync(User.GetUserId(), kind, request.Content, request.IsShareable, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<StudentWorkspaceEntryDto>.SuccessResponse(MapEntry(result.Data!)));
    }

    // ---------------------------------------------------------------- Student: update entry

    [HttpPut("api/student-workspace/entries/{id}")]
    [ProducesResponseType(typeof(ApiResponse<StudentWorkspaceEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntry(int id, [FromBody] UpdateWorkspaceEntryRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateEntryAsync(User.GetUserId(), id, request.Content, request.IsShareable, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<StudentWorkspaceEntryDto>.SuccessResponse(MapEntry(result.Data!)));
    }

    // ---------------------------------------------------------------- Student: delete entry

    [HttpDelete("api/student-workspace/entries/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.DeleteEntryAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null, "Entry deleted."));
    }

    // ---------------------------------------------------------------- Student: AI interview (suggest only)

    [HttpPost("api/student-workspace/interview")]
    [ProducesResponseType(typeof(ApiResponse<StudentInterviewSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Interview([FromBody] StudentInterviewRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.InterviewSuggestAsync(User.GetUserId(), request.Prompt, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<StudentInterviewSuggestionDto>.SuccessResponse(
            new StudentInterviewSuggestionDto { Suggestion = result.Data!.Suggestion }));
    }

    // ---------------------------------------------------------------- Educator: shareable entries

    [HttpGet("api/educator/students/{studentId}/shareable-entries")]
    [ProducesResponseType(typeof(ApiResponse<List<StudentWorkspaceEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEducatorShareableEntries(int studentId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.GetShareableEntriesForSchoolStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<List<StudentWorkspaceEntryDto>>.SuccessResponse(result.Data!.Select(MapEntry).ToList()));
    }

    // ---------------------------------------------------------------- Parent: shareable entries

    [HttpGet("api/children/{childId}/shareable-entries")]
    [ProducesResponseType(typeof(ApiResponse<List<StudentWorkspaceEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildShareableEntries(int childId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.GetShareableEntriesForChildAsync(User.GetUserId(), childId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<List<StudentWorkspaceEntryDto>>.SuccessResponse(result.Data!.Select(MapEntry).ToList()));
    }

    // ---------------------------------------------------------------- Helpers

    private static bool TryParseKind(string? value, out StudentEntryKind kind)
        => Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    private static StudentWorkspaceDto MapWorkspace(StudentWorkspaceModel m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        Entries = m.Entries.Select(MapEntry).ToList()
    };

    private static StudentWorkspaceEntryDto MapEntry(StudentWorkspaceEntryModel e) => new()
    {
        Id = e.Id,
        EntryKind = e.EntryKind.ToString(),
        Content = e.Content,
        IsShareable = e.IsShareable,
        DisplayOrder = e.DisplayOrder,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        if (message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase))
            return StatusCode(503, ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }
}
