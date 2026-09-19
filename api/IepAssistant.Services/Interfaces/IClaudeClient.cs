using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IClaudeClient
{
    Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams one assistant answer, executing tools through <paramref name="tools"/> until the
    /// model stops, <see cref="ClaudeToolRequest.MaxToolRounds"/> is reached, or <c>max_tokens</c>
    /// is hit. Text arrives as <see cref="ClaudeStreamEventKind.TextDelta"/> events as it streams;
    /// the last event is always <see cref="ClaudeStreamEventKind.Completed"/>. API failures surface
    /// as <see cref="ClaudeApiException"/> with the same <see cref="ClaudeFailureKind"/>
    /// classification as <see cref="CompleteAsync"/>, thrown from the enumeration.
    /// </summary>
    /// <remarks>
    /// Declared with a default body so the many single-purpose test fakes of this interface that
    /// only script <see cref="CompleteAsync"/> keep compiling; <c>ClaudeClient</c> overrides it, and
    /// a fake for a tool-using service must override it too.
    /// </remarks>
    IAsyncEnumerable<ClaudeStreamEvent> StreamWithToolsAsync(
        ClaudeToolRequest request,
        IToolExecutor tools,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{GetType().Name} does not implement {nameof(StreamWithToolsAsync)}.");
}
