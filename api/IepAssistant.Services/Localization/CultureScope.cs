using System.Globalization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentCulture"/>/<see cref="CultureInfo.CurrentUICulture"/> for the
/// duration of out-of-request work (background workers, email composition, notification fan-out) and
/// restores whatever was ambient before, even on an exception — <c>using var _ = CultureScope.For(lang);</c>.
/// Inside an ASP.NET Core request, RequestLocalization middleware already sets these from the request
/// culture providers (see Program.cs); this helper is for code that runs outside that pipeline, where the
/// "current" culture is otherwise whatever the hosting thread happened to have (normally the server's
/// invariant/default culture).
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private bool _disposed;

    private CultureScope(CultureInfo previousCulture, CultureInfo previousUiCulture)
    {
        _previousCulture = previousCulture;
        _previousUiCulture = previousUiCulture;
    }

    /// <summary>
    /// Resolves <paramref name="language"/> via <see cref="SupportedLanguages.Normalize"/> (null/blank/
    /// unsupported all fall back to <see cref="SupportedLanguages.English"/> — never throws for untrusted
    /// input such as a stale or hand-edited database value), applies it as both the current culture and
    /// UI culture, and returns a disposable that restores the prior values.
    /// </summary>
    public static CultureScope For(string? language)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        var normalized = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;
        var culture = CultureInfo.GetCultureInfo(normalized);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        return new CultureScope(previousCulture, previousUiCulture);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        _disposed = true;
    }
}
