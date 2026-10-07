using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 2, carry-over P3: <see cref="MfaService"/>'s
/// <c>ServiceResult</c> failure messages render in the active UI culture (English/Spanish). Previously
/// these were hard-coded English, which silently overrode AuthController's own localized fallback (see
/// <c>result.Message ?? _localizer["AuthApi.FailedToDisableMfa"]</c>) since the service's message was
/// never null.
/// </summary>
public sealed class MfaServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IConfiguration _configuration;

    public MfaServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = "test-key-at-least-32-bytes-long-0123456789" })
            .Build();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private MfaService CreateService(ApplicationDbContext ctx) => new(
        new UserRepository(ctx),
        ctx,
        totpService: null!,
        protector: null!,
        _configuration,
        TestSupport.TestLocalizers.Messages());

    private int SeedUserWithoutMfa()
    {
        using var ctx = CreateContext();
        var user = new User { Email = "nomfa@example.com", PasswordHash = "x", FirstName = "No", LastName = "Mfa", Role = UserRole.Parent, MfaEnabled = false };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task Disable_MfaNotEnabled_UnderEnglishCulture_MessageIsEnglish()
    {
        var userId = SeedUserWithoutMfa();

        using var _ = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).DisableAsync(userId, "whatever", "000000");

        Assert.False(result.Success);
        Assert.Equal("MFA is not enabled.", result.Message);
    }

    [Fact]
    public async Task Disable_MfaNotEnabled_UnderSpanishCulture_MessageIsSpanish()
    {
        var userId = SeedUserWithoutMfa();

        using var _ = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).DisableAsync(userId, "whatever", "000000");

        Assert.False(result.Success);
        Assert.Equal("La verificación en dos pasos no está activada.", result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
