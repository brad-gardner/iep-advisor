using System.Globalization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentUICulture"/> for the duration of out-of-request work (background
/// workers, email composition, notification fan-out) and restores whatever was ambient before, even on an
/// exception — <c>using var _ = CultureScope.For(lang);</c>. Inside an ASP.NET Core request,
/// RequestLocalization middleware already sets this from the request culture providers (see Program.cs);
/// this helper is for code that runs outside that pipeline, where the "current" UI culture is otherwise
/// whatever the hosting thread happened to have (normally the server's invariant/default culture).
///
/// Multilingual plan (2026-10-06) phase 1, P2 fix: this deliberately touches ONLY
/// <see cref="CultureInfo.CurrentUICulture"/> (resource-string selection — this is what every Phase 1
/// caller needs, e.g. <c>EmailService</c> selecting a Spanish subject/body). It leaves
/// <see cref="CultureInfo.CurrentCulture"/> (date/number formatting) exactly as it found it — mirroring
/// <c>RequestLocalizationSetup.Configure</c>'s English-only <c>SupportedCultures</c> for the same reason:
/// CurrentCulture is thread/request-ambient, so a Spanish recipient's language must never leak Spanish
/// date formats into unrelated work sharing that thread. A caller that needs to format a date FOR that
/// recipient does so explicitly (a later phase), not via this ambient switch.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousUiCulture;
    private bool _disposed;

    private CultureScope(CultureInfo previousUiCulture)
    {
        _previousUiCulture = previousUiCulture;
    }

    /// <summary>
    /// Resolves <paramref name="language"/> via <see cref="SupportedLanguages.Normalize"/> (null/blank/
    /// unsupported all fall back to <see cref="SupportedLanguages.English"/> — never throws for untrusted
    /// input such as a stale or hand-edited database value), applies it as the current UI culture only,
    /// and returns a disposable that restores the prior UI culture.
    /// </summary>
    public static CultureScope For(string? language)
    {
        var previousUiCulture = CultureInfo.CurrentUICulture;

        var normalized = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(normalized);

        return new CultureScope(previousUiCulture);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CultureInfo.CurrentUICulture = _previousUiCulture;
        _disposed = true;
    }
}
