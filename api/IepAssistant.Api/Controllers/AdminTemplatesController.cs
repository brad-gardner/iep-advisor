using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Admin surface for the State Document Template Engine (Phase 1). All endpoints require the platform
/// <c>Admin</c> role (matching <see cref="AdminController"/>); templates carry no student PII.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminTemplatesController : ControllerBase
{
    private readonly IDocumentTemplateService _service;

    public AdminTemplatesController(IDocumentTemplateService service)
    {
        _service = service;
    }

    [HttpGet("document-types")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocumentTypes(CancellationToken ct)
    {
        var result = await _service.ListDocumentTypesAsync(ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<DocumentTypeDto>>.SuccessResponse(
            result.Data!.Select(DocumentTemplateMappers.MapDocumentType)));
    }

    [HttpGet("document-templates")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var result = await _service.ListTemplatesAsync(ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<DocumentTemplateDto>>.SuccessResponse(
            result.Data!.Select(DocumentTemplateMappers.MapTemplate)));
    }

    [HttpPost("document-templates")]
    [ProducesResponseType(typeof(ApiResponse<DocumentTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateDocumentTemplateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.CreateTemplateAsync(
            User.GetUserId(), request.StateCode, request.DocumentTypeId, request.Name, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = DocumentTemplateMappers.MapTemplate(result.Data!);
        return CreatedAtAction(nameof(ListTemplates), null, ApiResponse<DocumentTemplateDto>.SuccessResponse(dto));
    }

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
