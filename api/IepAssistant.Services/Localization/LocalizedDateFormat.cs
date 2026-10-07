using System.Globalization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// The one place that formats a date or date/time for display in a recipient's language — shared by
/// <c>EmailService</c> (email bodies/subjects) and every Notification title/body builder (e.g.
/// <c>MeetingService</c>, <c>EvaluationCaseService</c>), so the same instant never renders two different
/// ways for the same recipient depending on which surface (email vs. in-app/bell) produced it. Replaces
/// the separate, independently-drifted copies that used to live as private statics on <c>EmailService</c>
/// and as the old <c>NotificationDateFormat</c> class.
///
/// <para><see cref="Localization.CultureScope"/> deliberately touches only
/// <see cref="CultureInfo.CurrentUICulture"/> and leaves <see cref="CultureInfo.CurrentCulture"/>
/// (date/number formatting) exactly as it found it, so a Spanish recipient's date must never leak into
/// unrelated work sharing the same background-worker thread. Every method here therefore takes an
/// explicit <paramref name="language"/> ("en"/"es", case-insensitive; null/unsupported falls back to
/// English) and formats against <see cref="CultureInfo.GetCultureInfo"/> for that language directly,
/// rather than reading any ambient culture.</para>
///
/// <para>English formats are unchanged from the literal format strings this replaces (verified
/// byte-identical to the prior per-caller formatting) — the multilingual plan requires English text to
/// stay exactly as it was before this phase.</para>
/// </summary>
public static class LocalizedDateFormat
{
    /// <summary>A long date with no time, e.g. "October 15, 2026" (en) or "15 de octubre de 2026" (es).</summary>
    public static string LongDate(DateTime value, string? language)
    {
        var culture = CultureFor(language, out var isSpanish);
        return isSpanish
            ? value.ToString("d 'de' MMMM 'de' yyyy", culture)
            : value.ToString("MMMM d, yyyy", culture);
    }

    /// <summary>A short date with no time, e.g. "Oct 15, 2026" (en) or "15 de oct de 2026" (es) — the one
    /// Spanish short-date pattern used everywhere a compact date (with year) is shown, e.g. the digest's
    /// obligation due-date suffix.</summary>
    public static string ShortDate(DateTime value, string? language)
    {
        var culture = CultureFor(language, out var isSpanish);
        return isSpanish
            ? value.ToString("d 'de' MMM 'de' yyyy", culture)
            : value.ToString("MMM d, yyyy", culture);
    }

    /// <summary>A short date (no year) plus time, e.g. "Oct 15, 3:00 PM" (en) or "15 de oct, 3:00 p. m."
    /// (es) — a compact list-row rendering (the digest's upcoming-meetings line). For the full meeting
    /// invitation/notification date-time, use <see cref="MeetingDateTime"/> instead.</summary>
    public static string ShortDateTime(DateTime value, string? language)
    {
        var culture = CultureFor(language, out var isSpanish);
        return isSpanish
            ? value.ToString("d 'de' MMM, h:mm tt", culture)
            : value.ToString("MMM d, h:mm tt", culture);
    }

    /// <summary>
    /// A meeting's full date and time, e.g. "October 15, 2026 at 3:00 PM" (en) or "15 de octubre de 2026 a
    /// la 1:00 p. m." / "15 de octubre de 2026 a las 3:00 p. m." (es — "a la" only immediately before a
    /// 1-o'clock hour, "a las" for every other hour). The ONE rendering shared by the meeting
    /// invitation/update/cancellation email (<c>EmailService</c>) and the matching bell/email notification
    /// text (<c>MeetingService</c>), so the two can never disagree or drift apart.
    /// </summary>
    /// <param name="value">Rendered as given — a caller that needs a timezone conversion, or that wants to
    /// show the zone id alongside, must do that itself; this only formats the instant.</param>
    public static string MeetingDateTime(DateTime value, string? language)
    {
        var culture = CultureFor(language, out var isSpanish);
        if (!isSpanish)
            return value.ToString("MMMM d, yyyy 'at' h:mm tt", culture);

        var datePart = value.ToString("d 'de' MMMM 'de' yyyy", culture);
        var timePart = value.ToString("h:mm tt", culture);
        var hour12 = value.Hour % 12 == 0 ? 12 : value.Hour % 12;
        var connector = hour12 == 1 ? "a la" : "a las";
        return $"{datePart} {connector} {timePart}";
    }

    private static CultureInfo CultureFor(string? language, out bool isSpanish)
    {
        isSpanish = SupportedLanguages.Normalize(language) == SupportedLanguages.Spanish;
        return CultureInfo.GetCultureInfo(isSpanish ? SupportedLanguages.Spanish : SupportedLanguages.English);
    }
}
