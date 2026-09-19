namespace IepAssistant.Domain.Entities;

/// <summary>
/// One turn in an <see cref="AdvocateThread"/>. <see cref="ContentMarkdown"/> is the parent's text
/// (≤ 2 000) or the assistant's answer with the <c>&lt;sources&gt;</c>/<c>&lt;suggest&gt;</c> blocks
/// already stripped (≤ 32 000). Citations, suggestions and the tool trace are persisted as JSON so a
/// reloaded thread renders exactly what streamed. Tool traces record names and sizes only — never
/// tool payloads, which carry child records.
/// </summary>
public class AdvocateMessage : BaseEntity
{
    public int AdvocateThreadId { get; set; }
    public AdvocateMessageRole Role { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public string? SuggestionsJson { get; set; }
    public string? ToolTraceJson { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }

    /// <summary>The tool budget or max tool rounds were hit, or the answer ran out of tokens.</summary>
    public bool Truncated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AdvocateThread Thread { get; set; } = null!;
}
