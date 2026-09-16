namespace IepAssistant.Domain.Entities;

/// <summary>How a <see cref="FamilyContactAttempt"/> was made (plan 7, decision 7). Stored as a string.</summary>
public enum FamilyContactMethod
{
    Email,
    Phone,
    Letter,
    InPerson,
    Portal
}
