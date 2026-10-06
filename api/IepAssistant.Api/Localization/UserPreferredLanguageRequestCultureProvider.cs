using System.Security.Claims;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Localization;

namespace IepAssistant.Api.Localization;

/// <summary>
/// First <see cref="IRequestCultureProvider"/> in <c>RequestLocalizationOptions.RequestCultureProviders</c>
/// (Program.cs) — resolves the request culture from the signed-in user's saved
/// <see cref="Domain.Entities.User.PreferredLanguage"/>, falling through to the next provider
/// (<see cref="AcceptLanguageHeaderRequestCultureProvider"/>) when the request is unauthenticated or the
/// user has no saved preference. Must be registered AFTER <c>UseAuthentication</c> in the pipeline so
/// <c>HttpContext.User</c> already carries the JWT's claims by the time this runs.
///
/// Decision: a per-request DB lookup, not a JWT claim. JWTs here live up to <c>Jwt:ExpiryInDays</c>
/// (default 7) with no refresh-token flow (<c>AuthService.RefreshTokenAsync</c> is
/// <see cref="NotImplementedException"/> — a token is only reissued by signing in again), so a claim
/// baked in at login would leave a language change silently stale for up to a week — the opposite of
/// "switching persists across reload" from the design checkpoint. A single indexed primary-key lookup per
/// authenticated request is the "one cheap DB lookup" the plan explicitly allows, and it is cached in
/// <see cref="HttpContext.Items"/> so a provider invoked more than once in the same request (not expected
/// from the stock middleware, but cheap insurance) does not repeat it.
/// </summary>
public class UserPreferredLanguageRequestCultureProvider : RequestCultureProvider
{
    private static readonly object CacheKey = new();

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CacheKey, out var cached))
            return (ProviderCultureResult?)cached;

        var result = await ResolveAsync(httpContext);
        httpContext.Items[CacheKey] = result;
        return result;
    }

    private static async Task<ProviderCultureResult?> ResolveAsync(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var storedLanguage = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        var normalized = SupportedLanguages.Normalize(storedLanguage);
        return normalized == null ? null : new ProviderCultureResult(normalized);
    }
}
