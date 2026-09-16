namespace IepAssistant.Domain.Entities;

/// <summary>
/// A procedural deadline kind, computed (never persisted) by <c>ObligationService</c>/<c>ObligationRules</c>
/// from <see cref="SchoolStudent"/> dates (plan 4, decision 2), or — for the plan 7 additions below —
/// from <see cref="GoalRecord"/>/<see cref="EvaluationCase"/> data.
/// </summary>
public enum ObligationKind
{
    AnnualReview,
    Reevaluation,
    EtrDue,
    /// <summary>Plan 7: an Active <see cref="GoalRecord"/> with no <see cref="GoalObservation"/> in 45 days.</summary>
    GoalObservationStale,
    /// <summary>Plan 7: an open <see cref="EvaluationCase"/>'s determination clock.</summary>
    EvaluationDetermination,
    /// <summary>Plan 7: an overdue <see cref="EvaluatorAssignment"/> (not yet submitted, past its due date).</summary>
    EvaluatorSubmission
}
