using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepDrafts;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class IepDraftController : ControllerBase
{
    private readonly IIepDraftService _service;
    private readonly IFeatureFlags _featureFlags;

    public IepDraftController(IIepDraftService service, IFeatureFlags featureFlags)
    {
        _service = service;
        _featureFlags = featureFlags;
    }

    // ---------------------------------------------------------------- Drafts

    [HttpPost("api/educator/students/{studentId}/iep-drafts")]
    [ProducesResponseType(typeof(ApiResponse<IepDraftDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDraft(int studentId, [FromBody] CreateIepDraftRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.CreateDraftAsync(User.GetUserId(), studentId, request.Title, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = IepDraftMappers.MapDraft(result.Data!);
        return CreatedAtAction(nameof(GetDraft), new { draftId = dto.Id }, ApiResponse<IepDraftDto>.SuccessResponse(dto));
    }

    [HttpGet("api/educator/students/{studentId}/iep-drafts")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IepDraftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListDrafts(int studentId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.ListDraftsAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<IepDraftDto>>.SuccessResponse(result.Data!.Select(IepDraftMappers.MapDraft)));
    }

    [HttpGet("api/iep-drafts/{draftId}")]
    [ProducesResponseType(typeof(ApiResponse<IepDraftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDraft(int draftId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var result = await _service.GetDraftAsync(User.GetUserId(), draftId, ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IepDraftDto>.SuccessResponse(IepDraftMappers.MapDraft(result.Data!)));
    }

    // ---------------------------------------------------------------- Sections

    [HttpPost("api/iep-drafts/{draftId}/sections")]
    public async Task<IActionResult> AddSection(int draftId, [FromBody] UpsertSectionRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AddSectionAsync(User.GetUserId(), draftId, new UpsertIepDraftSectionModel
        {
            SectionKind = request.SectionKind,
            RichText = request.RichText
        }, ct);
        return result.Success
            ? Ok(ApiResponse<SectionDto>.SuccessResponse(IepDraftMappers.MapSection(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpPut("api/iep-drafts/{draftId}/sections/{id}")]
    public async Task<IActionResult> UpdateSection(int draftId, int id, [FromBody] UpsertSectionRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateSectionAsync(User.GetUserId(), draftId, id, new UpsertIepDraftSectionModel
        {
            SectionKind = request.SectionKind,
            RichText = request.RichText
        }, ct);
        return result.Success
            ? Ok(ApiResponse<SectionDto>.SuccessResponse(IepDraftMappers.MapSection(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpDelete("api/iep-drafts/{draftId}/sections/{id}")]
    public async Task<IActionResult> DeleteSection(int draftId, int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var result = await _service.DeleteSectionAsync(User.GetUserId(), draftId, id, ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    // ---------------------------------------------------------------- Goals

    [HttpPost("api/iep-drafts/{draftId}/goals")]
    public async Task<IActionResult> AddGoal(int draftId, [FromBody] UpsertGoalRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AddGoalAsync(User.GetUserId(), draftId, MapGoalInput(request), ct);
        return result.Success
            ? Ok(ApiResponse<GoalDto>.SuccessResponse(IepDraftMappers.MapGoal(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpPut("api/iep-drafts/{draftId}/goals/{id}")]
    public async Task<IActionResult> UpdateGoal(int draftId, int id, [FromBody] UpsertGoalRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateGoalAsync(User.GetUserId(), draftId, id, MapGoalInput(request), ct);
        return result.Success
            ? Ok(ApiResponse<GoalDto>.SuccessResponse(IepDraftMappers.MapGoal(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpDelete("api/iep-drafts/{draftId}/goals/{id}")]
    public async Task<IActionResult> DeleteGoal(int draftId, int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var result = await _service.DeleteGoalAsync(User.GetUserId(), draftId, id, ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    // ---------------------------------------------------------------- Service lines

    [HttpPost("api/iep-drafts/{draftId}/service-lines")]
    public async Task<IActionResult> AddServiceLine(int draftId, [FromBody] UpsertServiceLineRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AddServiceLineAsync(User.GetUserId(), draftId, MapServiceLineInput(request), ct);
        return result.Success
            ? Ok(ApiResponse<ServiceLineDto>.SuccessResponse(IepDraftMappers.MapServiceLine(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpPut("api/iep-drafts/{draftId}/service-lines/{id}")]
    public async Task<IActionResult> UpdateServiceLine(int draftId, int id, [FromBody] UpsertServiceLineRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateServiceLineAsync(User.GetUserId(), draftId, id, MapServiceLineInput(request), ct);
        return result.Success
            ? Ok(ApiResponse<ServiceLineDto>.SuccessResponse(IepDraftMappers.MapServiceLine(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpDelete("api/iep-drafts/{draftId}/service-lines/{id}")]
    public async Task<IActionResult> DeleteServiceLine(int draftId, int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var result = await _service.DeleteServiceLineAsync(User.GetUserId(), draftId, id, ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    // ---------------------------------------------------------------- Accommodations

    [HttpPost("api/iep-drafts/{draftId}/accommodations")]
    public async Task<IActionResult> AddAccommodation(int draftId, [FromBody] UpsertAccommodationRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AddAccommodationAsync(User.GetUserId(), draftId, new UpsertIepDraftAccommodationModel
        {
            Category = request.Category,
            Text = request.Text
        }, ct);
        return result.Success
            ? Ok(ApiResponse<AccommodationDto>.SuccessResponse(IepDraftMappers.MapAccommodation(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpPut("api/iep-drafts/{draftId}/accommodations/{id}")]
    public async Task<IActionResult> UpdateAccommodation(int draftId, int id, [FromBody] UpsertAccommodationRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateAccommodationAsync(User.GetUserId(), draftId, id, new UpsertIepDraftAccommodationModel
        {
            Category = request.Category,
            Text = request.Text
        }, ct);
        return result.Success
            ? Ok(ApiResponse<AccommodationDto>.SuccessResponse(IepDraftMappers.MapAccommodation(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpDelete("api/iep-drafts/{draftId}/accommodations/{id}")]
    public async Task<IActionResult> DeleteAccommodation(int draftId, int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var result = await _service.DeleteAccommodationAsync(User.GetUserId(), draftId, id, ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    // ---------------------------------------------------------------- Transition items

    [HttpPost("api/iep-drafts/{draftId}/transition-items")]
    public async Task<IActionResult> AddTransitionItem(int draftId, [FromBody] UpsertTransitionItemRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.AddTransitionItemAsync(User.GetUserId(), draftId, new UpsertIepDraftTransitionItemModel
        {
            PostsecondaryGoalArea = request.PostsecondaryGoalArea,
            ServicesText = request.ServicesText
        }, ct);
        return result.Success
            ? Ok(ApiResponse<TransitionItemDto>.SuccessResponse(IepDraftMappers.MapTransitionItem(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpPut("api/iep-drafts/{draftId}/transition-items/{id}")]
    public async Task<IActionResult> UpdateTransitionItem(int draftId, int id, [FromBody] UpsertTransitionItemRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.UpdateTransitionItemAsync(User.GetUserId(), draftId, id, new UpsertIepDraftTransitionItemModel
        {
            PostsecondaryGoalArea = request.PostsecondaryGoalArea,
            ServicesText = request.ServicesText
        }, ct);
        return result.Success
            ? Ok(ApiResponse<TransitionItemDto>.SuccessResponse(IepDraftMappers.MapTransitionItem(result.Data!)))
            : MapFailure(result.Message);
    }

    [HttpDelete("api/iep-drafts/{draftId}/transition-items/{id}")]
    public async Task<IActionResult> DeleteTransitionItem(int draftId, int id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var result = await _service.DeleteTransitionItemAsync(User.GetUserId(), draftId, id, ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    // ---------------------------------------------------------------- Helpers

    private bool Enabled => _featureFlags.IsEnabled(FeatureFlags.SchoolSide);

    private static UpsertIepDraftGoalModel MapGoalInput(UpsertGoalRequest r) => new()
    {
        Domain = r.Domain,
        GoalText = r.GoalText,
        Baseline = r.Baseline,
        TargetCriteria = r.TargetCriteria,
        MeasurementMethod = r.MeasurementMethod,
        Timeframe = r.Timeframe
    };

    private static UpsertIepDraftServiceLineModel MapServiceLineInput(UpsertServiceLineRequest r) => new()
    {
        ServiceType = r.ServiceType,
        Frequency = r.Frequency,
        Duration = r.Duration,
        Location = r.Location,
        ProviderRole = r.ProviderRole,
        StartDate = r.StartDate,
        EndDate = r.EndDate
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
