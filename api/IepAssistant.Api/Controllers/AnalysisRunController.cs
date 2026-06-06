using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.AnalysisRuns;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class AnalysisRunController : ControllerBase
{
    private readonly IAnalysisRunService _analysisRunService;
    private readonly AnalysisRunQueue _queue;
    private readonly IFeatureFlags _featureFlags;

    public AnalysisRunController(
        IAnalysisRunService analysisRunService,
        AnalysisRunQueue queue,
        IFeatureFlags featureFlags)
    {
        _analysisRunService = analysisRunService;
        _queue = queue;
        _featureFlags = featureFlags;
    }

    [HttpPost("api/children/{childId}/analysis-runs")]
    [ProducesResponseType(typeof(ApiResponse<AnalysisRunDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateAnalysisRunRequest request, CancellationToken cancellationToken)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.AnalysisRun))
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var sources = new List<AnalysisRunSourceRef>();
        foreach (var s in request.Sources)
        {
            if (!Enum.TryParse<AnalysisSourceType>(s.SourceType, ignoreCase: true, out var parsedType))
                return BadRequest(ApiResponse<object>.Error($"Invalid source type: {s.SourceType}"));
            sources.Add(new AnalysisRunSourceRef(parsedType, s.SourceId));
        }

        var userId = User.GetUserId();
        var result = await _analysisRunService.CreateRunAsync(childId, userId, sources, cancellationToken);

        if (!result.Success)
            return MapFailure(result.Message);

        var run = result.Data!;
        await _queue.EnqueueAsync(run.Id, cancellationToken);

        var dto = MapToDto(run);
        return CreatedAtAction(nameof(GetById), new { childId, runId = dto.Id },
            ApiResponse<AnalysisRunDto>.SuccessResponse(dto, result.Message ?? "Analysis run queued"));
    }

    [HttpGet("api/children/{childId}/analysis-runs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AnalysisRunDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByChild(int childId, CancellationToken cancellationToken)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.AnalysisRun))
            return NotFound();

        var userId = User.GetUserId();
        var result = await _analysisRunService.GetRunsAsync(childId, userId, cancellationToken);

        if (!result.Success)
            return MapFailure(result.Message);

        var dtos = result.Data!.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<AnalysisRunDto>>.SuccessResponse(dtos));
    }

    [HttpGet("api/children/{childId}/analysis-runs/{runId}")]
    [ProducesResponseType(typeof(ApiResponse<AnalysisRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int childId, int runId, CancellationToken cancellationToken)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.AnalysisRun))
            return NotFound();

        var userId = User.GetUserId();
        var result = await _analysisRunService.GetRunAsync(runId, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Analysis run not found"));

        return Ok(ApiResponse<AnalysisRunDto>.SuccessResponse(MapToDto(result.Data!)));
    }

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("subscription", StringComparison.OrdinalIgnoreCase))
            return StatusCode(402, ApiResponse<object>.Error(message));

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }

    private static AnalysisRunDto MapToDto(AnalysisRunModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        Status = model.Status,
        OverallSummary = model.OverallSummary,
        CrossDocSynthesis = model.CrossDocSynthesis,
        OverallRedFlags = model.OverallRedFlags,
        AdvocacyGapAnalysis = model.AdvocacyGapAnalysis,
        ParentGoalsSnapshot = model.ParentGoalsSnapshot,
        ErrorMessage = model.ErrorMessage,
        CreatedAt = model.CreatedAt,
        Sources = model.Sources.Select(s => new AnalysisRunSourceDto
        {
            Id = s.Id,
            SourceType = s.SourceType,
            SourceId = s.SourceId,
            SourceLabel = s.SourceLabel
        }).ToList(),
        Sections = model.Sections.Select(s => new AnalysisRunSectionDto
        {
            Id = s.Id,
            AnalysisRunSourceId = s.AnalysisRunSourceId,
            SectionKind = s.SectionKind,
            Analysis = s.Analysis,
            DisplayOrder = s.DisplayOrder
        }).ToList()
    };
}
