using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 2: <see cref="SubscriptionService"/>'s <see cref="ServiceResult"/>
/// failure messages render in the active UI culture (English/Spanish), via the real
/// <c>Messages.resx</c>/<c>Messages.es.resx</c> resources.
/// </summary>
public sealed class SubscriptionServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public SubscriptionServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static SubscriptionService CreateService(ApplicationDbContext ctx) => new(
        ctx,
        new ConfigurationBuilder().Build(),
        NullLogger<SubscriptionService>.Instance,
        TestSupport.TestLocalizers.Messages());

    private int SeedActiveSubscriber()
    {
        using var ctx = CreateContext();
        var user = new User
        {
            Email = "subscriber@example.com", PasswordHash = "x", FirstName = "Sub", LastName = "Scriber",
            Role = UserRole.Parent, SubscriptionStatus = "active"
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task RedeemBetaCode_AlreadyActiveSubscription_UnderEnglishCulture_MessageIsEnglish()
    {
        var userId = SeedActiveSubscriber();

        using var _ = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).RedeemBetaCodeAsync(userId, "ANYCODE");

        Assert.False(result.Success);
        Assert.Equal("You already have an active subscription", result.Message);
    }

    [Fact]
    public async Task RedeemBetaCode_AlreadyActiveSubscription_UnderSpanishCulture_MessageIsSpanish()
    {
        var userId = SeedActiveSubscriber();

        using var _ = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).RedeemBetaCodeAsync(userId, "ANYCODE");

        Assert.False(result.Success);
        Assert.Equal("Ya tiene una suscripción activa", result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
