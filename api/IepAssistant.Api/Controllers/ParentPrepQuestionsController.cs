using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.MeetingPrep;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>The parent's own meeting-prep questions for a child. Parent-private; no educator endpoint by design.</summary>
[ApiController]
[Authorize]
public class ParentPrepQuestionsController : ControllerBase
{
    private readonly IParentPrepQuestionService _service;

    public ParentPrepQuestionsController(IParentPrepQuestionService service)
    {
        _service = service;
    }

    /// <summary>In display order.</summary>
    [HttpGet("api/children/{childId:int}/prep-questions")]
    public async Task<IActionResult> GetForChild(int childId, CancellationToken ct)
    {
        var result = await _service.GetForChildAsync(childId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message ?? "Not found");
        return Ok(ApiResponse<List<ParentPrepQuestionDto>>.SuccessResponse(result.Data!.Select(m => Map(m)).ToList()));
    }

    /// <summary>201 with the new question; 200 with the existing one (<c>alreadyExisted: true</c>) when the text is already on the list.</summary>
    [HttpPost("api/children/{childId:int}/prep-questions")]
    public async Task<IActionResult> Add(int childId, [FromBody] AddParentPrepQuestionRequest request, CancellationToken ct)
    {
        var result = await _service.AddAsync(childId, User.GetUserId(), request.Text, request.Source, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Creation failed");

        var dto = Map(result.Data!.Question, result.Data.AlreadyExisted);
        var body = ApiResponse<ParentPrepQuestionDto>.SuccessResponse(dto);
        return result.Data.AlreadyExisted ? Ok(body) : Created($"/api/prep-questions/{dto.Id}", body);
    }

    [HttpPut("api/prep-questions/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParentPrepQuestionRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, User.GetUserId(), request.Text, request.IsChecked, ct);
        if (!result.Success) return MapFailure(result.Message ?? "Update failed");
        return Ok(ApiResponse<ParentPrepQuestionDto>.SuccessResponse(Map(result.Data!)));
    }

    /// <summary>Returns the whole list in its new order.</summary>
    [HttpPut("api/children/{childId:int}/prep-questions/order")]
    public async Task<IActionResult> Reorder(int childId, [FromBody] ReorderParentPrepQuestionsRequest request, CancellationToken ct)
    {
        var result = await _service.ReorderAsync(childId, User.GetUserId(), request.Ids ?? new List<int>(), ct);
        if (!result.Success) return MapFailure(result.Message ?? "Reorder failed");
        return Ok(ApiResponse<List<ParentPrepQuestionDto>>.SuccessResponse(result.Data!.Select(m => Map(m)).ToList()));
    }

    [HttpDelete("api/prep-questions/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, User.GetUserId(), ct);
        if (!result.Success) return NotFound(ApiResponse<object>.Error(result.Message ?? "Not found"));
        return Ok(ApiResponse<object>.SuccessResponse(new { }));
    }

    /// <summary>"not found" (unknown child/question or no access) ⇒ 404; validation ⇒ 400.</summary>
    private IActionResult MapFailure(string message)
        => message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(ApiResponse<object>.Error(message))
            : BadRequest(ApiResponse<object>.Error(message));

    private static ParentPrepQuestionDto Map(ParentPrepQuestionModel m, bool? alreadyExisted = null) => new()
    {
        Id = m.Id,
        ChildProfileId = m.ChildProfileId,
        Text = m.Text,
        IsChecked = m.IsChecked,
        DisplayOrder = m.DisplayOrder,
        Source = m.Source,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        AlreadyExisted = alreadyExisted
    };
}
