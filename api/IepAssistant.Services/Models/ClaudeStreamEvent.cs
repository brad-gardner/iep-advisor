namespace IepAssistant.Services.Models;

public enum ClaudeStreamEventKind
{
    /// <summary>A fragment of visible answer text, in order. <see cref="ClaudeStreamEvent.Text"/> is set.</summary>
    TextDelta,

    /// <summary>The model asked for a tool and the executor is about to run it.</summary>
    ToolStarted,

    /// <summary>The tool returned (or signalled an error via <see cref="ToolExecutionException"/>).</summary>
    ToolFinished,

    /// <summary>The final event. Exactly one is emitted, always last, unless the stream throws.</summary>
    Completed,
}

/// <summary>
/// One event from <c>IClaudeClient.StreamWithToolsAsync</c>, discriminated by <see cref="Kind"/>.
/// Only the fields documented for a given kind are populated; the rest stay at their defaults.
/// </summary>
public sealed record ClaudeStreamEvent(
    ClaudeStreamEventKind Kind,
    string? Text = null,            // TextDelta
    string? ToolName = null,        // ToolStarted / ToolFinished
    string? ToolUseId = null,       // ToolStarted / ToolFinished
    bool ToolIsError = false,       // ToolFinished
    string? FullText = null,        // Completed: concatenated visible text of the final turn
    ClaudeToolTrace? Trace = null,  // Completed
    int? InputTokens = null,        // Completed: sum of input_tokens across every model turn
    int? OutputTokens = null,       // Completed: sum of output_tokens across every model turn
    bool Truncated = false);        // Completed: MaxToolRounds reached with tools pending, or max_tokens hit

/// <summary>
/// What happened during the tool loop, for persistence and diagnostics. <see cref="Rounds"/> is the
/// number of model turns streamed (1 for a text-only answer). Inputs and results are recorded by size
/// only, never by content — tool payloads carry child records.
/// </summary>
public sealed record ClaudeToolTrace(IReadOnlyList<ClaudeToolCallTrace> Calls, int Rounds);

public sealed record ClaudeToolCallTrace(
    string Name,
    string ToolUseId,
    int InputChars,
    int ResultChars,
    long DurationMs,
    bool IsError);
