using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IepAssistant.Api.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06), phase 1: the Program.cs RequestLocalization wiring — saved account
/// preference first, then Accept-Language (with "es-MX" falling back to its neutral parent "es" via
/// <c>FallBackToParentCultures</c>), then the "en" default. Exercises the real
/// <see cref="UserPreferredLanguageRequestCultureProvider"/> and the framework's
/// <see cref="AcceptLanguageHeaderRequestCultureProvider"/> directly against a fake
/// <see cref="HttpContext"/>, in the same order Program.cs registers them, rather than re-implementing
/// their matching logic.
/// </summary>
public sealed class UserPreferredLanguageRequestCultureProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public UserPreferredLanguageRequestCultureProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    private async Task<int> SeedUserAsync(string? preferredLanguage)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "x",
            FirstName = "Pat",
            LastName = "Parent",
            Role = UserRole.Parent,
            IsActive = true,
            PreferredLanguage = preferredLanguage
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    private HttpContext CreateHttpContext(int? authenticatedUserId, string? acceptLanguageHeader = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddLogging(); // RequestLocalizationMiddleware requires an ILoggerFactory.

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        if (authenticatedUserId.HasValue)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()) },
                authenticationType: "Test");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        if (acceptLanguageHeader != null)
            httpContext.Request.Headers.AcceptLanguage = acceptLanguageHeader;

        return httpContext;
    }

    private static RequestLocalizationOptions BuildOptions() => new()
    {
        DefaultRequestCulture = new RequestCulture("en"),
        SupportedCultures = new List<CultureInfo> { new("en"), new("es") },
        SupportedUICultures = new List<CultureInfo> { new("en"), new("es") },
        FallBackToParentCultures = true,
        FallBackToParentUICultures = true,
        RequestCultureProviders = new List<IRequestCultureProvider>
        {
            new UserPreferredLanguageRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider()
        }
    };

    /// <summary>
    /// Runs the SAME <see cref="RequestLocalizationMiddleware"/> Program.cs wires up via
    /// <c>app.UseRequestLocalization()</c>, rather than re-implementing its provider-walking and
    /// supported-culture matching logic. <see cref="AcceptLanguageHeaderRequestCultureProvider"/> returns
    /// the Accept-Language header's requested cultures UNFILTERED (e.g. "es-MX") — the middleware itself,
    /// not the provider, is what matches a candidate against SupportedCultures with
    /// FallBackToParentCultures, so exercising the provider alone (as the three "Provider_*" tests above
    /// do for OUR custom provider) would not catch a mismatch here.
    /// </summary>
    private static async Task<string> ResolveAsync(HttpContext httpContext, RequestLocalizationOptions options)
    {
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var middleware = new RequestLocalizationMiddleware(_ => Task.CompletedTask, Options.Create(options), loggerFactory);

        await middleware.Invoke(httpContext);

        var feature = httpContext.Features.Get<IRequestCultureFeature>();
        return feature?.RequestCulture.UICulture.Name ?? options.DefaultRequestCulture.Culture.Name;
    }

    // ----------------------------------------------------------------- the provider in isolation

    [Fact]
    public async Task Provider_AuthenticatedUserWithSavedSpanish_ReturnsSpanish()
    {
        var userId = await SeedUserAsync("es");
        var httpContext = CreateHttpContext(userId);

        var result = await new UserPreferredLanguageRequestCultureProvider().DetermineProviderCultureResult(httpContext);

        Assert.NotNull(result);
        Assert.Equal("es", result!.Cultures[0].Value);
    }

    [Fact]
    public async Task Provider_Unauthenticated_ReturnsNull()
    {
        var httpContext = CreateHttpContext(authenticatedUserId: null);

        var result = await new UserPreferredLanguageRequestCultureProvider().DetermineProviderCultureResult(httpContext);

        Assert.Null(result);
    }

    [Fact]
    public async Task Provider_AuthenticatedUserWithNoSavedPreference_ReturnsNull()
    {
        var userId = await SeedUserAsync(null);
        var httpContext = CreateHttpContext(userId);

        var result = await new UserPreferredLanguageRequestCultureProvider().DetermineProviderCultureResult(httpContext);

        Assert.Null(result);
    }

    // ----------------------------------------------------------------- precedence, as registered in Program.cs

    [Fact]
    public async Task Precedence_SavedPreferenceBeatsAcceptLanguageHeader()
    {
        var userId = await SeedUserAsync("es");
        var httpContext = CreateHttpContext(userId, acceptLanguageHeader: "en");

        var culture = await ResolveAsync(httpContext, BuildOptions());

        Assert.Equal("es", culture);
    }

    [Fact]
    public async Task Precedence_UnauthenticatedAcceptLanguageEsMx_FallsBackToNeutralSpanish()
    {
        var httpContext = CreateHttpContext(authenticatedUserId: null, acceptLanguageHeader: "es-MX,en;q=0.8");

        var culture = await ResolveAsync(httpContext, BuildOptions());

        Assert.Equal("es", culture);
    }

    [Fact]
    public async Task Precedence_UnsupportedAcceptLanguage_FallsBackToEnglishDefault()
    {
        var httpContext = CreateHttpContext(authenticatedUserId: null, acceptLanguageHeader: "fr-FR,fr;q=0.9");

        var culture = await ResolveAsync(httpContext, BuildOptions());

        Assert.Equal("en", culture);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
