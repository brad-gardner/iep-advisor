namespace IepAssistant.Services.Models;

/// <summary>
/// The kind of inline AI assist an educator requested for a single draft field/entity.
/// </summary>
public enum AssistKind
{
    /// <summary>Rewrite the content to be clearer and measurable.</summary>
    Rewrite,
    /// <summary>Critique the content and suggest improvements / what's weak.</summary>
    Improve,
    /// <summary>Propose a concrete measurement method + target criteria (or, for non-goal
    /// targets, make the content more specific/objective).</summary>
    SuggestMeasurement
}

/// <summary>Result of a single-field assist call — the suggestion text (never auto-applied), plus,
/// for evidence-grounded document assist, the model's rationale and the evidence it cited.</summary>
public sealed class AssistResultModel
{
    public required string Suggestion { get; init; }
    public string? Rationale { get; init; }
    public IReadOnlyList<AssistCitation> Citations { get; init; } = Array.Empty<AssistCitation>();
    /// <summary>True when the target is a goal with no baseline and the evidence holds none either —
    /// the UI asks for data instead of letting a number be invented.</summary>
    public bool MissingBaseline { get; init; }
}

public sealed class AssistCitation
{
    public required string EvidenceId { get; init; }
    public required string SourceLabel { get; init; }
    public required string Excerpt { get; init; }
}

/// <summary>A single ephemeral chat turn. The client owns the thread and resends it each call.</summary>
public sealed class ChatMessage
{
    /// <summary>"user" or "assistant".</summary>
    public required string Role { get; init; }
    public required string Content { get; init; }
}

/// <summary>Reply from an IEP-scoped chat turn.</summary>
public sealed class ChatReplyModel
{
    public required string Reply { get; init; }
}
