namespace IepAssistant.Domain.Entities;

/// <summary>Lifecycle of a <see cref="SharedDraftRevision"/> (plan 6, decision 1). Stored as a string.</summary>
public enum SharedDraftStatus
{
    Active,
    Superseded,
    Withdrawn
}
