namespace IepAssistant.Domain.Entities;

/// <summary>
/// Lifecycle of a <see cref="DocumentInstance"/> (State Document Template Engine, Phase 3). Mirrors
/// <see cref="IepDraftStatus"/>: an educator authors values while <see cref="Draft"/>; finalize (Phase 4)
/// briefly moves it to <see cref="Finalizing"/> (edits frozen) before it becomes <see cref="Finalized"/>.
/// Serialized as a string (JsonStringEnumConverter) and stored as a string column via HasConversion&lt;string&gt;().
/// </summary>
public enum DocumentInstanceStatus
{
    Draft = 0,
    Finalizing = 1,
    Finalized = 2
}
