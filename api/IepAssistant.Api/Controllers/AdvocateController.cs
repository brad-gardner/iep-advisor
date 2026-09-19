using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IepAssistant.Api.DTOs.Advocate;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Api.Streaming;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// The Virtual Advocate: a parent's private chat threads about a child, and the streaming message endpoint.
/// Threads are visible only to the parent who started them. No educator endpoint by design.
/// </summary>
[ApiController]
[Authorize]
public class AdvocateController : ControllerBase
{
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    private readonly IAdvocateService _service;

    public AdvocateController(IAdvocateService service)
    {
        _service = service;
    }

    [HttpGet("api/children/{childId:int}/advocate/threads")]
    public async Task<IActionResult> ListThreads(int childId, CancellationToken ct)
    {
        var result = await _service.ListThreadsAsync(User.GetUserId(), childId, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        return Ok(ApiResponse<List<AdvocateThreadDto>>.SuccessResponse(result.Data!.Select(MapThread).ToList()));
    }

    [HttpPost("api/children/{childId:int}/advocate/threads")]
    public async Task<IActionResult> CreateThread(int childId, [FromBody] CreateAdvocateThreadRequest? request, CancellationToken ct)
    {
        var result = await _service.CreateThreadAsync(User.GetUserId(), childId, request?.Title, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Creation failed");
        return Created($"/api/advocate/threads/{result.Data!.Id}", ApiResponse<AdvocateThreadDto>.SuccessResponse(MapThread(result.Data)));
    }

    [HttpGet("api/advocate/threads/{id:int}")]
    public async Task<IActionResult> GetThread(int id, CancellationToken ct)
    {
        var result = await _service.GetThreadAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        var t = result.Data!;
        var dto = new AdvocateThreadDetailDto
        {
            Id = t.Id,
            ChildProfileId = t.ChildProfileId,
            Title = t.Title,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            LastMessageAt = t.LastMessageAt,
            Messages = t.Messages.Select(MapMessage).ToList(),
            Disclaimer = AdvocatePrompts.Disclaimer
        };
        return Ok(ApiResponse<AdvocateThreadDetailDto>.SuccessResponse(dto));
    }

    [HttpPatch("api/advocate/threads/{id:int}")]
    public async Task<IActionResult> RenameThread(int id, [FromBody] RenameAdvocateThreadRequest request, CancellationToken ct)
    {
        var result = await _service.RenameThreadAsync(User.GetUserId(), id, request.Title, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Update failed");
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    [HttpDelete("api/advocate/threads/{id:int}")]
    public async Task<IActionResult> DeleteThread(int id, CancellationToken ct)
    {
        var result = await _service.DeleteThreadAsync(User.GetUserId(), id, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    [HttpGet("api/advocate/usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct)
    {
        var result = await _service.GetUsageAsync(User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        var u = result.Data!;
        return Ok(ApiResponse<AdvocateUsageDto>.SuccessResponse(new AdvocateUsageDto { Used = u.Used, Limit = u.Limit, SubscriptionActive = u.SubscriptionActive }));
    }

    /// <summary>
    /// Sends a message and streams the answer as server-sent events (<c>event: delta | tool | done | error</c>).
    /// Failures decidable before the model is called come back as ordinary JSON status codes (400 validation,
    /// 403 forbidden, 404 not found, 429 usage cap); once the stream has started, a failure is an
    /// <c>error</c> frame.
    /// </summary>
    [HttpPost("api/advocate/threads/{id:int}/messages")]
    [EnableRateLimiting("advocate-message")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendAdvocateMessageRequest request, CancellationToken ct)
    {
        var events = _service.SendMessageAsync(User.GetUserId(), id, request.Text, request.About, ct);
        await using var enumerator = events.GetAsyncEnumerator(ct);

        if (!await enumerator.MoveNextAsync())
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Error(AdvocatePrompts.UnavailableMessage));

        var first = enumerator.Current;
        if (first.Kind == AdvocateStreamEventKind.Error && first.Code != AdvocateErrorCodes.Unavailable)
        {
            var status = first.Code switch
            {
                AdvocateErrorCodes.Validation => StatusCodes.Status400BadRequest,
                AdvocateErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
                AdvocateErrorCodes.NotFound => StatusCodes.Status404NotFound,
                AdvocateErrorCodes.UsageCap => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status400BadRequest
            };
            return StatusCode(status, ApiResponse<object>.Error(first.Message ?? "Request failed"));
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await Response.StartAsync(ct);

        var body = Response.Body;
        await WriteFrameAsync(body, first, ct);
        while (await SseWriter.AwaitWithPingsAsync(body, enumerator.MoveNextAsync().AsTask(), PingInterval, ct))
        {
            await WriteFrameAsync(body, enumerator.Current, ct);
        }

        return new EmptyResult();
    }

    private static Task WriteFrameAsync(Stream body, AdvocateStreamEvent evt, CancellationToken ct) => evt.Kind switch
    {
        AdvocateStreamEventKind.Delta => SseWriter.WriteEventAsync(body, "delta", new AdvocateDeltaFrame { Text = evt.Text ?? string.Empty }, ct),
        AdvocateStreamEventKind.Tool => SseWriter.WriteEventAsync(body, "tool", new AdvocateToolFrame { Name = evt.ToolName ?? string.Empty, Label = evt.ToolLabel ?? string.Empty, Status = evt.ToolStatus ?? string.Empty }, ct),
        AdvocateStreamEventKind.Done => SseWriter.WriteEventAsync(body, "done", new AdvocateDoneFrame
        {
            MessageId = evt.MessageId ?? 0,
            ContentMarkdown = evt.ContentMarkdown ?? string.Empty,
            Citations = (evt.Citations ?? new List<AdvocateCitation>()).Select(MapCitation).ToList(),
            Suggestions = (evt.Suggestions ?? new List<AdvocateSuggestion>()).Select(MapSuggestion).ToList(),
            Truncated = evt.Truncated,
            Disclaimer = evt.Disclaimer ?? AdvocatePrompts.Disclaimer
        }, ct),
        AdvocateStreamEventKind.Error => SseWriter.WriteEventAsync(body, "error", new AdvocateErrorFrame { Code = evt.Code ?? AdvocateErrorCodes.Unavailable, Message = evt.Message ?? AdvocatePrompts.UnavailableMessage }, ct),
        _ => Task.CompletedTask
    };

    /// <summary>"not found" (unknown child/thread or no access) ⇒ 404; anything else ⇒ 400.</summary>
    private IActionResult MapFailure(string message)
        => message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(ApiResponse<object>.Error(message))
            : BadRequest(ApiResponse<object>.Error(message));

    private static AdvocateThreadDto MapThread(AdvocateThreadModel t) => new()
    {
        Id = t.Id,
        ChildProfileId = t.ChildProfileId,
        Title = t.Title,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        LastMessageAt = t.LastMessageAt
    };

    private static AdvocateMessageDto MapMessage(AdvocateMessageModel m) => new()
    {
        Id = m.Id,
        Role = m.Role.ToString(),
        ContentMarkdown = m.ContentMarkdown,
        Citations = m.Citations.Select(MapCitation).ToList(),
        Suggestions = m.Suggestions.Select(MapSuggestion).ToList(),
        Truncated = m.Truncated,
        CreatedAt = m.CreatedAt
    };

    private static AdvocateCitationDto MapCitation(AdvocateCitation c) => new() { Kind = c.Kind, Id = c.Id, Label = c.Label };

    private static AdvocateSuggestionDto MapSuggestion(AdvocateSuggestion s) => new() { Kind = s.Kind, Text = s.Text, Id = s.Id, Date = s.Date };
}
