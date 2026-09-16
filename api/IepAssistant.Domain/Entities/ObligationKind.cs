namespace IepAssistant.Domain.Entities;

/// <summary>
/// A procedural deadline kind, computed (never persisted) by <c>ObligationService</c>/<c>ObligationRules</c>
/// from <see cref="SchoolStudent"/> dates (plan 4, decision 2).
/// </summary>
public enum ObligationKind
{
    AnnualReview,
    Reevaluation,
    EtrDue
}
