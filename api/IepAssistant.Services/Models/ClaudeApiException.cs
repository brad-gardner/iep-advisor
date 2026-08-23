namespace IepAssistant.Services.Models;

/// <summary>
/// Canned, per-kind user-facing text. These strings are persisted into parent-visible
/// <c>ErrorMessage</c> columns and rendered in the UI, so they must never contain anything derived
/// from an API response body: Anthropic error payloads carry the model id and a request id, and an
/// auth failure can echo key material.
/// </summary>
public static class ClaudeFailureMessages
{
    public const string Configuration = "Analysis is temporarily unavailable due to a service configuration problem.";
    public const string RateLimited = "The analysis service is busy right now. Please try again in a few minutes.";
    public const string Timeout = "The analysis took too long to complete. Please try again.";
    public const string Transient = "The analysis service is temporarily unavailable. Please try again.";
    public const string RequestTooLarge = "This document set is too large to analyze at once. Try selecting fewer documents.";
    public const string InvalidResponse = "The analysis could not be completed. Please try again.";
    public const string Unknown = "An unexpected error occurred during analysis.";

    /// <summary>Returns the canned message for <paramref name="kind"/>.</summary>
    public static string For(ClaudeFailureKind kind) => kind switch
    {
        ClaudeFailureKind.Configuration => Configuration,
        ClaudeFailureKind.RateLimited => RateLimited,
        ClaudeFailureKind.Timeout => Timeout,
        ClaudeFailureKind.Transient => Transient,
        ClaudeFailureKind.RequestTooLarge => RequestTooLarge,
        ClaudeFailureKind.InvalidResponse => InvalidResponse,
        _ => Unknown
    };
}

/// <summary>
/// A classified Claude API failure. Callers catch this ahead of their broad <c>catch (Exception)</c>
/// so a dependency outage is distinguishable from a genuine bug.
/// </summary>
public sealed class ClaudeApiException : Exception
{
    public ClaudeFailureKind Kind { get; }

    /// <summary>
    /// Canned per-kind text that is safe to persist and show to end users. Never derived from the
    /// inner exception — API bodies carry model ids, request ids, and potentially key material.
    /// </summary>
    public string UserMessage { get; }

    public ClaudeApiException(ClaudeFailureKind kind, Exception? inner = null)
        : this(kind, ClaudeFailureMessages.For(kind), inner)
    {
    }

    // Private by design. Only the kind-based constructor above is reachable, which makes
    // "UserMessage is never derived from inner.Message" a compile-time guarantee rather than a
    // convention a future caller can quietly break with new ClaudeApiException(kind, ex.Message).
    private ClaudeApiException(ClaudeFailureKind kind, string userMessage, Exception? inner = null)
        : base($"Claude call failed ({kind})", inner)
    {
        Kind = kind;
        UserMessage = userMessage;
    }
}
