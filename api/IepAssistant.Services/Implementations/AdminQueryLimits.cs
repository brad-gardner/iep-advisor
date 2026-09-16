namespace IepAssistant.Services.Implementations;

/// <summary>
/// Sane bounds for admin-facing date-range/day-window query parameters (compliance board, adoption,
/// roster "due in range" drilldown — review-fix contract, todos/086 P3 #2/#5): an out-of-range value
/// fails cleanly (a <see cref="Models.ServiceResult"/> failure, mapped to 400 by the controllers) instead
/// of overflowing <see cref="DateTime"/> arithmetic (e.g. <c>AddDays</c> on a huge offset) into an
/// unhandled exception.
/// </summary>
internal static class AdminQueryLimits
{
    private const int MaxYearsSpan = 10;

    /// <summary>Upper bound for a caller-supplied day-window (e.g. adoption's <c>days</c>) — 10 years.</summary>
    public const int MaxDays = 3650;

    /// <summary>True when <paramref name="date"/> is null or within ±10 years of <paramref name="today"/>.</summary>
    public static bool IsWithinRange(DateTime? date, DateTime today)
        => date == null || (date.Value >= today.AddYears(-MaxYearsSpan) && date.Value <= today.AddYears(MaxYearsSpan));

    /// <summary>Clamps a day-count window to [1, <see cref="MaxDays"/>], defaulting non-positive input to <paramref name="defaultDays"/>.</summary>
    public static int ClampDays(int days, int defaultDays) => Math.Clamp(days <= 0 ? defaultDays : days, 1, MaxDays);
}
