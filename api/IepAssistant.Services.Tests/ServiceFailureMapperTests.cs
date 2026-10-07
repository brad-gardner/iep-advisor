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

    // --- Multilingual plan (2026-10-06) phase 3 review fix: these specific sites' status changed when
    // they were localized — e.g. AnalysisRunService.CreateRunAsync's "Child not found." moved from an
    // explicit NotFound (404) to Validation (400) to match main's PRE-localization status, which the
    // English-substring fallback had produced incidentally. Pinned here at the mapper level (the exact
    // English message each site's _localizer[...] call resolves to, with the ErrorKind that site now
    // sets) so a future edit can't silently regress the status again.

    [Theory]
    [InlineData("This summary has already been sent.")]
    [InlineData("No draft summary exists yet. Generate one first.")]
    [InlineData("The meeting summary could not be drafted right now. Please try again.")]
    public void Validation_MapsTo400_MeetingSummaryStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public void Validation_MapsTo400_DraftQuestionUnavailableMessage()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation,
            "This question could not be answered right now. Please try again.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Theory]
    [InlineData("Child not found.")]
    [InlineData("Analysis limit reached for this child.")]
    [InlineData("You do not have access to this child.")]
    public void Validation_MapsTo400_AnalysisRunStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 5 review fixes P2-1/P2-2: two more status-parity sites,
    // pinned the same way as the phase 3 fixes above.

    [Fact]
    public void Validation_MapsTo400_CreateIepFromEtrNoTemplateMessage()
    {
        // EvaluationCaseService.CreateIepFromEtrAsync's "no document template" failure re-wraps
        // DocumentInstanceService.CreateAsync's Unprocessable (422) as Validation (400) — main's
        // pre-existing status for this route, which simply re-propagating the inner kind would have
        // silently changed to 422.
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation,
            "No document template is available for this document type yet. Ask an administrator to publish one.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public void NotFound_MapsTo404_MeetingBriefNoBriefYetMessage()
    {
        // MeetingBriefService.GetAsync's "no brief generated yet" failure is NotFound (404) — a
        // deliberate change from the pre-existing (buggy) 400, matching IMeetingBriefService.GetAsync's
        // own doc comment ("mapped to 404") so the web client's Generate-brief empty state can key off
        // a real 404.
        var result = ServiceResult.FailureResult(ServiceErrorKind.NotFound,
            "No brief has been generated for this meeting yet.");

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
