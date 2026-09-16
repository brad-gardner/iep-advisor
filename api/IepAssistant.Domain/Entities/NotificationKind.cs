namespace IepAssistant.Domain.Entities;

/// <summary>Kind of <see cref="Notification"/> (plan 4, decision 3). Stored as a string.</summary>
public enum NotificationKind
{
    MeetingScheduled,
    MeetingUpdated,
    MeetingCancelled,
    MeetingReminder,
    ObligationDigest,
    DraftShared,
    ResponseReceived,
    Generic
}
