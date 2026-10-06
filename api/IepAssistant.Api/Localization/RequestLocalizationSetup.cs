using System.Globalization;
using Microsoft.AspNetCore.Localization;
using IepAssistant.Services.Localization;

namespace IepAssistant.Api.Localization;

/// <summary>
/// The actual <see cref="RequestLocalizationOptions"/> Program.cs configures via
/// <c>builder.Services.Configure&lt;RequestLocalizationOptions&gt;(RequestLocalizationSetup.Configure)</c> —
/// pulled into its own method so the precedence tests
/// (<c>UserPreferredLanguageRequestCultureProviderTests</c>) exercise this SAME configuration instead of a
/// hand-duplicated copy that could silently drift from what actually ships.
///
/// Multilingual plan (2026-10-06) phase 1, P2 fix: <see cref="RequestLocalizationOptions.SupportedCultures"/>
/// is English-only, on purpose — <see cref="CultureInfo.CurrentCulture"/> (date/number formatting) must NOT
/// follow the signed-in viewer's language. It is thread/request-ambient, so a Spanish-preferring sender
/// composing an email or notification in a background worker must never leak Spanish date formats into an
/// English recipient's content. <see cref="RequestLocalizationOptions.SupportedUICultures"/> (resource-string
/// selection, i.e. <see cref="CultureInfo.CurrentUICulture"/>) is the only thing this request pipeline
/// should vary by viewer, and is built from the full supported set (<see cref="SupportedLanguages.All"/>).
/// Explicit per-recipient date formatting for a non-English recipient is handled in a later phase (see
/// <see cref="CultureScope.For"/>, which mirrors this same culture/UI-culture split).
/// </summary>
public static class RequestLocalizationSetup
{
    public static void Configure(RequestLocalizationOptions options)
    {
        var supportedUICultures = SupportedLanguages.All.Select(l => new CultureInfo(l)).ToList();

        options.DefaultRequestCulture = new RequestCulture(SupportedLanguages.English, SupportedLanguages.English);
        options.SupportedCultures = new List<CultureInfo> { new(SupportedLanguages.English) };
        options.SupportedUICultures = supportedUICultures;
        // Lets the saved-preference/Accept-Language providers match a region-qualified UI culture tag
        // (e.g. "es-MX") to its neutral parent ("es") when the exact tag isn't itself in
        // SupportedUICultures. FallBackToParentCultures is moot while SupportedCultures is English-only,
        // but set for symmetry and in case a later phase widens SupportedCultures.
        options.FallBackToParentCultures = true;
        options.FallBackToParentUICultures = true;
        options.RequestCultureProviders = new List<IRequestCultureProvider>
        {
            // Saved account preference first (requires UseAuthentication to have already run — see
            // Program.cs), then the browser's Accept-Language header, then DefaultRequestCulture ("en").
            new UserPreferredLanguageRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider()
        };
    }
}
