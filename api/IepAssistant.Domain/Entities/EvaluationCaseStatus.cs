namespace IepAssistant.Domain.Entities;

/// <summary>
/// Lifecycle status of an <see cref="EvaluationCase"/> (plan 7, decision 1):
/// Open → ConsentPending (consent requested) → InProgress (consent received) → Determined (eligible
/// outcome recorded) → Closed. A NotEligible/Withdrawn determination moves the case straight to Closed.
/// </summary>
public enum EvaluationCaseStatus
{
    Open,
    ConsentPending,
    InProgress,
    Determined,
    Closed
}
