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
/// "switching persists across reload" from the design checkpoint. In practice this costs no extra query
/// at all on the normal authenticated path: the JWT bearer handler's <c>OnTokenValidated</c> (Program.cs)
/// already loads the user row for the SecurityStamp check and stashes <c>PreferredLanguage</c> in
/// <see cref="HttpContext.Items"/> under <see cref="PreferredLanguageItemsKey"/>; this provider reads that
/// first and only falls back to its own DB lookup when the key is absent (e.g. an unauthenticated request,
/// or a token shape that skipped the SecurityStamp check).
/// </summary>
public class UserPreferredLanguageRequestCultureProvider : RequestCultureProvider
{
    /// <summary>
    /// <see cref="HttpContext.Items"/> key the JWT bearer handler's <c>OnTokenValidated</c> (Program.cs)
    /// writes the validated user's raw <c>PreferredLanguage</c> under. Public so Program.cs can set it
    /// without this provider exposing anything else about its DB fallback.
    /// </summary>
    public const string PreferredLanguageItemsKey = "IepAssistant.UserPreferredLanguage";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(PreferredLanguageItemsKey, out var cached))
        {
            var normalized = SupportedLanguages.Normalize(cached as string);
            return Task.FromResult(normalized == null ? null : new ProviderCultureResult(normalized));
        }

        return ResolveAsync(httpContext);
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
