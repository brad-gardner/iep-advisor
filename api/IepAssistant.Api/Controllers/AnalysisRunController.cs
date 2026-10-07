using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public AnalysisRunController(
        IAnalysisRunService analysisRunService,
        AnalysisRunQueue queue)
    {
        _analysisRunService = analysisRunService;
        _queue = queue;
    }

    [HttpPost("api/children/{childId}/analysis-runs")]
    [EnableRateLimiting("analysis-run")]
    [ProducesResponseType(typeof(ApiResponse<AnalysisRunDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateAnalysisRunRequest request, CancellationToken cancellationToken)
    {
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
            return this.MapServiceFailure(result);

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
        var userId = User.GetUserId();
        var result = await _analysisRunService.GetRunsAsync(childId, userId, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        var dtos = result.Data!.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<AnalysisRunDto>>.SuccessResponse(dtos));
    }

    // Declared ahead of GetById so the literal "latest" segment is matched before {runId:int} is even
    // considered — ASP.NET Core's routing already prefers a literal segment over a parameter at the same
    // position regardless of declaration order, but the explicit :int constraint on GetById below removes
    // any ambiguity rather than relying on that precedence alone.
    [HttpGet("api/children/{childId}/analysis-runs/latest")]
    [ProducesResponseType(typeof(ApiResponse<AnalysisRunLatestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestForSource(
        int childId, [FromQuery] string sourceType, [FromQuery] int sourceId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AnalysisSourceType>(sourceType, ignoreCase: true, out var parsedType))
            return BadRequest(ApiResponse<object>.Error($"Invalid source type: {sourceType}"));

        var userId = User.GetUserId();
        var result = await _analysisRunService.GetLatestForSourceAsync(childId, parsedType, sourceId, userId, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        return Ok(ApiResponse<AnalysisRunLatestDto>.SuccessResponse(MapToLatestDto(result.Data!)));
    }

    [HttpGet("api/children/{childId}/analysis-runs/{runId:int}")]
    [ProducesResponseType(typeof(ApiResponse<AnalysisRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int childId, int runId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _analysisRunService.GetRunAsync(runId, userId, cancellationToken);

        if (!result.Success)
            return this.MapServiceFailure(result);

        return Ok(ApiResponse<AnalysisRunDto>.SuccessResponse(MapToDto(result.Data!)));
    }

    private static AnalysisRunDto MapToDto(AnalysisRunModel model)
    {
        var dto = new AnalysisRunDto();
        PopulateDto(dto, model);
        return dto;
    }

    private static AnalysisRunLatestDto MapToLatestDto(AnalysisRunLatestModel model)
    {
        var dto = new AnalysisRunLatestDto
        {
            Stale = model.Stale,
            OtherSources = model.OtherSources.Select(s => new AnalysisRunOtherSourceDto
            {
                SourceType = s.SourceType,
                SourceId = s.SourceId,
                Label = s.Label
            }).ToList()
        };
        PopulateDto(dto, model);
        return dto;
    }

    private static void PopulateDto(AnalysisRunDto dto, AnalysisRunModel model)
    {
        dto.Id = model.Id;
        dto.ChildProfileId = model.ChildProfileId;
        dto.Status = model.Status;
        dto.OverallSummary = model.OverallSummary;
        dto.CrossDocSynthesis = model.CrossDocSynthesis;
        dto.OverallRedFlags = model.OverallRedFlags;
        dto.AdvocacyGapAnalysis = model.AdvocacyGapAnalysis;
        dto.ParentGoalsSnapshot = model.ParentGoalsSnapshot;
        dto.ErrorMessage = model.ErrorMessage;
        dto.GeneratedLanguage = model.GeneratedLanguage;
        dto.CreatedAt = model.CreatedAt;
        dto.Sources = model.Sources.Select(s => new AnalysisRunSourceDto
        {
            Id = s.Id,
            SourceType = s.SourceType,
            SourceId = s.SourceId,
            SourceLabel = s.SourceLabel,
            Status = s.Status,
            ErrorMessage = s.ErrorMessage
        }).ToList();
        dto.Sections = model.Sections.Select(s => new AnalysisRunSectionDto
        {
            Id = s.Id,
            AnalysisRunSourceId = s.AnalysisRunSourceId,
            SectionKind = s.SectionKind,
            Analysis = s.Analysis,
            GoalAnalyses = s.GoalAnalyses,
            EtrCompleteness = s.EtrCompleteness,
            EtrEligibility = s.EtrEligibility,
            DisplayOrder = s.DisplayOrder
        }).ToList();
    }
}
