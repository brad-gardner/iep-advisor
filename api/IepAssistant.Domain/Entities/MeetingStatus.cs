namespace IepAssistant.Domain.Entities;

/// <summary>Lifecycle of a <see cref="Meeting"/> (plan 4, decision 1). Stored as a string.</summary>
public enum MeetingStatus
{
    Proposed,
    Scheduled,
    Held,
    Continued,
    Cancelled
}
