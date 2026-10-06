using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06), phase 1: <c>PUT /api/auth/me</c>'s <c>PreferredLanguage</c> contract —
/// null/omitted leaves the stored value unchanged, a supported code (case-insensitive) is stored
/// lowercase, and anything else is a validation failure with a localized message — plus the Spanish
/// translation itself, asserted via the real <c>Messages.es.resx</c> resource (not a stub), for both an
/// AuthService validation failure and a service-level failure (invalid invite code on register). Same
/// real-SQLite fixture style as <see cref="AuthServiceTests"/>.
/// </summary>
public sealed class AuthServicePreferredLanguageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IConfiguration _configuration;

    public AuthServicePreferredLanguageTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-0123456789",
                ["Jwt:Issuer"] = "IepAssistant.Api",
                ["Jwt:Audience"] = "IepAssistant.Client",
                ["Jwt:ExpiryInDays"] = "7"
            })
            .Build();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private AuthService CreateService(ApplicationDbContext ctx)
        => new(_configuration, new UserRepository(ctx), ctx, TestSupport.TestLocalizers.Messages());

    private async Task<int> SeedUserAsync(string? initialLanguage = null)
    {
        using var ctx = CreateContext();
        var user = new User
        {
            Email = $"lang-{Guid.NewGuid():N}@example.com",
            PasswordHash = "x",
            FirstName = "Pat",
            LastName = "Parent",
            Role = UserRole.Parent,
            IsActive = true,
            PreferredLanguage = initialLanguage
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    // ----------------------------------------------------------------- valid

    [Fact]
    public async Task UpdateProfile_SupportedLanguageAnyCase_StoresLowercase()
    {
        var userId = await SeedUserAsync();

        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { PreferredLanguage = "ES" });

        Assert.True(result.Success);
        using (var ctx = CreateContext())
            Assert.Equal("es", ctx.Users.Single(u => u.Id == userId).PreferredLanguage);
    }

    // ----------------------------------------------------------------- unchanged

    [Fact]
    public async Task UpdateProfile_NullPreferredLanguage_LeavesStoredValueUnchanged()
    {
        var userId = await SeedUserAsync(initialLanguage: "es");

        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { FirstName = "Patricia" });

        Assert.True(result.Success);
        using (var ctx = CreateContext())
        {
            var user = ctx.Users.Single(u => u.Id == userId);
            Assert.Equal("es", user.PreferredLanguage);
            Assert.Equal("Patricia", user.FirstName);
        }
    }

    // ----------------------------------------------------------------- invalid

    [Fact]
    public async Task UpdateProfile_UnsupportedLanguage_FailsAndLeavesStoredValueUntouched()
    {
        var userId = await SeedUserAsync(initialLanguage: "en");

        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { PreferredLanguage = "fr" });

        Assert.False(result.Success);
        using (var ctx = CreateContext())
            Assert.Equal("en", ctx.Users.Single(u => u.Id == userId).PreferredLanguage);
    }

    [Fact]
    public async Task UpdateProfile_UnsupportedLanguage_AlsoDoesNotApplyOtherFieldsInTheSameRequest()
    {
        // Validation runs before any field is mutated, so a bad PreferredLanguage must not leave
        // FirstName/LastName/State partially applied.
        var userId = await SeedUserAsync();

        using (var ctx = CreateContext())
            await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { FirstName = "ShouldNotStick", PreferredLanguage = "klingon" });

        using (var ctx = CreateContext())
            Assert.NotEqual("ShouldNotStick", ctx.Users.Single(u => u.Id == userId).FirstName);
    }

    // ----------------------------------------------------------------- Spanish message

    [Fact]
    public async Task UpdateProfile_UnsupportedLanguage_UnderEnglishCulture_MessageIsEnglish()
    {
        var userId = await SeedUserAsync();

        using var _ = CultureScope.For("en");
        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { PreferredLanguage = "fr" });

        Assert.False(result.Success);
        Assert.Equal("Unsupported language. Use 'en' or 'es'.", result.Message);
    }

    [Fact]
    public async Task UpdateProfile_UnsupportedLanguage_UnderSpanishCulture_MessageIsSpanish()
    {
        var userId = await SeedUserAsync();

        using var _ = CultureScope.For("es");
        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).UpdateProfileAsync(userId, new UpdateProfileModel { PreferredLanguage = "fr" });

        Assert.False(result.Success);
        Assert.Equal("Idioma no admitido. Use 'en' o 'es'.", result.Message);
    }

    /// <summary>A second AuthService failure path (RegisterAsync's invalid-invite-code check), to prove
    /// the Spanish translation isn't a one-off on the PreferredLanguage message specifically.</summary>
    [Fact]
    public async Task Register_InvalidInviteCode_UnderSpanishCulture_MessageIsSpanish()
    {
        using var _ = CultureScope.For("es");

        ServiceResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).RegisterAsync(new RegisterModel
            {
                Email = "nuevo@example.com",
                Password = "Password123!",
                FirstName = "Nueva",
                LastName = "Usuaria",
                InviteCode = "not-a-real-code"
            });

        Assert.False(result.Success);
        Assert.Equal("El código de invitación no es válido o ha vencido.", result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
