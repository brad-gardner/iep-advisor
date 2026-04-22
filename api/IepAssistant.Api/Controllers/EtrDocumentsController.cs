using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.EtrDocuments;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Authorize]
public class EtrDocumentsController : ControllerBase
{
    private readonly IEtrDocumentService _etrDocumentService;

    public EtrDocumentsController(IEtrDocumentService etrDocumentService)
    {
        _etrDocumentService = etrDocumentService;
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
