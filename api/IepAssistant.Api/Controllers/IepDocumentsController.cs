using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepDocuments;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class IepDocumentsController : ControllerBase
{
    private readonly IIepDocumentService _iepDocumentService;
    private readonly IIepProcessingService _iepProcessingService;
    private readonly IIepAnalysisService _analysisService;
    private readonly IepProcessingQueue _processingQueue;
    private readonly IepAnalysisQueue _analysisQueue;

    public IepDocumentsController(
        IIepDocumentService iepDocumentService,
        IIepProcessingService iepProcessingService,
        IIepAnalysisService analysisService,
        IepProcessingQueue processingQueue,
        IepAnalysisQueue analysisQueue)
    {
        _iepDocumentService = iepDocumentService;
        _iepProcessingService = iepProcessingService;
        _analysisService = analysisService;
        _processingQueue = processingQueue;
        _analysisQueue = analysisQueue;
    }

    [HttpGet("api/children/{childId}/ieps")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<IepDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByChild(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var documents = await _iepDocumentService.GetByChildIdAsync(childId, userId, cancellationToken);
        var dtos = documents.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<IepDocumentDto>>.SuccessResponse(dtos));
    }

    [HttpGet("api/ieps/{id}")]
    [ProducesResponseType(typeof(ApiResponse<IepDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _iepDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("IEP document not found"));

        return Ok(ApiResponse<IepDocumentDto>.SuccessResponse(MapToDto(document)));
    }

    [HttpPost("api/children/{childId}/ieps")]
    [ProducesResponseType(typeof(ApiResponse<IepDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> Upload(int childId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Error("No file provided"));

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Only PDF files are supported"));

        var userId = User.GetUserId();

        using var stream = file.OpenReadStream();
        var result = await _iepDocumentService.UploadAsync(childId, userId, file.FileName, stream, file.Length, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Upload failed"));

        var dto = MapToDto(result.Data!);

        // Enqueue background processing
        await _processingQueue.EnqueueAsync(dto.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<IepDocumentDto>.SuccessResponse(dto, "IEP document uploaded successfully"));
    }

    [HttpGet("api/ieps/{id}/download")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var url = await _iepDocumentService.GetDownloadUrlAsync(id, userId, cancellationToken);

        if (url == null)
            return NotFound(ApiResponse<object>.Error("Document not found"));

        return Ok(ApiResponse<object>.SuccessResponse(new { url }));
    }

    [HttpDelete("api/ieps/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _iepDocumentService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Delete failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Document deleted successfully"));
    }

    [HttpGet("api/ieps/{id}/sections")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSections(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var sections = await _iepProcessingService.GetSectionsAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<object>>.SuccessResponse(sections.Cast<object>()));
    }

    [HttpPost("api/ieps/{id}/process")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reprocess(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _iepDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("Document not found"));

        await _processingQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, "Document queued for processing"));
    }

    [HttpPost("api/ieps/{id}/analyze")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyze(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _iepDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("Document not found"));

        if (document.Status != "parsed")
            return BadRequest(ApiResponse<object>.Error("Document must be parsed before analysis"));

        await _analysisQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, "Analysis queued"));
    }

    [HttpGet("api/ieps/{id}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<IepAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var analysis = await _analysisService.GetAnalysisAsync(id, userId, cancellationToken);

        if (analysis == null)
            return NotFound(ApiResponse<object>.Error("No analysis found for this document"));

        return Ok(ApiResponse<IepAnalysisDto>.SuccessResponse(MapToAnalysisDto(analysis)));
    }

    private static IepAnalysisDto MapToAnalysisDto(Services.Models.IepAnalysisModel model) => new()
    {
        Id = model.Id,
        IepDocumentId = model.IepDocumentId,
        Status = model.Status,
        OverallSummary = model.OverallSummary,
        SectionAnalyses = model.SectionAnalyses,
        GoalAnalyses = model.GoalAnalyses,
        OverallRedFlags = model.OverallRedFlags,
        SuggestedQuestions = model.SuggestedQuestions,
        AdvocacyGapAnalysis = model.AdvocacyGapAnalysis,
        ParentGoalsSnapshot = model.ParentGoalsSnapshot,
        ErrorMessage = model.ErrorMessage,
        CreatedAt = model.CreatedAt,
    };

    private static IepDocumentDto MapToDto(Services.Models.IepDocumentModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        FileName = model.FileName,
        UploadDate = model.UploadDate,
        IepDate = model.IepDate,
        Status = model.Status,
        FileSizeBytes = model.FileSizeBytes,
        CreatedAt = model.CreatedAt
    };
}
