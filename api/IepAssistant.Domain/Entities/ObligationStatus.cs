namespace IepAssistant.Domain.Entities;

/// <summary>Computed urgency bucket for an <c>ObligationModel</c> (plan 4, decision 2).</summary>
public enum ObligationStatus
{
    Upcoming,
    DueSoon,
    Overdue,
    Unknown
}
