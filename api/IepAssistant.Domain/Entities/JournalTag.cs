namespace IepAssistant.Domain.Entities;

/// <summary>What kind of journal entry a parent recorded about their child (stored as string).</summary>
public enum JournalTag
{
    Incident = 0,
    Communication = 1,
    Medical = 2,
    Progress = 3,
    Other = 4
}
