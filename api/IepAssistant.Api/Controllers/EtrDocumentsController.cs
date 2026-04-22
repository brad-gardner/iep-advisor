using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.EtrDocuments;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class EtrDocumentsController : ControllerBase
{
    private readonly IEtrDocumentService _etrDocumentService;
    private readonly IEtrProcessingService _etrProcessingService;
    private readonly IEtrAnalysisService _etrAnalysisService;
    private readonly IAccessService _accessService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly EtrProcessingQueue _processingQueue;
    private readonly EtrAnalysisQueue _analysisQueue;

    public EtrDocumentsController(
        IEtrDocumentService etrDocumentService,
        IEtrProcessingService etrProcessingService,
        IEtrAnalysisService etrAnalysisService,
        IAccessService accessService,
        ISubscriptionService subscriptionService,
        EtrProcessingQueue processingQueue,
        EtrAnalysisQueue analysisQueue)
    {
        _etrDocumentService = etrDocumentService;
        _etrProcessingService = etrProcessingService;
        _etrAnalysisService = etrAnalysisService;
        _accessService = accessService;
        _subscriptionService = subscriptionService;
        _processingQueue = processingQueue;
        _analysisQueue = analysisQueue;
    }

    [HttpGet("api/children/{childId}/etrs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<EtrDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByChild(int childId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var documents = await _etrDocumentService.GetByChildIdAsync(childId, userId, cancellationToken);
        var dtos = documents.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<EtrDocumentDto>>.SuccessResponse(dtos));
    }

    [HttpGet("api/etrs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<EtrDocumentListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var documents = await _etrDocumentService.GetAllForUserAsync(userId, cancellationToken);
        var dtos = documents.Select(MapToListItemDto).ToList();
        return Ok(ApiResponse<List<EtrDocumentListItemDto>>.SuccessResponse(dtos));
    }

    [HttpGet("api/etrs/{id}")]
    [ProducesResponseType(typeof(ApiResponse<EtrDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _etrDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("ETR document not found"));

        return Ok(ApiResponse<EtrDocumentDto>.SuccessResponse(MapToDto(document)));
    }

    [HttpPost("api/children/{childId}/etrs")]
    [ProducesResponseType(typeof(ApiResponse<EtrDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateEtrRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var userId = User.GetUserId();
        var model = new CreateEtrDocumentModel
        {
            EvaluationDate = request.EvaluationDate!.Value,
            EvaluationType = request.EvaluationType,
            DocumentState = request.DocumentState,
            Notes = request.Notes
        };

        var result = await _etrDocumentService.CreateAsync(childId, userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Creation failed"));

        var dto = MapToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<EtrDocumentDto>.SuccessResponse(dto, "ETR created successfully"));
    }

    [HttpPost("api/etrs/{id}/upload")]
    [ProducesResponseType(typeof(ApiResponse<EtrDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> Upload(int id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Error("No file provided"));

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error("Only PDF files are supported"));

        // Validate PDF magic bytes
        using var stream = file.OpenReadStream();
        var header = new byte[5];
        var bytesRead = await stream.ReadAsync(header, 0, 5, cancellationToken);
        if (bytesRead < 5 || System.Text.Encoding.ASCII.GetString(header) != "%PDF-")
            return BadRequest(ApiResponse<object>.Error("File does not appear to be a valid PDF"));
        stream.Position = 0;

        var sanitizedFileName = Path.GetFileName(file.FileName);
        var userId = User.GetUserId();

        // Ownership + role check before subscription check (mirror IEP: service also checks, but do explicit role gate here)
        var document = await _etrDocumentService.GetByIdAsync(id, userId, cancellationToken);
        if (document == null)
            return NotFound(ApiResponse<object>.Error("ETR document not found"));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error("Insufficient permissions"));

        if (!await _subscriptionService.HasActiveSubscriptionAsync(userId, cancellationToken))
            return StatusCode(402, ApiResponse<object>.Error("Active subscription required to upload and process ETR documents"));

        var result = await _etrDocumentService.AttachFileAsync(id, userId, sanitizedFileName, stream, file.Length, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Upload failed"));

        var dto = MapToDto(result.Data!);

        await _processingQueue.EnqueueAsync(dto.Id, cancellationToken);

        return Ok(ApiResponse<EtrDocumentDto>.SuccessResponse(dto, "File attached successfully"));
    }

    [HttpPut("api/etrs/{id}/metadata")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(int id, [FromBody] UpdateEtrMetadataRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new UpdateEtrMetadataModel
        {
            EvaluationDate = request.EvaluationDate,
            EvaluationType = request.EvaluationType,
            DocumentState = request.DocumentState,
            Notes = request.Notes
        };

        var result = await _etrDocumentService.UpdateMetadataAsync(id, userId, model, cancellationToken);

        if (!result.Success)
        {
            if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(ApiResponse<object>.Error(result.Message));
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Update failed"));
        }

        return Ok(ApiResponse<object>.SuccessResponse(null, "Metadata updated successfully"));
    }

    [HttpGet("api/etrs/{id}/download")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var url = await _etrDocumentService.GetDownloadUrlAsync(id, userId, cancellationToken);

        if (url == null)
            return NotFound(ApiResponse<object>.Error("Document not found"));

        return Ok(ApiResponse<object>.SuccessResponse(new { url }));
    }

    [HttpGet("api/etrs/{id}/sections")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSections(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var sections = await _etrProcessingService.GetSectionsAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<object>>.SuccessResponse(sections.Cast<object>()));
    }

    [HttpPost("api/etrs/{id}/process")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reprocess(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _etrDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("Document not found"));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error("Insufficient permissions"));

        if (document.Status == "processing")
            return Conflict(ApiResponse<object>.Error("Document is already being processed"));

        await _processingQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, "Document queued for processing"));
    }

    [HttpPost("api/etrs/{id}/analyze")]
    [ProducesResponseType(typeof(ApiResponse<EtrAnalysisDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Analyze(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var document = await _etrDocumentService.GetByIdAsync(id, userId, cancellationToken);

        if (document == null)
            return NotFound(ApiResponse<object>.Error("ETR document not found"));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error("Insufficient permissions"));

        if (document.Status != "parsed")
            return BadRequest(ApiResponse<object>.Error("ETR must be parsed before analysis"));

        // Subscription gate (mirrors IEP /analyze)
        if (!await _subscriptionService.HasActiveSubscriptionAsync(userId, cancellationToken))
            return StatusCode(402, ApiResponse<object>.Error("Active subscription required to analyze ETR documents"));

        // Per-child ETR analysis limit (distinct from IEP). Currently returns true; see TODO in EtrAnalysisService.
        if (!await _etrAnalysisService.CheckEtrAnalysisLimitAsync(userId, document.ChildProfileId, cancellationToken))
            return StatusCode(429, ApiResponse<object>.Error("ETR analysis limit reached for this child."));

        // Create/reset the analysis row so the caller can poll immediately.
        var existing = await _etrAnalysisService.GetAnalysisAsync(id, userId, cancellationToken);
        var dto = new EtrAnalysisDto
        {
            Id = existing?.Id ?? 0,
            EtrDocumentId = id,
            Status = "pending",
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
        };

        await _analysisQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<EtrAnalysisDto>.SuccessResponse(dto, "ETR analysis queued"));
    }

    [HttpGet("api/etrs/{id}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<EtrAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var analysis = await _etrAnalysisService.GetAnalysisAsync(id, userId, cancellationToken);

        if (analysis == null)
            return NotFound(ApiResponse<object>.Error("No analysis found for this ETR"));

        return Ok(ApiResponse<EtrAnalysisDto>.SuccessResponse(MapToAnalysisDto(analysis)));
    }

    private static EtrAnalysisDto MapToAnalysisDto(EtrAnalysisModel model) => new()
    {
        Id = model.Id,
        EtrDocumentId = model.EtrDocumentId,
        Status = model.Status,
        AssessmentCompleteness = model.AssessmentCompleteness,
        EligibilityReview = model.EligibilityReview,
        OverallRedFlags = model.OverallRedFlags,
        SuggestedQuestions = model.SuggestedQuestions,
        OverallSummary = model.OverallSummary,
        ErrorMessage = model.ErrorMessage,
        CreatedAt = model.CreatedAt,
    };

    [HttpDelete("api/etrs/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _etrDocumentService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? "Delete failed"));

        return Ok(ApiResponse<object>.SuccessResponse(null, "Document deleted successfully"));
    }

    private static EtrDocumentListItemDto MapToListItemDto(EtrDocumentListItemModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        FileName = model.FileName,
        UploadDate = model.UploadDate,
        EvaluationDate = model.EvaluationDate,
        EvaluationType = model.EvaluationType,
        DocumentState = model.DocumentState,
        Notes = model.Notes,
        Status = model.Status,
        FileSizeBytes = model.FileSizeBytes,
        CreatedAt = model.CreatedAt,
        ChildId = model.ChildId,
        ChildFirstName = model.ChildFirstName,
        ChildLastName = model.ChildLastName
    };

    private static EtrDocumentDto MapToDto(EtrDocumentModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        FileName = model.FileName,
        UploadDate = model.UploadDate,
        EvaluationDate = model.EvaluationDate,
        EvaluationType = model.EvaluationType,
        DocumentState = model.DocumentState,
        Notes = model.Notes,
        Status = model.Status,
        FileSizeBytes = model.FileSizeBytes,
        CreatedAt = model.CreatedAt
    };
}
