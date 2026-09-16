using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Evaluations;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

/// <summary>
/// Evaluation case lifecycle: referral → consent → clock → determination → ETR handoff (plan 7, decision
/// 1). Declares only <c>[Authorize]</c> — per-resource authorization (Viewer+ to read, Collaborator+ to
/// write) is enforced inside <see cref="IEvaluationCaseService"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/educator/students/{studentId:int}/evaluation")]
public class EvaluationCaseController : ControllerBase
{
    private const long MaxConsentFileBytes = 10 * 1024 * 1024;

    private readonly IEvaluationCaseService _cases;

    public EvaluationCaseController(IEvaluationCaseService cases)
    {
        _cases = cases;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int studentId, CancellationToken ct)
    {
        var result = await _cases.GetForStudentAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto?>.SuccessResponse(result.Data == null ? null : EvaluationMappers.MapCase(result.Data)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int studentId, [FromBody] CreateEvaluationCaseRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _cases.CreateAsync(User.GetUserId(), studentId, new CreateEvaluationCaseModel
        {
            Kind = request.Kind,
            ReferralDate = request.ReferralDate,
            ReferralSource = request.ReferralSource
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = EvaluationMappers.MapCase(result.Data!);
        return CreatedAtAction(nameof(Get), new { studentId }, ApiResponse<EvaluationCaseDto>.SuccessResponse(dto));
    }

    [HttpPost("consent/request")]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RequestConsent(int studentId, [FromBody] RequestConsentRequest? request, CancellationToken ct)
    {
        var result = await _cases.RequestConsentAsync(User.GetUserId(), studentId, request?.RequestedAt, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto>.SuccessResponse(EvaluationMappers.MapCase(result.Data!)));
    }

    /// <summary>Accepts EITHER a multipart form (fields <c>receivedAt</c> + optional <c>file</c>) OR a
    /// plain JSON body <c>{ receivedAt }</c> (contract: "multipart or JSON").</summary>
    [HttpPost("consent/receive")]
    [RequestSizeLimit(MaxConsentFileBytes + 1024)]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReceiveConsent(int studentId, CancellationToken ct)
    {
        DateTime? receivedAt = null;
        Stream? fileStream = null;
        string? fileName = null;
        string? contentType = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            if (form.TryGetValue("receivedAt", out var raw) && DateTime.TryParse(raw, out var parsed))
                receivedAt = parsed;

            var file = form.Files.GetFile("file");
            if (file != null && file.Length > 0)
            {
                fileStream = file.OpenReadStream();
                fileName = file.FileName;
                contentType = file.ContentType;
            }
        }
        else if (Request.ContentLength > 0)
        {
            try
            {
                var json = await JsonSerializer.DeserializeAsync<ReceiveConsentJsonRequest>(
                    Request.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
                receivedAt = json?.ReceivedAt;
            }
            catch (JsonException)
            {
                return BadRequest(ApiResponse<object>.Error("Invalid request body."));
            }
        }

        if (receivedAt == null)
            return BadRequest(ApiResponse<object>.Error("receivedAt is required."));

        var result = await _cases.ReceiveConsentAsync(User.GetUserId(), studentId, new ReceiveConsentModel
        {
            ReceivedAt = receivedAt.Value,
            FileStream = fileStream,
            FileName = fileName,
            ContentType = contentType
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto>.SuccessResponse(EvaluationMappers.MapCase(result.Data!)));
    }

    [HttpGet("consent/download")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConsentDownloadUrl(int studentId, CancellationToken ct)
    {
        var result = await _cases.GetConsentDownloadUrlAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<string>.SuccessResponse(result.Data));
    }

    [HttpPut("due-date")]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OverrideDueDate(int studentId, [FromBody] OverrideDueDateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _cases.OverrideDueDateAsync(User.GetUserId(), studentId, new OverrideDueDateModel
        {
            DeterminationDueDate = request.DeterminationDueDate,
            Reason = request.Reason
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto>.SuccessResponse(EvaluationMappers.MapCase(result.Data!)));
    }

    [HttpPost("assignments")]
    [ProducesResponseType(typeof(ApiResponse<EvaluatorAssignmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddAssignment(int studentId, [FromBody] CreateEvaluatorAssignmentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _cases.AddAssignmentAsync(User.GetUserId(), studentId, new CreateEvaluatorAssignmentModel
        {
            UserId = request.UserId,
            Domain = request.Domain,
            DueDate = request.DueDate
        }, ct);
        if (!result.Success) return MapFailure(result.Message);

        var dto = EvaluationMappers.MapAssignment(result.Data!);
        return Created($"/api/educator/students/{studentId}/evaluation/assignments/{dto.Id}", ApiResponse<EvaluatorAssignmentDto>.SuccessResponse(dto));
    }

    [HttpPut("assignments/{assignmentId:int}")]
    [ProducesResponseType(typeof(ApiResponse<EvaluatorAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAssignment(int studentId, int assignmentId, [FromBody] UpdateEvaluatorAssignmentRequest request, CancellationToken ct)
    {
        var result = await _cases.UpdateAssignmentAsync(User.GetUserId(), assignmentId, new UpdateEvaluatorAssignmentModel
        {
            SubmittedAt = request.SubmittedAt,
            Notes = request.Notes,
            DueDate = request.DueDate
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluatorAssignmentDto>.SuccessResponse(EvaluationMappers.MapAssignment(result.Data!)));
    }

    [HttpDelete("assignments/{assignmentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAssignment(int studentId, int assignmentId, CancellationToken ct)
    {
        var result = await _cases.RemoveAssignmentAsync(User.GetUserId(), assignmentId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return NoContent();
    }

    [HttpPost("determine")]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Determine(int studentId, [FromBody] DetermineEvaluationRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Error("Invalid request"));

        var result = await _cases.DetermineAsync(User.GetUserId(), studentId, new DetermineEvaluationModel
        {
            Outcome = request.Outcome,
            DeterminationDate = request.DeterminationDate,
            Rationale = request.Rationale,
            EtrAuthoredVersionId = request.EtrAuthoredVersionId
        }, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto>.SuccessResponse(EvaluationMappers.MapCase(result.Data!)));
    }

    [HttpPost("close")]
    [ProducesResponseType(typeof(ApiResponse<EvaluationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Close(int studentId, CancellationToken ct)
    {
        var result = await _cases.CloseAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return Ok(ApiResponse<EvaluationCaseDto>.SuccessResponse(EvaluationMappers.MapCase(result.Data!)));
    }

    [HttpPost("create-iep")]
    [ProducesResponseType(typeof(ApiResponse<CreateIepResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateIep(int studentId, CancellationToken ct)
    {
        var result = await _cases.CreateIepFromEtrAsync(User.GetUserId(), studentId, ct);
        if (!result.Success) return MapFailure(result.Message);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CreateIepResponseDto>.SuccessResponse(new CreateIepResponseDto { InstanceId = result.Data }));
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
