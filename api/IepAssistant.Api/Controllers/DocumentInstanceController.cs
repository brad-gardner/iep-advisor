using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Documents;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Educator authoring of document instances (State Document Template Engine, Phase 3). Declares only
/// <c>[Authorize]</c> — per-resource authorization (Collaborator+ on the student) is enforced inside
/// <see cref="IDocumentInstanceService"/>, mirroring <see cref="IepDraftController"/>. Enums serialize
/// as strings. Finalize / versioning / PDF are Phase 4 and not exposed here.
/// </summary>
[ApiController]
[Authorize]
public class DocumentInstanceController : ControllerBase
{
    private readonly IDocumentInstanceService _service;

    public DocumentInstanceController(IDocumentInstanceService service)
    {
        _service = service;
    }

    [HttpPost("api/educator/students/{studentId:int}/documents")]
    [ProducesResponseType(typeof(ApiResponse<DocumentInstanceDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(int studentId, [FromBody] CreateDocumentInstanceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _service.CreateAsync(studentId, request.DocumentTypeId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = DocumentInstanceMappers.MapDetail(result.Data!);
        return CreatedAtAction(nameof(Get), new { instanceId = dto.Id }, ApiResponse<DocumentInstanceDetailDto>.SuccessResponse(dto));
    }

    [HttpGet("api/educator/students/{studentId:int}/documents")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentInstanceSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(int studentId, CancellationToken ct)
    {
        var result = await _service.ListForStudentAsync(studentId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<IEnumerable<DocumentInstanceSummaryDto>>.SuccessResponse(
            result.Data!.Select(DocumentInstanceMappers.MapSummary)));
    }

    [HttpGet("api/documents/{instanceId:int}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentInstanceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int instanceId, CancellationToken ct)
    {
        var result = await _service.GetAsync(instanceId, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        return Ok(ApiResponse<DocumentInstanceDetailDto>.SuccessResponse(DocumentInstanceMappers.MapDetail(result.Data!)));
    }

    [HttpPut("api/documents/{instanceId:int}/values")]
    [ProducesResponseType(typeof(ApiResponse<DocumentInstanceValuesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveValues(int instanceId, [FromBody] SaveDocumentValuesRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
            return BadRequest(ApiResponse<object>.Error("The provided row version is not valid base64."));

        var result = await _service.SaveValuesAsync(instanceId, request.Values, rowVersion, User.GetUserId(), ct);
        if (!result.Success) return MapFailure(result.Message);

        // Lightweight response: normalized values + rotated token only (the immutable pinned tree
        // stays on the client — no full-tree re-query/re-ship per autosave tick).
        return Ok(ApiResponse<DocumentInstanceValuesDto>.SuccessResponse(DocumentInstanceMappers.MapValues(result.Data!)));
    }

    [HttpDelete("api/documents/{instanceId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int instanceId, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(instanceId, User.GetUserId(), ct);
        return result.Success ? Ok(ApiResponse<object>.SuccessResponse(null)) : MapFailure(result.Message);
    }

    private IActionResult MapFailure(string? message)
    {
        message ??= "Request failed";

        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Error(message));

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Error(message));

        // No resolvable template for the requested (state, document type) — a client-actionable 4xx.
        if (message.Contains("no document template", StringComparison.OrdinalIgnoreCase))
            return UnprocessableEntity(ApiResponse<object>.Error(message));

        // Stale optimistic-concurrency token.
        if (message.Contains("changed by someone else", StringComparison.OrdinalIgnoreCase))
            return Conflict(ApiResponse<object>.Error(message));

        return BadRequest(ApiResponse<object>.Error(message));
    }

    /// <summary>
    /// Decodes a base64 concurrency token; blank -> null (success). Tolerant of base64url and of a
    /// <c>+</c> that arrived as a space (matches TemplateAuthoringController's decoder). Returns false
    /// only on malformed input.
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
