using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class MeetingDecisionModel
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
    public int RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public class CreateMeetingDecisionModel
{
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
}

public class UpdateMeetingDecisionModel
{
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
}

/// <summary>A decision surfaced as a candidate edit for a specific draft instance (plan 7, decision 3) —
/// from a meeting linked to the instance directly, or to the same student within 60 days. Never
/// auto-applied; a human reviews this list in the editor and marks each one applied once they've made the
/// corresponding edit themselves.</summary>
public class ProposedEditModel
{
    public int DecisionId { get; set; }
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}
