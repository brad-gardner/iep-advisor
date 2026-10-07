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

    // --- Multilingual plan (2026-10-06) phase 6: DistrictController switched from its own private
    // MapFailure<T> (an inline copy of the same English-substring heuristic) to the shared
    // MapServiceFailure. Every DistrictService failure site below sets the ErrorKind that reproduces
    // the PRE-existing heuristic's outcome for that exact English message, so the route's status is
    // unchanged even though the message can now be Spanish.

    [Theory]
    [InlineData("Educator profile not found.")]
    [InlineData("District not found.")]
    [InlineData("School not found.")]
    public void NotFound_MapsTo404_DistrictStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.NotFound, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Theory]
    [InlineData("You do not have permission to change district settings.")]
    [InlineData("You do not have permission to view the district dashboard.")]
    [InlineData("You do not have permission to create schools.")]
    [InlineData("You do not have permission to edit schools.")]
    [InlineData("You do not have permission to deactivate schools.")]
    [InlineData("You do not have permission to view this data.")]
    [InlineData("You do not have permission to view this school's data.")]
    public void Forbidden_MapsTo403_DistrictStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Forbidden, message);

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("School name is required.")]
    [InlineData("School name must be 200 characters or fewer.")]
    [InlineData("State code must be 2 characters.")]
    [InlineData("This school cannot be deactivated while it has 3 active student(s). Move or remove them first.")]
    [InlineData("This school cannot be deactivated while it has 2 active staff member(s). Reassign or deactivate them first.")]
    [InlineData("The requested date range is out of bounds.")]
    public void Validation_MapsTo400_DistrictStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6: StaffController switched from its own private
    // MapFailure (an inline copy of the same English-substring heuristic) to the shared
    // MapServiceFailure. Every StaffInviteService failure site below sets the ErrorKind that reproduces
    // the PRE-existing heuristic's outcome for that exact English message, so the route's status is
    // unchanged even though the message can now be Spanish.

    [Theory]
    [InlineData("Staff profile not found.")]
    [InlineData("Invite not found.")]
    [InlineData("School not found.")]
    [InlineData("Staff member not found.")]
    public void NotFound_MapsTo404_StaffInviteStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.NotFound, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Theory]
    [InlineData("You do not have permission to invite a District Admin.")]
    [InlineData("You do not have permission to invite staff to another school.")]
    [InlineData("You do not have permission to invite staff.")]
    [InlineData("You do not have permission to view the staff list.")]
    [InlineData("You do not have permission to manage District Admin invites.")]
    [InlineData("You do not have permission to manage invites for another school.")]
    [InlineData("You do not have permission to manage staff invites.")]
    [InlineData("You do not have permission to manage a District Admin.")]
    [InlineData("You do not have permission to manage staff at another school.")]
    [InlineData("You do not have permission to manage staff.")]
    public void Forbidden_MapsTo403_StaffInviteStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Forbidden, message);

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("Email is required.")]
    [InlineData("Email must be 256 characters or fewer.")]
    [InlineData("Invalid org role.")]
    [InlineData("A District Admin invite must not specify a school.")]
    [InlineData("A school is required for School Admin and Teacher invites.")]
    [InlineData("Your account is not assigned to a school.")]
    [InlineData("That email already has an account. Staff must be invited with an email that isn't already registered — please use your work email.")]
    [InlineData("That email has already been invited.")]
    [InlineData("Invite is no longer pending.")]
    [InlineData("You cannot deactivate the last active District Admin of the district.")]
    public void Validation_MapsTo400_StaffInviteStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6: DistrictImportsController switched from its own private
    // MapFailure (an inline copy of the same English-substring heuristic) to the shared
    // MapServiceFailure. Every RosterImportService failure site below sets the ErrorKind that reproduces
    // the PRE-existing heuristic's outcome for that exact English message, so the route's status is
    // unchanged even though the message can now be Spanish.

    [Theory]
    [InlineData("Educator profile not found.")]
    [InlineData("Import not found.")]
    public void NotFound_MapsTo404_RosterImportStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.NotFound, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public void Forbidden_MapsTo403_RosterImportNoPermissionToImport()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Forbidden, "You do not have permission to import students.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("Your account is not assigned to a school.")]
    [InlineData("This import is not a student roster.")]
    [InlineData("This import has already been committed.")]
    [InlineData("Fix the errors or choose to import valid rows only.")]
    public void Validation_MapsTo400_RosterImportStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6: ExportsController switched from its own private
    // MapFailure (an inline copy of the same English-substring heuristic) to the shared
    // MapServiceFailure. Every ExportService failure site below sets the ErrorKind that reproduces the
    // PRE-existing heuristic's outcome for that exact English message, so the route's status is
    // unchanged even though the message can now be Spanish.

    [Theory]
    [InlineData("Export not found.")]
    [InlineData("Student not found.")]
    public void NotFound_MapsTo404_ExportStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.NotFound, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public void Forbidden_MapsTo403_ExportNoPermissionToRequest()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Forbidden, "You do not have permission to request this export.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Validation_MapsTo400_ExportNotReadyMessage()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, "This export is not ready for download yet.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6: AdminTemplatesController switched from its own private
    // MapFailure (an inline copy of the English-substring heuristic) to the shared MapServiceFailure.
    // None of DocumentTemplateService's PRE-existing English messages contained "permission" or
    // "not found", so every one of its failures was ALREADY 400 under the old heuristic — pinned here as
    // Validation so that never silently changes.

    [Theory]
    [InlineData("Template name is required.")]
    [InlineData("State code must be a 2-letter code (e.g. OH), or left blank for the default template.")]
    [InlineData("The selected document type does not exist.")]
    [InlineData("The selected document type is not active.")]
    [InlineData("A template for IEP in OH already exists.")]
    public void Validation_MapsTo400_DocumentTemplateStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6: AuditLogController switched from its own private
    // MapFailure (an inline copy of the same English-substring heuristic) to the shared
    // MapServiceFailure. Every AuditLogQueryService failure site below sets the ErrorKind that
    // reproduces the PRE-existing heuristic's outcome for that exact English message, so the route's
    // status is unchanged even though the message can now be Spanish.

    [Fact]
    public void Forbidden_MapsTo403_AuditLogNoPermission()
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Forbidden, "You do not have permission to view the activity log.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("Page size must be greater than zero.")]
    [InlineData("Cursor must not be negative.")]
    [InlineData("Invalid action 'Bogus'.")]
    public void Validation_MapsTo400_AuditLogStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(ServiceErrorKind.Validation, message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    // --- Multilingual plan (2026-10-06) phase 6 project sweep: IepDraftController and
    // ParentPrepQuestionsController switched from their own private MapFailure (each an inline copy of
    // the same English-substring heuristic) to the shared MapServiceFailure. Neither service sets
    // ErrorKind yet (still ServiceErrorKind.None), so these pin the None-fallback's outcome for each
    // service's exact PRE-existing English messages — unchanged behavior, verified at the mapper level.

    [Theory]
    [InlineData("IEP draft not found.")]
    [InlineData("Section not found.")]
    [InlineData("Goal not found.")]
    [InlineData("Service line not found.")]
    [InlineData("Accommodation not found.")]
    [InlineData("Transition item not found.")]
    public void NotFound_FallsBackToEnglishHeuristic_IepDraftStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public void Forbidden_FallsBackToEnglishHeuristic_IepDraftNoPermissionMessage()
    {
        var result = ServiceResult.FailureResult("You do not have permission to access this IEP draft.");

        var action = new TestController().MapServiceFailure(result);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Validation_FallsBackToEnglishHeuristic_IepDraftBeingFinalizedMessage()
    {
        var result = ServiceResult.FailureResult("The draft is being finalized; try again in a moment.");

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Theory]
    [InlineData("Child profile not found.")]
    [InlineData("Prep question not found.")]
    public void NotFound_FallsBackToEnglishHeuristic_ParentPrepQuestionStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Theory]
    [InlineData("Source must be 'parent' or 'advocate'.")]
    [InlineData("Question text is required.")]
    [InlineData("Question must be 500 characters or fewer.")]
    [InlineData("Provide text or isChecked.")]
    [InlineData("ids is required.")]
    [InlineData("ids must not repeat.")]
    [InlineData("Every id must be one of this child's prep questions.")]
    public void Validation_FallsBackToEnglishHeuristic_ParentPrepQuestionStatusParitySites(string message)
    {
        var result = ServiceResult.FailureResult(message);

        var action = new TestController().MapServiceFailure(result);

        Assert.IsType<BadRequestObjectResult>(action);
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
