using System.Globalization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Multilingual plan (2026-10-06) phase 4: explicit, per-recipient-LANGUAGE date formatting for
/// notification title/body text — never the ambient <see cref="CultureInfo.CurrentCulture"/>, which
/// <see cref="CultureScope"/> deliberately leaves untouched (see its doc comment) specifically so one
/// recipient's Spanish date never leaks into another recipient's English text sharing the same
/// background-worker thread. A caller building notification text for a given recipient passes that
/// recipient's normalized language (as <see cref="NotificationService"/> hands it to the
/// <c>Func&lt;string, (string Title, string Body)&gt;</c> callback) to these methods instead of an
/// implicit <c>DateTime.ToString()</c>.
///
/// <para>English formats are UNCHANGED from the literal format strings this replaces (same pattern,
/// <see cref="CultureInfo.GetCultureInfo"/> for "en" renders identically to the invariant-culture-ish
/// ambient formatting the original interpolated strings relied on) — multilingual plan requirement that
/// English notification text stay byte-identical to before this phase.</para>
/// </summary>
public static class NotificationDateFormat
{
    /// <summary>
    /// A meeting's date/time, e.g. "October 15, 2026 at 3:00 PM" (en) or "15 de octubre de 2026, a las
    /// 3:00 p.m." (es). <paramref name="utc"/> is rendered as given — callers that need a timezone
    /// conversion must do it before calling (unchanged from the pre-existing behavior this replaces,
    /// which also rendered the raw UTC instant alongside a separate timezone-id label).
    /// </summary>
    public static string FormatMeetingDateTime(DateTime utc, string? language) =>
        SupportedLanguages.Normalize(language) == SupportedLanguages.Spanish
            ? utc.ToString("d 'de' MMMM 'de' yyyy, 'a las' h:mm tt", CultureInfo.GetCultureInfo(SupportedLanguages.Spanish))
            : utc.ToString("MMMM d, yyyy 'at' h:mm tt", CultureInfo.GetCultureInfo(SupportedLanguages.English));

    /// <summary>A date with no time component, e.g. "Oct 15, 2026" (en) or "15 de octubre de 2026" (es).</summary>
    public static string FormatDate(DateTime date, string? language) =>
        SupportedLanguages.Normalize(language) == SupportedLanguages.Spanish
            ? date.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo(SupportedLanguages.Spanish))
            : date.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo(SupportedLanguages.English));
}
