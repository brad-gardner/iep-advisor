using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Admin authoring surface for the State Document Template Engine (Phase 2): build a template's
/// section/field structure on its Draft version, publish an immutable version, and fork a new Draft
/// from the latest Published version. All endpoints require the platform <c>Admin</c> role (matching
/// <see cref="AdminTemplatesController"/>); templates carry no student PII.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class TemplateAuthoringController : ControllerBase
{
    private readonly ITemplateAuthoringService _service;

    public TemplateAuthoringController(ITemplateAuthoringService service)
    {
        _service = service;
    }

    // ---------------------------------------------------------------- Version tree (form-schema preview)

    [HttpGet("document-template-versions/{versionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(int versionId, CancellationToken ct)
        => Respond(await _service.GetVersionAsync(versionId, ct));

    // ---------------------------------------------------------------- Sections

    [HttpPost("document-template-versions/{versionId:int}/sections")]
    public async Task<IActionResult> AddSection(int versionId, [FromBody] AddSectionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.AddSectionAsync(User.GetUserId(), versionId, request.Title, rowVersion, ct));
    }

    [HttpPut("document-template-sections/{sectionId:int}")]
    public async Task<IActionResult> UpdateSection(int sectionId, [FromBody] UpdateSectionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.UpdateSectionAsync(User.GetUserId(), sectionId, request.Title, rowVersion, ct));
    }

    [HttpDelete("document-template-sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteSection(int sectionId, [FromQuery] string? rowVersion, CancellationToken ct)
    {
        if (!TryDecodeRowVersion(rowVersion, out var token)) return InvalidRowVersion();

        return Respond(await _service.DeleteSectionAsync(User.GetUserId(), sectionId, token, ct));
    }

    [HttpPut("document-template-versions/{versionId:int}/sections/order")]
    public async Task<IActionResult> ReorderSections(int versionId, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.ReorderSectionsAsync(User.GetUserId(), versionId, request.OrderedIds, rowVersion, ct));
    }

    // ---------------------------------------------------------------- Fields

    [HttpPost("document-template-sections/{sectionId:int}/fields")]
    public async Task<IActionResult> AddField(int sectionId, [FromBody] AddFieldRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.AddFieldAsync(
            User.GetUserId(), sectionId, request.FieldType, request.Label, request.Required, request.ConfigJson, rowVersion, ct));
    }

    [HttpPut("document-template-fields/{fieldId:int}")]
    public async Task<IActionResult> UpdateField(int fieldId, [FromBody] UpdateFieldRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.UpdateFieldAsync(
            User.GetUserId(), fieldId, request.FieldType, request.Label, request.Required, request.ConfigJson, rowVersion, ct));
    }

    [HttpDelete("document-template-fields/{fieldId:int}")]
    public async Task<IActionResult> DeleteField(int fieldId, [FromQuery] string? rowVersion, CancellationToken ct)
    {
        if (!TryDecodeRowVersion(rowVersion, out var token)) return InvalidRowVersion();

        return Respond(await _service.DeleteFieldAsync(User.GetUserId(), fieldId, token, ct));
    }

    [HttpPut("document-template-sections/{sectionId:int}/fields/order")]
    public async Task<IActionResult> ReorderFields(int sectionId, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.ReorderFieldsAsync(User.GetUserId(), sectionId, request.OrderedIds, rowVersion, ct));
    }

    // ---------------------------------------------------------------- Lifecycle

    [HttpPost("document-templates/{templateId:int}/publish")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(int templateId, [FromBody] PublishRequest? request, CancellationToken ct)
    {
        if (!TryDecodeRowVersion(request?.RowVersion, out var rowVersion)) return InvalidRowVersion();

        return Respond(await _service.PublishAsync(User.GetUserId(), templateId, rowVersion, ct));
    }

    [HttpPost("document-templates/{templateId:int}/create-draft")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDraftFromPublished(int templateId, CancellationToken ct)
        => Respond(await _service.CreateDraftFromPublishedAsync(User.GetUserId(), templateId, ct));

    // ---------------------------------------------------------------- Helpers

    private IActionResult Respond(ServiceResult<TemplateVersionDetailModel> result)
    {
        if (result.Success)
            return Ok(ApiResponse<TemplateVersionDetailDto>.SuccessResponse(
                DocumentTemplateMappers.MapVersionDetail(result.Data!)));

        // Publish gathers multiple field-level errors.
        if (result.Errors.Count > 0)
            return BadRequest(ApiResponse<object>.Error(result.Errors));

        var message = result.Message ?? "Request failed";

        if (message.Contains("changed by someone else", StringComparison.OrdinalIgnoreCase))
            return Conflict(ApiResponse<object>.Error(message));

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }

    private IActionResult InvalidRowVersion()
        => BadRequest(ApiResponse<object>.Error("The provided row version is not valid base64."));

    /// <summary>
    /// Decodes a base64 concurrency token; blank -> null (success). Returns false only on malformed
    /// input. Tolerant of the two common query-string hazards: base64url (<c>-</c>/<c>_</c>) and a
    /// <c>+</c> that arrived as a space because it was not percent-encoded — both normalize back to
    /// standard base64 so the token round-trips whether it rides in the JSON body or the query string.
    /// </summary>
    private static bool TryDecodeRowVersion(string? token, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(token)) return true;

        var normalized = token.Replace(' ', '+').Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0) normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
