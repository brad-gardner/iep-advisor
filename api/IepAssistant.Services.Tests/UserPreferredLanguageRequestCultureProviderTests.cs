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

    /// <summary>
    /// Builds options via the SAME <see cref="RequestLocalizationSetup.Configure"/> Program.cs registers
    /// with <c>builder.Services.Configure&lt;RequestLocalizationOptions&gt;</c>, rather than a
    /// hand-duplicated copy that could silently drift from what actually ships.
    /// </summary>
    private static RequestLocalizationOptions BuildOptions()
    {
        var options = new RequestLocalizationOptions();
        RequestLocalizationSetup.Configure(options);
        return options;
    }

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

    // ----------------------------------------------------------------- HttpContext.Items fast path (set by
    // JwtBearerEvents.OnTokenValidated in Program.cs, which already loaded the user row for the
    // SecurityStamp check) — proven by seeding a DIFFERENT value in the database than in Items, so a
    // result matching Items (not the DB) shows the lookup was skipped, not merely that it agreed.

    [Fact]
    public async Task Provider_PreferredLanguageInHttpContextItems_WinsOverDbValue()
    {
        var userId = await SeedUserAsync("en"); // DB says "en".
        var httpContext = CreateHttpContext(userId);
        httpContext.Items[UserPreferredLanguageRequestCultureProvider.PreferredLanguageItemsKey] = "es";

        var result = await new UserPreferredLanguageRequestCultureProvider().DetermineProviderCultureResult(httpContext);

        Assert.NotNull(result);
        Assert.Equal("es", result!.Cultures[0].Value);
    }

    [Fact]
    public async Task Provider_UnsupportedLanguageInHttpContextItems_ReturnsNull_WithoutFallingBackToDb()
    {
        var userId = await SeedUserAsync("es"); // DB says "es" — if this were consulted, the result would be "es", not null.
        var httpContext = CreateHttpContext(userId);
        httpContext.Items[UserPreferredLanguageRequestCultureProvider.PreferredLanguageItemsKey] = "fr";

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

    /// <summary>
    /// P2 fix (2026-10-06 multilingual plan review): SupportedCultures is English-only, so the request's
    /// UI culture (resource strings) can resolve to "es" while its culture (date/number formatting) never
    /// does — only Program.cs's later, explicit per-recipient formatting (future phase) should format a
    /// date for a Spanish viewer. Proven against the real RequestLocalizationMiddleware + the actual
    /// RequestLocalizationSetup.Configure, not a re-implementation of the matching logic.
    /// </summary>
    [Fact]
    public async Task Precedence_SavedSpanishPreference_SetsUiCultureSpanish_ButCultureStaysEnglish()
    {
        var userId = await SeedUserAsync("es");
        var httpContext = CreateHttpContext(userId);

        var uiCulture = await ResolveAsync(httpContext, BuildOptions());

        Assert.Equal("es", uiCulture);
        var feature = httpContext.Features.Get<IRequestCultureFeature>();
        Assert.Equal("en", feature!.RequestCulture.Culture.Name);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
