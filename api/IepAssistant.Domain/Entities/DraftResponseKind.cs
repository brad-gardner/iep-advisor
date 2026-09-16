namespace IepAssistant.Domain.Entities;

/// <summary>Kind of a family <see cref="DraftResponse"/> to a <see cref="SharedDraftRevision"/> item (plan 6, decision 4). Stored as a string.</summary>
public enum DraftResponseKind
{
    Agree,
    Question,
    ChangeRequest,
    Comment
}
