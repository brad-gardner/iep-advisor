using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Read-only document-type lookup for authenticated non-admin surfaces (e.g. the educator
/// "create document" picker). Authoring of templates/types stays admin-only on
/// <see cref="AdminTemplatesController"/>; document types carry no student PII.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class DocumentTypesController : ControllerBase
{
    private readonly IDocumentTemplateService _service;

    public DocumentTypesController(IDocumentTemplateService service)
    {
        _service = service;
    }

    [HttpGet("document-types")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocumentTypes(CancellationToken ct)
    {
        var result = await _service.ListDocumentTypesAsync(ct);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.Error(result.Message ?? "Failed to load document types."));

        return Ok(ApiResponse<IEnumerable<DocumentTypeDto>>.SuccessResponse(
            result.Data!.Select(DocumentTemplateMappers.MapDocumentType)));
    }
}
