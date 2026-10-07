using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.EtrDocuments;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class EtrDocumentsController : ControllerBase
{
    private readonly IEtrDocumentService _etrDocumentService;
    private readonly IEtrProcessingService _etrProcessingService;
    private readonly IAccessService _accessService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly EtrProcessingQueue _processingQueue;
    private readonly IStringLocalizer<Messages> _localizer;

    public EtrDocumentsController(
        IEtrDocumentService etrDocumentService,
        IEtrProcessingService etrProcessingService,
        IAccessService accessService,
        ISubscriptionService subscriptionService,
        EtrProcessingQueue processingQueue,
        IStringLocalizer<Messages> localizer)
    {
        _etrDocumentService = etrDocumentService;
        _etrProcessingService = etrProcessingService;
        _accessService = accessService;
        _subscriptionService = subscriptionService;
        _processingQueue = processingQueue;
        _localizer = localizer;
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
            return NotFound(ApiResponse<object>.Error(_localizer["EtrDocumentsApi.NotFound"]));

        return Ok(ApiResponse<EtrDocumentDto>.SuccessResponse(MapToDto(document)));
    }

    [HttpPost("api/children/{childId}/etrs")]
    [ProducesResponseType(typeof(ApiResponse<EtrDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateEtrRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["Api.InvalidRequest"]));

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
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.CreationFailed"].Value));

        var dto = MapToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<EtrDocumentDto>.SuccessResponse(dto, _localizer["EtrDocumentsApi.Created"]));
    }

    [HttpPost("api/etrs/{id}/upload")]
    [ProducesResponseType(typeof(ApiResponse<EtrDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> Upload(int id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Error(_localizer["DocumentsApi.NoFileProvided"]));

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Error(_localizer["DocumentsApi.OnlyPdfSupported"]));

        // Validate PDF magic bytes
        using var stream = file.OpenReadStream();
        var header = new byte[5];
        var bytesRead = await stream.ReadAsync(header, 0, 5, cancellationToken);
        if (bytesRead < 5 || System.Text.Encoding.ASCII.GetString(header) != "%PDF-")
            return BadRequest(ApiResponse<object>.Error(_localizer["DocumentsApi.InvalidPdf"]));
        stream.Position = 0;

        var sanitizedFileName = Path.GetFileName(file.FileName);
        var userId = User.GetUserId();

        // Ownership + role check before subscription check (mirror IEP: service also checks, but do explicit role gate here)
        var document = await _etrDocumentService.GetByIdAsync(id, userId, cancellationToken);
        if (document == null)
            return NotFound(ApiResponse<object>.Error(_localizer["EtrDocumentsApi.NotFound"]));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error(_localizer["DocumentsApi.InsufficientPermissions"]));

        if (!await _subscriptionService.HasActiveSubscriptionAsync(userId, cancellationToken))
            return StatusCode(402, ApiResponse<object>.Error(_localizer["EtrDocumentsApi.SubscriptionRequired"]));

        var result = await _etrDocumentService.AttachFileAsync(id, userId, sanitizedFileName, stream, file.Length, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.UploadFailed"].Value));

        var dto = MapToDto(result.Data!);

        await _processingQueue.EnqueueAsync(dto.Id, cancellationToken);

        return Ok(ApiResponse<EtrDocumentDto>.SuccessResponse(dto, _localizer["DocumentsApi.FileAttached"]));
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

        // Multilingual plan Phase 3: status came from matching translated text ("not found"); the service
        // now sets ErrorKind.NotFound/Validation explicitly, so use the shared kind-based mapper instead.
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["DocumentsApi.UpdateFailed"]);

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["EtrDocumentsApi.MetadataUpdated"]));
    }

    [HttpGet("api/etrs/{id}/download")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var url = await _etrDocumentService.GetDownloadUrlAsync(id, userId, cancellationToken);

        if (url == null)
            return NotFound(ApiResponse<object>.Error(_localizer["DocumentsApi.DocumentNotFound"]));

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
            return NotFound(ApiResponse<object>.Error(_localizer["DocumentsApi.DocumentNotFound"]));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error(_localizer["DocumentsApi.InsufficientPermissions"]));

        if (document.Status == "processing")
            return Conflict(ApiResponse<object>.Error(_localizer["EtrDocumentsApi.AlreadyProcessing"]));

        await _processingQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, _localizer["DocumentsApi.QueuedForProcessing"]));
    }

    [HttpDelete("api/etrs/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _etrDocumentService.DeleteAsync(id, userId, cancellationToken);

        if (!result.Success)
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.DeleteFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["DocumentsApi.DocumentDeleted"]));
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
