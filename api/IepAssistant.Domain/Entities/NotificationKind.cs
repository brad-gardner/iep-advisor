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
    /// <summary>Plan 7: a weekly-digest nudge that an Active goal has had no observation in 45 days.</summary>
    GoalObservationRequested,
    /// <summary>Plan 7: an <see cref="EvaluatorAssignment"/> is overdue — sent to the evaluator + case lead.</summary>
    EvaluatorOverdue,
    /// <summary>Plan 7: a requested <see cref="ExportJob"/> finished (Completed or Failed) — sent to the requester.</summary>
    ExportReady,

    /// <summary>Pilot-gates plan, phase 1: the nightly (or on-demand) audit hash-chain walk found a
    /// tampered/mismatched row. Sent to every platform admin.</summary>
    AuditIntegrityBroken,
    Generic
}
