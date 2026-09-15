namespace IepAssistant.Domain.Entities;

/// <summary>Why a student left the roster (set with <see cref="StudentStatus.Exited"/>). Stored as a string.</summary>
public enum ExitReason
{
    Graduated,
    Transferred,
    Withdrawn,
    Declassified,
    Other
}
