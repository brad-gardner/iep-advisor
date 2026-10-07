using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// <see cref="ChildProfileService.UpdateAsync"/> partial-update semantics for the parent child form's
/// Grade Level / Disability Category dropdowns: null leaves a field unchanged, empty clears it ("Not set").
/// </summary>
public sealed class ChildProfileServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ChildProfileServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext CreateContext() => new(_options);

    private static ChildProfileService CreateService(ApplicationDbContext ctx) =>
        new(new ChildProfileRepository(ctx), new AccessService(ctx), ctx, TestSupport.TestLocalizers.Messages());

    private (int UserId, int ChildId) SeedChild()
    {
        using var ctx = CreateContext();
        var owner = new User { Email = "owner@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent };
        ctx.Users.Add(owner);
        ctx.SaveChanges();
        var child = new ChildProfile { UserId = owner.Id, FirstName = "Jordan", GradeLevel = "4th", DisabilityCategory = "Autism", SchoolDistrict = "Riverside" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.SaveChanges();
        return (owner.Id, child.Id);
    }

    private ChildProfile Reload(int childId)
    {
        using var ctx = CreateContext();
        return ctx.ChildProfiles.AsNoTracking().Single(c => c.Id == childId);
    }

    [Fact]
    public async Task Update_EmptyGradeAndDisability_ClearsBoth()
    {
        var (userId, childId) = SeedChild();
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).UpdateAsync(childId, userId, new UpdateChildProfileModel { GradeLevel = "", DisabilityCategory = "  " });
            Assert.True(result.Success);
        }

        var child = Reload(childId);
        Assert.Null(child.GradeLevel);
        Assert.Null(child.DisabilityCategory);
        Assert.Equal("Riverside", child.SchoolDistrict);
    }

    [Fact]
    public async Task Update_NullGradeAndDisability_LeavesThemUnchanged()
    {
        var (userId, childId) = SeedChild();
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).UpdateAsync(childId, userId, new UpdateChildProfileModel { SchoolDistrict = "Lakeside" });
            Assert.True(result.Success);
        }

        var child = Reload(childId);
        Assert.Equal("4th", child.GradeLevel);
        Assert.Equal("Autism", child.DisabilityCategory);
        Assert.Equal("Lakeside", child.SchoolDistrict);
    }

    [Fact]
    public async Task Update_NewValues_AreStored()
    {
        var (userId, childId) = SeedChild();
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).UpdateAsync(childId, userId, new UpdateChildProfileModel { GradeLevel = "5th", DisabilityCategory = "Other health impairment" });
            Assert.True(result.Success);
        }

        var child = Reload(childId);
        Assert.Equal("5th", child.GradeLevel);
        Assert.Equal("Other health impairment", child.DisabilityCategory);
    }

    // ----------------------------------------------------------------- multilingual plan (2026-10-06)
    // phase 2: Children.NotFound renders in the UI culture — English under "en", Spanish under "es".

    [Fact]
    public async Task Update_UnknownChild_UnderEnglishCulture_MessageIsEnglish()
    {
        var (userId, childId) = SeedChild();

        using var _ = CultureScope.For("en");
        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateAsync(childId, userId + 999, new UpdateChildProfileModel());

        Assert.False(result.Success);
        Assert.Equal("Child profile not found.", result.Message);
    }

    [Fact]
    public async Task Update_UnknownChild_UnderSpanishCulture_MessageIsSpanish()
    {
        var (userId, childId) = SeedChild();

        using var _ = CultureScope.For("es");
        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateAsync(childId, userId + 999, new UpdateChildProfileModel());

        Assert.False(result.Success);
        Assert.Equal("Perfil del hijo no encontrado.", result.Message);
    }
}
