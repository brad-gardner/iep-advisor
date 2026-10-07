using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Extensions;

/// <summary>
/// Multilingual plan (2026-10-06) phase 2 review fix P2-A: a single place controllers map a failed
/// <see cref="ServiceResult"/>/<see cref="ServiceResult{T}"/> to an HTTP status. Switches on
/// <see cref="ServiceResult.ErrorKind"/> first; only when that is <see cref="ServiceErrorKind.None"/>
/// (a service not yet converted to set it) does it fall back to the ORIGINAL English-substring
/// heuristic ("permission" -&gt; 403, "not found" -&gt; 404, else 400) that every <c>MapFailure</c> in this
/// codebase used before this fix. That fallback is deliberately English-only — matching a Spanish
/// word here would make status depend on translated text again, the exact bug this fix removes.
/// </summary>
public static class ServiceFailureMapperExtensions
{
    /// <summary>
    /// Maps <paramref name="result"/> (must have <c>Success == false</c>) to a <see cref="ProblemDetails"/>-
    /// style <see cref="ApiResponse{T}"/> envelope with the matching status code. <paramref name="fallbackMessage"/>
    /// is used only when <paramref name="result"/> has no message (callers that already localize their
    /// generic "request failed" text should pass it here, e.g. <c>_localizer["Api.RequestFailed"]</c>).
    /// </summary>
    public static IActionResult MapServiceFailure(this ControllerBase controller, ServiceResult result, string? fallbackMessage = null)
    {
        var message = result.Message ?? fallbackMessage ?? "Request failed";

        switch (result.ErrorKind)
        {
            case ServiceErrorKind.Forbidden:
                return controller.StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(message));
            case ServiceErrorKind.NotFound:
                return controller.NotFound(ApiResponse<object>.Error(message));
            case ServiceErrorKind.Conflict:
                return controller.Conflict(ApiResponse<object>.Error(message));
            case ServiceErrorKind.Unavailable:
                return controller.StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Error(message));
            case ServiceErrorKind.PaymentRequired:
                return controller.StatusCode(StatusCodes.Status402PaymentRequired, ApiResponse<object>.Error(message));
            case ServiceErrorKind.Unprocessable:
                return controller.UnprocessableEntity(ApiResponse<object>.Error(message));
            case ServiceErrorKind.Validation:
                return controller.BadRequest(ApiResponse<object>.Error(message));
            case ServiceErrorKind.None:
            default:
                if (message.Contains("permission", StringComparison.OrdinalIgnoreCase))
                    return controller.StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(message));
                if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return controller.NotFound(ApiResponse<object>.Error(message));
                return controller.BadRequest(ApiResponse<object>.Error(message));
        }
    }
}
