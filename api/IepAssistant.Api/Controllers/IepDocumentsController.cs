using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.IepDocuments;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class IepDocumentsController : ControllerBase
{
    private readonly IIepDocumentService _iepDocumentService;
    private readonly IIepProcessingService _iepProcessingService;
    private readonly IAccessService _accessService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IepProcessingQueue _processingQueue;
    private readonly IStringLocalizer<Messages> _localizer;

    public IepDocumentsController(
        IIepDocumentService iepDocumentService,
        IIepProcessingService iepProcessingService,
        IAccessService accessService,
        ISubscriptionService subscriptionService,
        IepProcessingQueue processingQueue,
        IStringLocalizer<Messages> localizer)
    {
        _iepDocumentService = iepDocumentService;
        _iepProcessingService = iepProcessingService;
        _accessService = accessService;
        _subscriptionService = subscriptionService;
        _processingQueue = processingQueue;
        _localizer = localizer;
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
            return NotFound(ApiResponse<object>.Error(_localizer["IepDocumentsApi.NotFound"]));

        return Ok(ApiResponse<IepDocumentDto>.SuccessResponse(MapToDto(document)));
    }

    [HttpPost("api/children/{childId}/ieps")]
    [ProducesResponseType(typeof(ApiResponse<IepDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(int childId, [FromBody] CreateIepRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Error(_localizer["Api.InvalidRequest"]));

        var userId = User.GetUserId();
        var model = new CreateIepDocumentModel
        {
            IepDate = request.IepDate!.Value,
            MeetingType = request.MeetingType,
            Attendees = request.Attendees,
            Notes = request.Notes
        };

        var result = await _iepDocumentService.CreateAsync(childId, userId, model, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.CreationFailed"].Value));

        var dto = MapToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<IepDocumentDto>.SuccessResponse(dto, _localizer["IepDocumentsApi.Created"]));
    }

    [HttpPost("api/ieps/{id}/upload")]
    [ProducesResponseType(typeof(ApiResponse<IepDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> AttachFile(int id, IFormFile file, CancellationToken cancellationToken)
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

        // Check subscription before processing
        if (!await _subscriptionService.HasActiveSubscriptionAsync(userId, cancellationToken))
            return StatusCode(402, ApiResponse<object>.Error(_localizer["IepDocumentsApi.SubscriptionRequired"]));

        var result = await _iepDocumentService.AttachFileAsync(id, userId, sanitizedFileName, stream, file.Length, cancellationToken);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.UploadFailed"].Value));

        var dto = MapToDto(result.Data!);

        // Enqueue background processing
        await _processingQueue.EnqueueAsync(dto.Id, cancellationToken);

        return Ok(ApiResponse<IepDocumentDto>.SuccessResponse(dto, _localizer["DocumentsApi.FileAttached"]));
    }

    [HttpPut("api/ieps/{id}/metadata")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(int id, [FromBody] UpdateIepMetadataRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var model = new UpdateIepMetadataModel
        {
            IepDate = request.IepDate,
            MeetingType = request.MeetingType,
            Attendees = request.Attendees,
            Notes = request.Notes
        };

        var result = await _iepDocumentService.UpdateMetadataAsync(id, userId, model, cancellationToken);

        // Multilingual plan Phase 3: status came from matching translated text ("not found"), which
        // breaks once UpdateMetadataAsync's failure messages are localized (see ServiceErrorKind docs).
        // The service now sets ErrorKind.NotFound/Validation explicitly; use the shared kind-based mapper.
        if (!result.Success)
            return this.MapServiceFailure(result, _localizer["DocumentsApi.UpdateFailed"]);

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["IepDocumentsApi.MetadataUpdated"]));
    }

    [HttpGet("api/ieps/{id}/download")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var url = await _iepDocumentService.GetDownloadUrlAsync(id, userId, cancellationToken);

        if (url == null)
            return NotFound(ApiResponse<object>.Error(_localizer["DocumentsApi.DocumentNotFound"]));

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
            return NotFound(ApiResponse<object>.Error(result.Message ?? _localizer["DocumentsApi.DeleteFailed"].Value));

        return Ok(ApiResponse<object>.SuccessResponse(null, _localizer["DocumentsApi.DocumentDeleted"]));
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
            return NotFound(ApiResponse<object>.Error(_localizer["DocumentsApi.DocumentNotFound"]));

        if (!await _accessService.HasMinimumRoleAsync(document.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return StatusCode(403, ApiResponse<object>.Error(_localizer["DocumentsApi.InsufficientPermissions"]));

        await _processingQueue.EnqueueAsync(id, cancellationToken);
        return Accepted(ApiResponse<object>.SuccessResponse(null, _localizer["DocumentsApi.QueuedForProcessing"]));
    }

    private static IepDocumentDto MapToDto(IepDocumentModel model) => new()
    {
        Id = model.Id,
        ChildProfileId = model.ChildProfileId,
        FileName = model.FileName,
        UploadDate = model.UploadDate,
        IepDate = model.IepDate,
        MeetingType = model.MeetingType,
        Attendees = model.Attendees,
        Notes = model.Notes,
        Status = model.Status,
        FileSizeBytes = model.FileSizeBytes,
        CreatedAt = model.CreatedAt
    };
}
