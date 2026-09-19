using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class AdvocateThreadModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}

public class AdvocateThreadDetailModel : AdvocateThreadModel
{
    /// <summary>Oldest first.</summary>
    public List<AdvocateMessageModel> Messages { get; set; } = new();
}

public class AdvocateMessageModel
{
    public int Id { get; set; }
    public AdvocateMessageRole Role { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public List<AdvocateCitation> Citations { get; set; } = new();
    public List<AdvocateSuggestion> Suggestions { get; set; } = new();
    public bool Truncated { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// A record the assistant read this turn and named in its <c>&lt;sources&gt;</c> block. <see cref="Kind"/> is
/// the sourceRef kind (<c>kb</c>, <c>child</c>, and from Phase 3 <c>goal</c>, <c>iep</c>, <c>journal</c>…);
/// <see cref="Label"/> is the human title the toolset recorded for it, when it has one.
/// </summary>
public sealed record AdvocateCitation(string Kind, int Id, string? Label);

/// <summary>
/// A handoff the assistant proposed via <c>&lt;suggest&gt;</c>. <see cref="Kind"/> is one of
/// <see cref="AdvocateSuggestionKinds"/>. Text kinds (<c>prep_question</c>, <c>journal_entry</c>) carry
/// <see cref="Text"/> (and optionally <see cref="Date"/>, <c>yyyy-MM-dd</c>); <c>open_*</c> kinds carry the
/// <see cref="Id"/> of a record that appeared in this turn's tool results. No API write ever happens from a
/// suggestion — the UI turns it into a prefilled navigation.
/// </summary>
public sealed record AdvocateSuggestion(string Kind, string? Text, int? Id, string? Date);

public static class AdvocateSuggestionKinds
{
    public const string PrepQuestion = "prep_question";
    public const string JournalEntry = "journal_entry";
    public const string OpenKnowledgeBase = "open_kb";
    public const string OpenGoal = "open_goal";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PrepQuestion, JournalEntry, OpenKnowledgeBase, OpenGoal
    };

    /// <summary>For an <c>open_*</c> kind, the sourceRef kind its id must have appeared under this turn.</summary>
    public static string? SourceKindFor(string suggestionKind) => suggestionKind switch
    {
        OpenKnowledgeBase => "kb",
        OpenGoal => "goal",
        _ => null
    };
}

public class AdvocateUsageModel
{
    public int Used { get; set; }
    public int Limit { get; set; }
    public bool SubscriptionActive { get; set; }
}

public enum AdvocateStreamEventKind
{
    Delta,
    Tool,
    Done,
    Error
}

/// <summary>Stable error codes carried by <see cref="AdvocateStreamEventKind.Error"/> events.</summary>
public static class AdvocateErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string Forbidden = "forbidden";
    public const string UsageCap = "usage_cap";
    public const string Unavailable = "unavailable";
}

/// <summary>
/// One event from <see cref="Interfaces.IAdvocateService.SendMessageAsync"/>, discriminated by <see cref="Kind"/>.
/// Only the fields documented for a kind are populated. Pre-check failures (validation, not found, forbidden,
/// usage cap) are always the FIRST and only event, so a transport can map them to a plain status code before
/// switching to a stream; <see cref="AdvocateErrorCodes.Unavailable"/> can arrive after deltas.
/// </summary>
public sealed class AdvocateStreamEvent
{
    public AdvocateStreamEventKind Kind { get; init; }

    // Delta
    public string? Text { get; init; }

    // Tool
    public string? ToolName { get; init; }
    public string? ToolLabel { get; init; }
    /// <summary>"started" | "finished" | "failed"</summary>
    public string? ToolStatus { get; init; }

    // Error
    public string? Code { get; init; }
    public string? Message { get; init; }

    // Done
    public int? MessageId { get; init; }
    public string? ContentMarkdown { get; init; }
    public List<AdvocateCitation>? Citations { get; init; }
    public List<AdvocateSuggestion>? Suggestions { get; init; }
    public bool Truncated { get; init; }
    public string? Disclaimer { get; init; }

    public static AdvocateStreamEvent Delta(string text) => new() { Kind = AdvocateStreamEventKind.Delta, Text = text };

    public static AdvocateStreamEvent Tool(string name, string label, string status) =>
        new() { Kind = AdvocateStreamEventKind.Tool, ToolName = name, ToolLabel = label, ToolStatus = status };

    public static AdvocateStreamEvent Error(string code, string message) =>
        new() { Kind = AdvocateStreamEventKind.Error, Code = code, Message = message };

    public static AdvocateStreamEvent Done(int messageId, string contentMarkdown, List<AdvocateCitation> citations, List<AdvocateSuggestion> suggestions, bool truncated, string disclaimer) =>
        new()
        {
            Kind = AdvocateStreamEventKind.Done,
            MessageId = messageId,
            ContentMarkdown = contentMarkdown,
            Citations = citations,
            Suggestions = suggestions,
            Truncated = truncated,
            Disclaimer = disclaimer
        };
}
