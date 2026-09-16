namespace IepAssistant.Domain.Entities;

/// <summary>Result of a <see cref="FamilyContactAttempt"/> (plan 7, decision 7). Stored as a string.</summary>
public enum FamilyContactOutcome
{
    Reached,
    NoAnswer,
    LeftMessage,
    Declined,
    Returned
}
