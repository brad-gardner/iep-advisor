namespace IepAssistant.Domain.Entities;

/// <summary>A <see cref="MeetingParticipant"/>'s RSVP state (plan 4, decision 1). Stored as a string.</summary>
public enum InviteStatus
{
    Pending,
    Accepted,
    Declined,
    Tentative
}
