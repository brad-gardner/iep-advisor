using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.Extensions;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 2 review fix P2-A: HTTP status must not depend on localized
/// message text. Covers <see cref="ServiceFailureMapperExtensions.MapServiceFailure"/> directly —
/// each <see cref="ServiceErrorKind"/> maps to its status regardless of message text/language, and the
/// <see cref="ServiceErrorKind.None"/> fallback (for services not yet converted) matches only the
/// ORIGINAL English substrings, never a Spanish one, so status can never flip based on translated text.
/// </summary>
public sealed class ServiceFailureMapperTests
{
    private sealed class TestController : ControllerBase
    {
    }

    [Fact]
    public void Forbidden_MapsTo403_RegardlessOfMessageLanguage()
    {
        var result = ServiceResult.Forbidden("No tiene permiso para invitar a un padre, madre o tutor para este estudiante.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void NotFound_MapsTo404_RegardlessOfMessageLanguage()
    {
        var result = ServiceResult.NotFound("Enlace no encontrado.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public void Conflict_MapsTo409()
    {
        var result = ServiceResult.Conflict("Ya existe una invitación pendiente.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<ConflictObjectResult>(action);
    }

    [Fact]
    public void Validation_MapsTo400_EvenWhenSpanishMessageContainsPermisoWord()
    {
        // A 400-kind failure whose Spanish TEXT happens to contain "permiso" must still be 400 — the
        // whole point of P2-A is that status comes from ErrorKind, never from matching message text.
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation,
            "No tiene permiso de estacionamiento válido para esta zona.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public void Validation_MapsTo400_EvenWhenSpanishMessageContainsNoEncontradStem()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation,
            "El valor no encontrado en la lista no es válido.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public void None_FallsBackToEnglishHeuristic_AndNeverMatchesSpanishStems()
    {
        // ErrorKind defaults to None for a service not yet converted. The fallback heuristic is
        // deliberately English-only: a Spanish "permiso"/"no encontrad" message must fall through to
        // 400, not be misrouted to 403/404.
        var result = ServiceResult.FailureResult("No tiene permiso para hacer esto.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public void None_FallsBackToEnglishHeuristic_PermissionWordStillMaps403()
    {
        var result = ServiceResult.FailureResult("You do not have permission to do this.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void None_FallsBackToEnglishHeuristic_NotFoundWordStillMaps404()
    {
        var result = ServiceResult.FailureResult("That record was not found.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public void NullMessage_UsesFallbackMessage()
    {
        var result = new ServiceResult { Success = false, Message = null };

        var action = new TestController().MapServiceFailure(result, "Request failed");

        var badRequest = Assert.IsType<BadRequestObjectResult>(action);
        var body = Assert.IsType<IepAssistant.Api.DTOs.Common.ApiResponse<object>>(badRequest.Value);
        Assert.Equal("Request failed", body.Message);
    }
}
