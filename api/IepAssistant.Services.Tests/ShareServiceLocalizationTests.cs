using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 2: <see cref="ShareService"/>'s <see cref="ServiceResult"/>
/// failure messages render in the active UI culture (English/Spanish), via the real
/// <c>Messages.resx</c>/<c>Messages.es.resx</c> resources — same pattern as
/// <see cref="AuthServicePreferredLanguageTests"/>.
/// </summary>
public sealed class ShareServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ShareServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static ShareService CreateService(ApplicationDbContext ctx) => new(
        ctx,
        new AccessService(ctx),
        new UserRepository(ctx),
        new TestSupport.TestEmailServiceBase(),
        NullLogger<ShareService>.Instance,
        TestSupport.TestLocalizers.Messages());

    /// <summary>Seeds a child with no ChildAccess row for <paramref name="nonOwnerUserId"/> at all, so
    /// <see cref="IAccessService.GetRoleAsync"/> resolves null (not Owner) — the simplest way to reach
    /// ShareService.InviteAsync's "only owners can invite" guard.</summary>
    private int SeedChildWithNoAccessForUser(out int nonOwnerUserId)
    {
        using var ctx = CreateContext();
        var owner = new User { Email = "owner@example.com", PasswordHash = "x", FirstName = "Owen", LastName = "Owner", Role = UserRole.Parent };
        var stranger = new User { Email = "stranger@example.com", PasswordHash = "x", FirstName = "Sam", LastName = "Stranger", Role = UserRole.Parent };
        ctx.Users.AddRange(owner, stranger);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = owner.Id, FirstName = "Kid", IsActive = true };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        nonOwnerUserId = stranger.Id;
        return child.Id;
    }

    [Fact]
    public async Task Invite_ByNonOwner_UnderEnglishCulture_MessageIsEnglish()
    {
        var childId = SeedChildWithNoAccessForUser(out var strangerId);

        using var _ = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).InviteAsync(childId, strangerId, "invitee@example.com", AccessRole.Viewer);

        Assert.False(result.Success);
        Assert.Equal("Only owners can invite users.", result.Message);
    }

    [Fact]
    public async Task Invite_ByNonOwner_UnderSpanishCulture_MessageIsSpanish()
    {
        var childId = SeedChildWithNoAccessForUser(out var strangerId);

        using var _ = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).InviteAsync(childId, strangerId, "invitee@example.com", AccessRole.Viewer);

        Assert.False(result.Success);
        Assert.Equal("Solo los propietarios pueden invitar a usuarios.", result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
