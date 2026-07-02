using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.District;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Read-only district audit-log viewer (Phase 2). Actor-scoped, keyset-paged, filterable. DistrictAdmin
/// sees district-wide activity; SchoolAdmin sees own-school actors only; Teacher, parents, and students
/// are denied. Viewing the audit log is itself not audited (pilot decision).
/// </summary>
[ApiController]
[Authorize]
[Route("api/district/audit-log")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AuditLogPageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Query([FromQuery] AuditLogQueryRequest request, CancellationToken ct)
    {
        var result = await _auditLogQueryService.QueryAsync(User.GetUserId(), new AuditLogQuery
        {
            StaffUserId = request.StaffUserId,
            StudentId = request.StudentId,
            Action = request.Action,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            Cursor = request.Cursor,
            PageSize = request.PageSize
        }, ct);

        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<AuditLogPageDto>.SuccessResponse(MapPage(result.Data!)));
    }

    private static AuditLogPageDto MapPage(AuditLogPageModel m) => new()
    {
        NextCursor = m.NextCursor,
        Entries = m.Entries.Select(e => new AuditLogEntryDto
        {
            Id = e.Id,
            Action = e.Action,
            ActorUserId = e.ActorUserId,
            ActorName = e.ActorName,
            ResourceType = e.ResourceType,
            ResourceId = e.ResourceId,
            ResourceDisplayName = e.ResourceDisplayName,
            RecipientUserId = e.RecipientUserId,
            RecipientName = e.RecipientName,
            CreatedAt = e.CreatedAt
        }).ToList()
    };

    // Mirrors DistrictController.MapFailure: permission → 403, not found → 404, else → 400.
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
