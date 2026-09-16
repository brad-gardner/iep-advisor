namespace IepAssistant.Domain.Entities;

/// <summary>Determination outcome of an <see cref="EvaluationCase"/> (plan 7, decision 1).</summary>
public enum EligibilityOutcome
{
    Eligible,
    NotEligible,
    Withdrawn
}
