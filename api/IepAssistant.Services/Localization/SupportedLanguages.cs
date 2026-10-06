namespace IepAssistant.Services.Localization;

/// <summary>
/// The one place that defines which languages the app supports (plan: multilingual English + Spanish,
/// phase 1). <see cref="Domain.Entities.User.PreferredLanguage"/>, <c>PUT /api/auth/me</c> validation, the
/// request-culture providers, and <see cref="CultureScope"/> all resolve through here so the supported set
/// never drifts between call sites.
/// </summary>
public static class SupportedLanguages
{
    public const string English = "en";
    public const string Spanish = "es";

    /// <summary>Lowercase, in no particular priority order — callers needing a default should use
    /// <see cref="English"/> explicitly rather than assuming list order.</summary>
    public static readonly IReadOnlyList<string> All = new[] { English, Spanish };

    /// <summary>
    /// Trims and lowercases <paramref name="language"/> and returns it only if supported; otherwise null.
    /// Used both to validate a stored/requested exact code ("EN", " es ") and, defensively, a value read
    /// back from the database that might predate this feature or have been written by something else.
    /// This is NOT the same normalization as an Accept-Language header value like "es-MX" — that
    /// region-qualified case is handled by ASP.NET Core's parent-culture fallback in RequestLocalization
    /// (see Program.cs), not here.
    /// </summary>
    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var trimmed = language.Trim().ToLowerInvariant();
        return All.Contains(trimmed) ? trimmed : null;
    }
}
