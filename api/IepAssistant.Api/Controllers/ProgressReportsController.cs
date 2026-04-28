using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.ProgressReports;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class ProgressReportsController : ControllerBase
{
    private readonly IProgressReportService _service;
    private readonly IProgressReportAnalysisService _analysisService;
    private readonly ProgressReportAnalysisQueue _analysisQueue;

    public ProgressReportsController(
        IProgressReportService service,
        IProgressReportAnalysisService analysisService,
        ProgressReportAnalysisQueue analysisQueue)
    {
        _service = service;
        _analysisService = analysisService;
        _analysisQueue = analysisQueue;
    }

    [HttpGet("api/ieps/{iepId}/progress-reports")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProgressReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIep(int iepId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var reports = await _service.GetByIepIdAsync(iepId, userId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ProgressReportDto>>.SuccessResponse(reports.Select(MapToDto)));
    }

    [HttpGet("api/progress-reports/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProgressReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var report = await _service.GetByIdAsync(id, userId, cancellationToken);
        if (report == null)
            return NotFound(ApiResponse<object>.Error("Progress report not found"));

        return Ok(ApiResponse<ProgressReportDto>.SuccessResponse(MapToDto(report)));
    }

    [HttpPost("api/ieps/{iepId}/progress-reports")]
    [ProducesResponseType(typeof(ApiResponse<ProgressReportDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(int iepId, [FromBody] CreateProgressReportRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new CreateProgressReportModel
        {
            ReportingPeriodStart = request.ReportingPeriodStart,
            ReportingPeriodEnd = request.ReportingPeriodEnd,
            Notes = request.Notes
        };

        var result = await _service.CreateAsync(iepId, userId, model, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Creation failed"));

        var dto = MapToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<ProgressReportDto>.SuccessResponse(dto, "Progress report created"));
    }

    [HttpPost("api/progress-reports/{id}/upload")]
    [ProducesResponseType(typeof(ApiResponse<ProgressReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> AttachFile(int id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Error("No file provided"));

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Only PDF files are supported"));

        using var stream = file.OpenReadStream();
        var header = new byte[5];
        var bytesRead = await stream.ReadAsync(header, 0, 5, cancellationToken);
        if (bytesRead < 5 || System.Text.Encoding.ASCII.GetString(header) != "%PDF-")
            return BadRequest(ApiResponse<object>.Error("File does not appear to be a valid PDF"));
        stream.Position = 0;

        var sanitized = Path.GetFileName(file.FileName);
        var userId = User.GetUserId();
        var result = await _service.AttachFileAsync(id, userId, sanitized, stream, file.Length, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Upload failed"));

        // Kick off analysis in the background as soon as the file is attached.
        await _analysisQueue.EnqueueAsync(result.Data!.Id, cancellationToken);

        return Ok(ApiResponse<ProgressReportDto>.SuccessResponse(MapToDto(result.Data!), "File attached"));
    }

    [HttpPut("api/progress-reports/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(int id, [FromBody] CreateProgressReportRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new CreateProgressReportModel
        {
            ReportingPeriodStart = request.ReportingPeriodStart,
            ReportingPeriodEnd = request.ReportingPeriodEnd,
            Notes = request.Notes
        };

        var result = await _service.UpdateMetadataAsync(id, userId, model, cancellationToken);
        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Update failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Metadata updated"));
    }

    [HttpGet("api/progress-reports/{id}/download")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var url = await _service.GetDownloadUrlAsync(id, userId, cancellationToken);
        if (url == null)
            return NotFound(ApiResponse<object>.Error("Progress report not found"));

        return Ok(ApiResponse<object>.SuccessResponse(new { url }));
    }

    [HttpGet("api/progress-reports/{id}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<ProgressReportAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var analysis = await _analysisService.GetAnalysisAsync(id, userId, cancellationToken);
        if (analysis == null)
            return NotFound(ApiResponse<object>.Error("Analysis not found"));

        return Ok(ApiResponse<ProgressReportAnalysisDto>.SuccessResponse(MapAnalysisToDto(analysis)));
    }

    [HttpPost("api/progress-reports/{id}/analyze")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartAnalysis(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var report = await _service.GetByIdAsync(id, userId, cancellationToken);
        if (report == null)
            return NotFound(ApiResponse<object>.Error("Progress report not found"));

        if (string.IsNullOrEmpty(report.FileName))
            return BadRequest(ApiResponse<object>.Error("Upload the progress report PDF before running analysis."));

        await _analysisQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, "Analysis queued"));
    }

    [HttpDelete("api/progress-reports/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _service.DeleteAsync(id, userId, cancellationToken);
        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Delete failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Progress report deleted"));
    }

    private static ProgressReportAnalysisDto MapAnalysisToDto(ProgressReportAnalysisModel m) => new()
    {
        Id = m.Id,
        ProgressReportId = m.ProgressReportId,
        Status = m.Status,
        Summary = m.Summary,
        GoalProgressFindings = m.GoalProgressFindings,
        RedFlags = m.RedFlags,
        AdvocacyGapAnalysis = m.AdvocacyGapAnalysis,
        ParentGoalsSnapshot = m.ParentGoalsSnapshot,
        IepGoalsSnapshot = m.IepGoalsSnapshot,
        ErrorMessage = m.ErrorMessage,
        CreatedAt = m.CreatedAt
    };

    private static ProgressReportDto MapToDto(ProgressReportModel m) => new()
    {
        Id = m.Id,
        IepDocumentId = m.IepDocumentId,
        ChildProfileId = m.ChildProfileId,
        FileName = m.FileName,
        UploadDate = m.UploadDate,
        ReportingPeriodStart = m.ReportingPeriodStart,
        ReportingPeriodEnd = m.ReportingPeriodEnd,
        Notes = m.Notes,
        Status = m.Status,
        ErrorMessage = m.ErrorMessage,
        FileSizeBytes = m.FileSizeBytes,
        CreatedAt = m.CreatedAt
    };
}
