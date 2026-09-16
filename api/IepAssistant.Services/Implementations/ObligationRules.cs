using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pure procedural-deadline math (plan 4, decision 2) — versioned by these constants, never hardcoded in
/// the UI or duplicated per caller. Profile is currently informational: both the <c>OH</c> and
/// <c>Default</c> profiles use the same fallback formulas today (annual review = IEP date + 365 days;
/// re-evaluation = ETR date + 3 years), matching the plan-4 contract; a future state-specific rule set
/// only needs to change <see cref="ResolveAnnualReviewDue"/>/<see cref="ResolveReevaluationDue"/>.
/// </summary>
public static class ObligationRules
{
    public const string OhProfile = "OH";
    public const string DefaultProfile = "Default";

    /// <summary>DueSoon boundary: due within this many days (inclusive) of today.</summary>
    public const int DueSoonWindowDays = 30;

    public const int AnnualReviewFallbackDays = 365;
    public const int ReevaluationFallbackYears = 3;

    public static string ResolveProfile(string? stateCode) =>
        string.Equals(stateCode?.Trim(), "OH", StringComparison.OrdinalIgnoreCase) ? OhProfile : DefaultProfile;

    public static (DateTime? DueDate, string SourceLabel) ResolveAnnualReviewDue(DateTime? annualReviewDueDate, DateTime? iepDate)
    {
        if (annualReviewDueDate.HasValue)
            return (annualReviewDueDate.Value.Date, "Annual review due date on file");
        if (iepDate.HasValue)
            return (iepDate.Value.Date.AddDays(AnnualReviewFallbackDays), "IEP date + 365 days");
        return (null, "No IEP date or annual review due date on file");
    }

    public static (DateTime? DueDate, string SourceLabel) ResolveReevaluationDue(DateTime? reevaluationDueDate, DateTime? etrDate)
    {
        if (reevaluationDueDate.HasValue)
            return (reevaluationDueDate.Value.Date, "Re-evaluation due date on file");
        if (etrDate.HasValue)
            return (etrDate.Value.Date.AddYears(ReevaluationFallbackYears), "ETR date + 3 years");
        return (null, "No ETR date or re-evaluation due date on file");
    }

    /// <summary>True when the student has an IEP but no ETR date on file — a distinct "ETR needs
    /// establishing" gap from the (possibly also Unknown) reevaluation obligation.</summary>
    public static bool NeedsEtrDue(DateTime? iepDate, DateTime? etrDate) => iepDate.HasValue && !etrDate.HasValue;

    public static ObligationStatus ResolveStatus(DateTime? dueDate, DateTime today)
    {
        if (dueDate == null)
            return ObligationStatus.Unknown;
        var due = dueDate.Value.Date;
        var todayDate = today.Date;
        if (due < todayDate)
            return ObligationStatus.Overdue;
        if (due <= todayDate.AddDays(DueSoonWindowDays))
            return ObligationStatus.DueSoon;
        return ObligationStatus.Upcoming;
    }

    public static int? DaysUntilDue(DateTime? dueDate, DateTime today) =>
        dueDate.HasValue ? (int)(dueDate.Value.Date - today.Date).TotalDays : null;
}
