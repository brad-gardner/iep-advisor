using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Tests.TestSupport;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 2, decision 3: the signed cancel-deletion link. ScheduleDeletionAsync
/// deactivates the account and bumps SecurityStamp immediately, which invalidates the very session
/// that requested the deletion — CancelDeletionByTokenAsync is the only way back in, and must keep
/// working after that revocation while rejecting anything forged, corrupted, or stale.
/// </summary>
public sealed class AccountServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AccountServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class CapturingEmailService : TestEmailServiceBase
    {
        public string? LastCancelUrl { get; private set; }
        public override Task SendAccountDeletionCancelLinkEmailAsync(string toEmail, string firstName, string cancelUrl, DateTime purgeDate, CancellationToken ct = default)
        {
            LastCancelUrl = cancelUrl;
            return Task.CompletedTask;
        }
    }

    // Both services share ONE data protection provider per test — a real token minted by one
    // AccountService instance must validate under another (mirrors two different requests hitting the
    // same running app), but a token from a DIFFERENT provider (a different app/key ring) must not.
    private (AccountService Service, CapturingEmailService Email) CreateService(ApplicationDbContext ctx, IDataProtectionProvider? provider = null)
    {
        var email = new CapturingEmailService();
        var service = new AccountService(
            new UserRepository(ctx),
            ctx,
            totpService: null!,
            protector: null!,
            emailService: email,
            dataProtectionProvider: provider ?? DataProtectionProvider.Create("shared-test-app"),
            configuration: new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "https://app.example.com" }).Build());
        return (service, email);
    }

    private static string ExtractToken(string cancelUrl)
    {
        var query = new Uri(cancelUrl).Query.TrimStart('?');
        var pair = query.Split('&').Single(p => p.StartsWith("token="));
        return Uri.UnescapeDataString(pair["token=".Length..]);
    }

    private int SeedActiveParent(ApplicationDbContext ctx, string email = "parent@example.com")
    {
        var user = new User { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"), FirstName = "Pat", LastName = "Parent", Role = UserRole.Parent, IsActive = true };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task ScheduleDeletion_DeactivatesAccount_AndEmailsASignedCancelLink()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);

        CapturingEmailService email;
        using (var ctx = CreateContext())
        {
            var (service, capturedEmail) = CreateService(ctx, provider);
            email = capturedEmail;
            var result = await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var user = verify.Users.Find(userId)!;
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletionRequestedAt);
        Assert.NotNull(email.LastCancelUrl);
        Assert.Contains("/account/cancel-deletion?token=", email.LastCancelUrl);
    }

    [Fact]
    public async Task CancelDeletionByToken_WorksEvenAfterTheSessionWasRevoked()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId;
        string token;
        int stampAfterSchedule;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);

        using (var ctx = CreateContext())
        {
            var (service, email) = CreateService(ctx, provider);
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            token = ExtractToken(email.LastCancelUrl!);
        }

        using (var ctx = CreateContext())
            stampAfterSchedule = ctx.Users.Find(userId)!.SecurityStamp;

        // A brand-new AccountService instance (a fresh, unauthenticated request) — there is no active
        // session; the token itself is the only credential.
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            var result = await service.CancelDeletionByTokenAsync(token);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var user = verify.Users.Find(userId)!;
        Assert.True(user.IsActive);
        Assert.Null(user.DeletionRequestedAt);
        Assert.NotEqual(stampAfterSchedule, user.SecurityStamp); // bumped again on reactivation
    }

    [Fact]
    public async Task CancelDeletionByToken_RejectsFabricatedToken()
    {
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);

        var result = await service.CancelDeletionByTokenAsync("not-a-real-token-at-all");

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task CancelDeletionByToken_RejectsTokenMintedByADifferentKeyRing()
    {
        int userId;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);

        string token;
        using (var ctx = CreateContext())
        {
            var (service, email) = CreateService(ctx, DataProtectionProvider.Create("app-A"));
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            token = ExtractToken(email.LastCancelUrl!);
        }

        // A different key ring cannot decrypt/authenticate a token it never issued — the same failure
        // mode as a forged token (CryptographicException from IDataProtector.Unprotect).
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, DataProtectionProvider.Create("app-B"));
            var result = await service.CancelDeletionByTokenAsync(token);
            Assert.False(result.Success);
            Assert.Contains("Invalid", result.Message);
        }
    }

    [Fact]
    public async Task CancelDeletionByToken_RejectsStaleTokenFromASupersededRequest()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);

        string firstToken;
        using (var ctx = CreateContext())
        {
            var (service, email) = CreateService(ctx, provider);
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            firstToken = ExtractToken(email.LastCancelUrl!);
        }

        // Cancel the first request, then re-request deletion — a new DeletionRequestedAt is stamped.
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            await service.CancelDeletionByTokenAsync(firstToken);
        }
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
        }

        // The OLD token (from the first, now-superseded request) must no longer work.
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            var result = await service.CancelDeletionByTokenAsync(firstToken);
            Assert.False(result.Success);
            Assert.Contains("Invalid", result.Message);
        }
    }

    [Fact]
    public async Task CancelDeletionByToken_RejectsToken_ForAUserThatNoLongerExists()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);

        string token;
        using (var ctx = CreateContext())
        {
            var (service, email) = CreateService(ctx, provider);
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            token = ExtractToken(email.LastCancelUrl!);
        }

        // Simulate the purge worker having already run (parent path deletes the row outright).
        using (var ctx = CreateContext())
        {
            ctx.Users.Remove(ctx.Users.Find(userId)!);
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            var result = await service.CancelDeletionByTokenAsync(token);
            Assert.False(result.Success);
            Assert.Contains("Invalid", result.Message);
        }
    }

    public void Dispose() => _connection.Dispose();
}
