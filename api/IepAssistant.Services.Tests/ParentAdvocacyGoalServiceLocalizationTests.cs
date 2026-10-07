using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: <see cref="ParentAdvocacyGoalService"/>'s
/// <see cref="ServiceResult"/> failure messages render in the active UI culture, and status comes from
/// <see cref="ServiceResult.ErrorKind"/> — never from matching (possibly Spanish) message text — via
/// <see cref="IepAssistant.Api.Extensions.ServiceFailureMapperExtensions.MapServiceFailure"/>.
/// </summary>
public sealed class ParentAdvocacyGoalServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ParentAdvocacyGoalServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static ParentAdvocacyGoalService CreateService(ApplicationDbContext ctx) => new(
        new ParentAdvocacyGoalRepository(ctx),
        new ChildProfileRepository(ctx),
        new AccessService(ctx),
        ctx,
        TestSupport.TestLocalizers.Messages());

    private int SeedChildWithNoAccessForUser(out int strangerUserId)
    {
        using var ctx = CreateContext();
        var owner = new User { Email = "advgoal-owner@example.com", PasswordHash = "x", FirstName = "Owen", LastName = "Owner", Role = UserRole.Parent };
        var stranger = new User { Email = "advgoal-stranger@example.com", PasswordHash = "x", FirstName = "Sam", LastName = "Stranger", Role = UserRole.Parent };
        ctx.Users.AddRange(owner, stranger);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = owner.Id, FirstName = "Kid", IsActive = true };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        strangerUserId = stranger.Id;
        return child.Id;
    }

    [Fact]
    public async Task Create_ByStranger_UnderEnglishCulture_MessageIsEnglish()
    {
        var childId = SeedChildWithNoAccessForUser(out var strangerId);

        using var _lang = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(childId, strangerId, new CreateAdvocacyGoalModel { GoalText = "Reading support" });

        Assert.False(result.Success);
        Assert.Equal("Child profile not found.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Create_ByStranger_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        var childId = SeedChildWithNoAccessForUser(out var strangerId);

        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(childId, strangerId, new CreateAdvocacyGoalModel { GoalText = "Apoyo de lectura" });

        Assert.False(result.Success);
        Assert.Equal("Perfil del hijo no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task Create_WithInvalidCategory_UnderSpanishCulture_MessageIsSpanish_AndMapsTo400ViaErrorKind()
    {
        using var ctxSeed = CreateContext();
        var owner = new User { Email = "advgoal-owner2@example.com", PasswordHash = "x", FirstName = "Owen", LastName = "Owner", Role = UserRole.Parent };
        ctxSeed.Users.Add(owner);
        ctxSeed.SaveChanges();
        var child = new ChildProfile { UserId = owner.Id, FirstName = "Kid", IsActive = true };
        ctxSeed.ChildProfiles.Add(child);
        ctxSeed.SaveChanges();
        ctxSeed.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctxSeed.SaveChanges();

        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(child.Id, owner.Id, new CreateAdvocacyGoalModel { GoalText = "x", Category = "not-a-real-category" });

        Assert.False(result.Success);
        Assert.Equal("Categoría no válida. Debe ser: academic, behavioral, services o placement.", result.Message);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<BadRequestObjectResult>(action);
    }

    private sealed class TestController : ControllerBase
    {
    }

    public void Dispose() => _connection.Dispose();
}
