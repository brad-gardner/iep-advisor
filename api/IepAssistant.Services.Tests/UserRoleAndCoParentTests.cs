using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P3a coverage: UserRole enum round-trip + tolerant converter, multi-owner co-parent access
/// through ChildAccess, and ChildAccess-authoritative data export. Uses a real SQLite in-memory
/// engine (same pattern as <see cref="AnalysisRunTestFixture"/>) so the value converter executes.
/// </summary>
public sealed class UserRoleAndCoParentTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public UserRoleAndCoParentTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    [Fact]
    public void UserRole_Admin_RoundTrips()
    {
        int userId;
        using (var ctx = CreateContext())
        {
            var user = new User
            {
                Email = "admin@example.com",
                PasswordHash = "x",
                FirstName = "Ada",
                LastName = "Admin",
                Role = UserRole.Admin
            };
            ctx.Users.Add(user);
            ctx.SaveChanges();
            userId = user.Id;
        }

        using (var ctx = CreateContext())
        {
            var reloaded = ctx.Users.Single(u => u.Id == userId);
            Assert.Equal(UserRole.Admin, reloaded.Role);
        }
    }

    [Fact]
    public void TolerantConverter_MapsLegacyUserValue_ToParent()
    {
        int userId;
        using (var ctx = CreateContext())
        {
            var user = new User
            {
                Email = "legacy@example.com",
                PasswordHash = "x",
                FirstName = "Lee",
                LastName = "Legacy",
                Role = UserRole.Parent
            };
            ctx.Users.Add(user);
            ctx.SaveChanges();
            userId = user.Id;

            // Force the raw stored value back to the legacy "User" string the old schema used.
            ctx.Database.ExecuteSqlRaw("UPDATE Users SET Role = 'User' WHERE Id = {0}", userId);
        }

        using (var ctx = CreateContext())
        {
            var reloaded = ctx.Users.Single(u => u.Id == userId);
            Assert.Equal(UserRole.Parent, reloaded.Role);
        }
    }

    [Fact]
    public async Task CoParent_SecondOwnerChildAccess_GrantsAccessAndAppearsInRepository()
    {
        int childId;
        int userBId;

        using (var ctx = CreateContext())
        {
            var ownerA = new User { Email = "a@example.com", PasswordHash = "x", FirstName = "Ann", LastName = "A", Role = UserRole.Parent };
            var ownerB = new User { Email = "b@example.com", PasswordHash = "x", FirstName = "Ben", LastName = "B", Role = UserRole.Parent };
            ctx.Users.AddRange(ownerA, ownerB);
            ctx.SaveChanges();
            userBId = ownerB.Id;

            var child = new ChildProfile { UserId = ownerA.Id, FirstName = "Cy", LastName = "Child", IsActive = true };
            ctx.ChildProfiles.Add(child);
            ctx.SaveChanges();
            childId = child.Id;

            ctx.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = child.Id,
                UserId = ownerA.Id,
                Role = AccessRole.Owner,
                IsActive = true,
                AcceptedAt = DateTime.UtcNow
            });
            // Second accepted Owner ChildAccess for co-parent B.
            ctx.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = child.Id,
                UserId = ownerB.Id,
                Role = AccessRole.Owner,
                IsActive = true,
                AcceptedAt = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var accessService = new AccessService(ctx);
            var hasOwner = await accessService.HasMinimumRoleAsync(childId, userBId, AccessRole.Owner);
            Assert.True(hasOwner);

            var repo = new ChildProfileRepository(ctx);
            var children = await repo.GetByUserIdAsync(userBId);
            Assert.Contains(children, c => c.Id == childId);
        }
    }

    [Fact]
    public async Task ExportData_IncludesCoOwnedChild_NotPrimaryOwner()
    {
        int childId;
        int userBId;

        using (var ctx = CreateContext())
        {
            var ownerA = new User { Email = "pa@example.com", PasswordHash = "x", FirstName = "Pat", LastName = "A", Role = UserRole.Parent };
            var ownerB = new User { Email = "pb@example.com", PasswordHash = "x", FirstName = "Pip", LastName = "B", Role = UserRole.Parent };
            ctx.Users.AddRange(ownerA, ownerB);
            ctx.SaveChanges();
            userBId = ownerB.Id;

            // Child's denormalized primary owner is A; B is a co-owner only via ChildAccess.
            var child = new ChildProfile { UserId = ownerA.Id, FirstName = "Quinn", LastName = "Child", IsActive = true };
            ctx.ChildProfiles.Add(child);
            ctx.SaveChanges();
            childId = child.Id;

            ctx.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = child.Id,
                UserId = ownerB.Id,
                Role = AccessRole.Owner,
                IsActive = true,
                AcceptedAt = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var service = new AccountService(
                new UserRepository(ctx),
                ctx,
                totpService: null!,
                protector: null!);

            var export = await service.ExportDataAsync(userBId);

            var childrenProp = export.GetType().GetProperty("children");
            Assert.NotNull(childrenProp);
            var children = (System.Collections.IEnumerable)childrenProp!.GetValue(export)!;
            var ids = children.Cast<object>()
                .Select(c => (int)c.GetType().GetProperty("Id")!.GetValue(c)!)
                .ToList();

            Assert.Contains(childId, ids);
        }
    }

    [Fact]
    public async Task UpdateUser_RejectsInvalidRoleString()
    {
        int userId;
        using (var ctx = CreateContext())
        {
            var user = new User
            {
                Email = "victim@example.com",
                PasswordHash = "x",
                FirstName = "V",
                LastName = "X",
                Role = UserRole.Parent,
            };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            userId = user.Id;
        }

        using (var ctx = CreateContext())
        {
            var service = new UserService(new UserRepository(ctx), ctx);
            var result = await service.UpdateUserAsync(
                userId,
                new Services.Models.UpdateUserModel { Role = "Hacker" });

            Assert.False(result.Success);
        }

        // Role must be unchanged after the rejected update.
        using (var ctx = CreateContext())
        {
            var user = await ctx.Users.FindAsync(userId);
            Assert.Equal(UserRole.Parent, user!.Role);
        }
    }

    public void Dispose() => _connection.Dispose();
}
