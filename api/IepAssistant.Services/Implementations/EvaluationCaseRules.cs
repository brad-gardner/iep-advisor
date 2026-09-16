namespace IepAssistant.Services.Implementations;

/// <summary>Pure evaluation-clock math (plan 7, decision 1) — versioned by this constant (mirrors <see cref="ObligationRules"/>).</summary>
public static class EvaluationCaseRules
{
    /// <summary>Default determination window from consent received (OH default; same fallback for every profile today).</summary>
    public const int EvaluationClockDays = 60;

    public static DateTime ComputeDeterminationDueDate(DateTime consentReceivedAt) =>
        consentReceivedAt.Date.AddDays(EvaluationClockDays);
}
