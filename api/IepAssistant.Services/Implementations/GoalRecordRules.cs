namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pure goal-progress-monitoring math (plan 7, decision 6) — versioned by these constants, never
/// hardcoded per caller (mirrors <see cref="ObligationRules"/>).
/// </summary>
public static class GoalRecordRules
{
    /// <summary>An Active goal with no observation in this many days is "stale" (obligation + DTO flag).</summary>
    public const int StaleAfterDays = 45;

    /// <summary>Trajectory keeps at most this many most-recent numeric observations.</summary>
    public const int TrajectoryMaxPoints = 12;

    /// <summary>Fewer numeric points than this and the trajectory is "insufficient data" rather than plotted.</summary>
    public const int MinNumericPointsForTrajectory = 2;

    /// <summary>True when <paramref name="referenceDate"/> (the last observation, or the goal's
    /// ProjectedAt when it has never been observed) is <see cref="StaleAfterDays"/> or more before
    /// <paramref name="today"/>.</summary>
    public static bool IsStale(DateTime referenceDate, DateTime today) =>
        (today.Date - referenceDate.Date).TotalDays >= StaleAfterDays;
}
