using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
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
            configuration: new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "https://app.example.com" }).Build(),
            localizer: TestLocalizers.Messages());
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

    /// <summary>Seeds a child the user owns via an accepted, active Owner <see cref="ChildAccess"/> —
    /// the same authz plane ExportDataAsync's children query reads (AccountServiceTests, export test).</summary>
    private int SeedOwnedChild(ApplicationDbContext ctx, int userId, string firstName)
    {
        var child = new ChildProfile { UserId = userId, FirstName = firstName, LastName = "Child", IsActive = true };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        ctx.ChildAccesses.Add(new ChildAccess
        {
            ChildProfileId = child.Id,
            UserId = userId,
            Role = AccessRole.Owner,
            IsActive = true,
            AcceptedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();

        return child.Id;
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
        // Phase 4 review fix: the public cancel-deletion landing page opens in this account's own
        // language before anyone has to sign in.
        Assert.Contains("&lang=en", email.LastCancelUrl);
    }

    [Fact]
    public async Task ScheduleDeletion_SpanishAccount_CancelUrlCarriesSpanishLangParam()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId;
        using (var ctx = CreateContext())
        {
            userId = SeedActiveParent(ctx, "es-deletion@example.com");
            ctx.Users.Find(userId)!.PreferredLanguage = "es";
            ctx.SaveChanges();
        }

        CapturingEmailService email;
        using (var ctx = CreateContext())
        {
            var (service, capturedEmail) = CreateService(ctx, provider);
            email = capturedEmail;
            var result = await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            Assert.True(result.Success, result.Message);
        }

        Assert.NotNull(email.LastCancelUrl);
        Assert.Contains("&lang=es", email.LastCancelUrl);
    }

    /// <summary>Unified-analysis plan, phase 4: the export must carry a child's AnalysisRuns (with their
    /// Sources and Sections) alongside the legacy IepAnalyses, and must not leak another user's child's
    /// runs — the same child-scoping bug class ChildAccess exists to prevent elsewhere in the export.</summary>
    [Fact]
    public async Task ExportData_IncludesTheChildsAnalysisRuns_WithSourcesAndSections_NotAnotherUsersChild()
    {
        int userId, childId, runId;
        using (var ctx = CreateContext())
        {
            userId = SeedActiveParent(ctx, "parent@example.com");
            childId = SeedOwnedChild(ctx, userId, "Jacob");

            var run = new AnalysisRun
            {
                ChildProfileId = childId,
                Status = AnalysisRunStatus.Completed,
                OverallSummary = "Looks good overall.",
            };
            ctx.AnalysisRuns.Add(run);
            ctx.SaveChanges();
            runId = run.Id;

            ctx.AnalysisRunSources.Add(new AnalysisRunSource
            {
                AnalysisRunId = run.Id,
                SourceType = AnalysisSourceType.IepDocument,
                SourceId = 999,
                SourceLabel = "IEP — Annual Review",
                SourceContentSnapshot = "Extracted IEP text for the run.",
                Status = AnalysisRunSourceStatus.Completed
            });
            ctx.AnalysisRunSections.Add(new AnalysisRunSection
            {
                AnalysisRunId = run.Id,
                SectionKind = "iep_goals",
                Analysis = "{\"goals\":[]}",
                DisplayOrder = 0
            });
            ctx.SaveChanges();

            // A second user's child has its own run — must never show up in the first user's export.
            var otherUserId = SeedActiveParent(ctx, "other@example.com");
            var otherChildId = SeedOwnedChild(ctx, otherUserId, "OtherKid");
            ctx.AnalysisRuns.Add(new AnalysisRun { ChildProfileId = otherChildId, Status = AnalysisRunStatus.Completed });
            ctx.SaveChanges();
        }

        using var exportCtx = CreateContext();
        var (service, _) = CreateService(exportCtx);
        var data = await service.ExportDataAsync(userId);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        var runs = doc.RootElement.GetProperty("analysisRuns");
        Assert.Equal(1, runs.GetArrayLength()); // not the other user's child's run

        var exportedRun = runs[0];
        Assert.Equal(runId, exportedRun.GetProperty("Id").GetInt32());
        Assert.Equal(childId, exportedRun.GetProperty("ChildProfileId").GetInt32());
        Assert.Equal("Completed", exportedRun.GetProperty("Status").GetString());

        var sources = exportedRun.GetProperty("sources");
        Assert.Equal(1, sources.GetArrayLength());
        Assert.Equal("IepDocument", sources[0].GetProperty("SourceType").GetString());
        Assert.Equal("Extracted IEP text for the run.", sources[0].GetProperty("SourceContentSnapshot").GetString());

        var sections = exportedRun.GetProperty("sections");
        Assert.Equal(1, sections.GetArrayLength());
        Assert.Equal("iep_goals", sections[0].GetProperty("SectionKind").GetString());
        Assert.Equal("{\"goals\":[]}", sections[0].GetProperty("Analysis").GetString());
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

    [Fact]
    public async Task CancelDeletionByToken_ExpiresWithTheGracePeriod_EvenIfThePurgeWorkerNeverRan()
    {
        var provider = DataProtectionProvider.Create("shared-test-app");
        int userId; string token;
        using (var ctx = CreateContext())
            userId = SeedActiveParent(ctx);
        using (var ctx = CreateContext())
        {
            var (service, email) = CreateService(ctx, provider);
            await service.ScheduleDeletionAsync(userId, "Password1!", mfaCode: null);
            token = ExtractToken(email.LastCancelUrl!);
        }

        // Simulate a purge worker that fell behind: the request is now 45 days old but the row is still there.
        using (var ctx = CreateContext())
        {
            var user = ctx.Users.Find(userId)!;
            var aged = user.DeletionRequestedAt!.Value.AddDays(-45);
            user.DeletionRequestedAt = aged;
            ctx.SaveChanges();
            // Re-mint a token bound to the aged timestamp so only the age (not a ticks mismatch) is under test.
            token = TokenFor(provider, userId, aged);
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx, provider);
            var result = await service.CancelDeletionByTokenAsync(token);
            Assert.False(result.Success);
        }
    }

    private static string TokenFor(IDataProtectionProvider provider, int userId, DateTime requestedAt) =>
        provider.CreateProtector(AccountService.DeletionTokenPurpose).Protect($"{userId}|{requestedAt.Ticks}");

    // ----------------------------------------------------------------- multilingual plan (2026-10-06)
    // phase 2, carry-over P3: CancelDeletionAsync's message renders in the UI culture — English under
    // "en", Spanish under "es". Previously hard-coded English, silently overriding AuthController's
    // own localized fallback (<c>result.Message ?? _localizer["AuthApi.FailedToCancelDeletion"]</c>).

    [Fact]
    public async Task CancelDeletion_NoPendingRequest_UnderEnglishCulture_MessageIsEnglish()
    {
        int userId;
        using (var seedCtx = CreateContext())
            userId = SeedActiveParent(seedCtx, "en-nopending@example.com");

        using var cultureScope = CultureScope.For("en");
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);
        var result = await service.CancelDeletionAsync(userId);

        Assert.False(result.Success);
        Assert.Equal("No pending deletion request", result.Message);
    }

    [Fact]
    public async Task CancelDeletion_NoPendingRequest_UnderSpanishCulture_MessageIsSpanish()
    {
        int userId;
        using (var seedCtx = CreateContext())
            userId = SeedActiveParent(seedCtx, "es-nopending@example.com");

        using var cultureScope = CultureScope.For("es");
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);
        var result = await service.CancelDeletionAsync(userId);

        Assert.False(result.Success);
        Assert.Equal("No hay una solicitud de eliminación pendiente", result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
