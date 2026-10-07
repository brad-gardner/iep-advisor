using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 5 coverage for <see cref="ParentContributionService"/>: the
/// same "Child profile not found." text carries a different <see cref="ServiceErrorKind"/> at each call
/// site (NotFound for the read path's controller route, which always 404s; Validation for the create
/// path's route, which always 400s), matching <c>ParentContributionsController</c>'s pre-existing
/// per-endpoint status choices (see the class doc comment on <see cref="ParentContributionService"/>).
/// </summary>
public sealed class ParentContributionServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private ParentContributionService CreateService(Domain.Data.ApplicationDbContext ctx)
        => new(ctx, new AccessService(ctx), new OrgAccessService(ctx), new CapturingAuditLogger(), TestSupport.TestLocalizers.Messages());

    private sealed class TestController : ControllerBase
    {
    }

    [Fact]
    public async Task GetForChildAsync_UnknownChild_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        var userId = _db.SeedUser("parentes@example.com");

        using var _lang = CultureScope.For("es");
        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForChildAsync(-1, userId);

        Assert.False(result.Success);
        Assert.Equal("Perfil del hijo no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task CreateAsync_UnknownChild_UnderSpanishCulture_MessageIsSpanish_AndMapsTo400ViaErrorKind()
    {
        var userId = _db.SeedUser("parentcreatees@example.com");

        using var _lang = CultureScope.For("es");
        using var ctx = _db.Context();
        var result = await CreateService(ctx).CreateAsync(-1, userId, new SaveParentContributionModel
        {
            Kind = ParentContributionKind.WorksAtHome,
            Text = "Practices reading every night."
        });

        Assert.False(result.Success);
        Assert.Equal("Perfil del hijo no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public async Task GetSharedForSchoolStudentAsync_Stranger_UnderSpanishCulture_MessageIsSpanish_AndMapsTo403ViaErrorKind()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId);
        var (strangerUserId, _) = _db.Staff("strangeres@example.com", districtId, _db.School(districtId, "School B"), Models.OrgRoleIds.Teacher);

        using var _lang = CultureScope.For("es");
        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetSharedForSchoolStudentAsync(strangerUserId, studentId);

        Assert.False(result.Success);
        Assert.Equal("No tiene permiso para ver a este estudiante.", result.Message);
        Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    public void Dispose() => _db.Dispose();
}
