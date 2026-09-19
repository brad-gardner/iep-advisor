using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Journal;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>Parent-private journal about a child. No educator endpoint by design.</summary>
[ApiController]
[Authorize]
public class JournalController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IJournalService _service;

    public JournalController(IJournalService service)
    {
        _service = service;
    }

    /// <summary>Newest first by the day it happened. <c>tag</c> filters; <c>take</c> caps (default 50, max 200).</summary>
    [HttpGet("api/children/{childId:int}/journal")]
    public async Task<IActionResult> GetForChild(int childId, [FromQuery] string? tag, [FromQuery] int? take, CancellationToken ct)
    {
        JournalTag? tagFilter = null;
        if (!string.IsNullOrWhiteSpace(tag))
        {
            if (!TryParseTag(tag, out var parsed)) return BadRequest(ApiResponse<object>.Error("Invalid tag."));
            tagFilter = parsed;
        }

        var result = await _service.GetForChildAsync(childId, User.GetUserId(), tagFilter, take, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        return Ok(ApiResponse<List<JournalEntryDto>>.SuccessResponse(result.Data!.Select(Map).ToList()));
    }

    [HttpPost("api/children/{childId:int}/journal")]
    public async Task<IActionResult> Create(int childId, [FromBody] SaveJournalEntryRequest request, CancellationToken ct)
    {
        if (!TryBuildModel(request, out var model, out var error)) return BadRequest(ApiResponse<object>.Error(error));
        var result = await _service.CreateAsync(childId, User.GetUserId(), model, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Creation failed");
        return Created($"/api/journal/{result.Data!.Id}", ApiResponse<JournalEntryDto>.SuccessResponse(Map(result.Data)));
    }

    [HttpPut("api/journal/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveJournalEntryRequest request, CancellationToken ct)
    {
        if (!TryBuildModel(request, out var model, out var error)) return BadRequest(ApiResponse<object>.Error(error));
        var result = await _service.UpdateAsync(id, User.GetUserId(), model, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Update failed");
        return Ok(ApiResponse<JournalEntryDto>.SuccessResponse(Map(result.Data!)));
    }

    [HttpDelete("api/journal/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, User.GetUserId(), ct);
        if (!result.Success) return NotFound(ApiResponse<object>.Error(result.Message ?? "Not found"));
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    /// <summary>"not found" (unknown child/entry or no access) ⇒ 404; validation (including an unresolvable link) ⇒ 400.</summary>
    private IActionResult MapFailure(string message)
        => message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(ApiResponse<object>.Error(message))
            : BadRequest(ApiResponse<object>.Error(message));

    private static bool TryBuildModel(SaveJournalEntryRequest request, out SaveJournalEntryModel model, out string error)
    {
        model = new SaveJournalEntryModel();
        if (!TryParseTag(request.Tag, out var tag)) { error = "Invalid tag."; return false; }
        if (!DateOnly.TryParseExact(request.OccurredOn, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredOn))
        {
            error = "occurredOn must be a date in yyyy-MM-dd format.";
            return false;
        }

        model = new SaveJournalEntryModel
        {
            OccurredOn = occurredOn,
            Tag = tag,
            ContentMarkdown = request.ContentMarkdown,
            LinkedIepDocumentId = request.LinkedIepDocumentId,
            LinkedEtrDocumentId = request.LinkedEtrDocumentId,
            LinkedMeetingId = request.LinkedMeetingId
        };
        error = string.Empty;
        return true;
    }

    private static bool TryParseTag(string value, out JournalTag tag)
        => Enum.TryParse(value, ignoreCase: true, out tag) && Enum.IsDefined(tag);

    private static JournalEntryDto Map(JournalEntryModel m) => new()
    {
        Id = m.Id,
        ChildProfileId = m.ChildProfileId,
        OccurredOn = m.OccurredOn.ToString(DateFormat, CultureInfo.InvariantCulture),
        Tag = m.Tag.ToString(),
        ContentMarkdown = m.ContentMarkdown,
        LinkedIepDocumentId = m.LinkedIepDocumentId,
        LinkedEtrDocumentId = m.LinkedEtrDocumentId,
        LinkedMeetingId = m.LinkedMeetingId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        CreatedById = m.CreatedById
    };
}
