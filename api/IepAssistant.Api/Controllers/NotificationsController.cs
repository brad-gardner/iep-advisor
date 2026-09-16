using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Notifications;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>In-app notifications for the calling user (plan 4, decision 3) — every role.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] bool unread = false, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await _notificationService.GetForUserAsync(User.GetUserId(), unread, limit, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        var data = result.Data!;
        return Ok(ApiResponse<NotificationListDto>.SuccessResponse(new NotificationListDto
        {
            Items = data.Items.Select(MapNotification).ToList(),
            UnreadCount = data.UnreadCount
        }));
    }

    [HttpPost("{id:int}/read")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var result = await _notificationService.MarkReadAsync(User.GetUserId(), id, ct);
        if (!result.Success)
            return MapFailure(result.Message);

        return Ok(ApiResponse<object>.SuccessResponse(null));
    }

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllReadResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllReadAsync(User.GetUserId(), ct);
        return Ok(ApiResponse<MarkAllReadResponseDto>.SuccessResponse(new MarkAllReadResponseDto { Marked = result.Data }));
    }

    private static NotificationDto MapNotification(NotificationModel n) => new()
    {
        Id = n.Id,
        Kind = n.Kind,
        Title = n.Title,
        Body = n.Body,
        LinkPath = n.LinkPath,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt,
        EmailSentAt = n.EmailSentAt,
        EmailError = n.EmailError
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
