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
    /// <summary>Plan 6: staff resolved a family's <see cref="DraftResponse"/> — notifies the parent who submitted it.</summary>
    DraftResponseResolved,
    /// <summary>Plan 6: a post-meeting family summary was sent.</summary>
    MeetingSummarySent,
    Generic
}
