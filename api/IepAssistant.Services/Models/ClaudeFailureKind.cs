namespace IepAssistant.Services.Models;

/// <summary>
/// Classification of a failed Claude call. Deliberately a code enum rather than a database lookup
/// table: it exists only to drive <c>switch</c> branches and select a canned user-facing message,
/// so runtime editability buys nothing while a DB round-trip on every failure costs something.
/// </summary>
public enum ClaudeFailureKind
{
    /// <summary>401/403/404, invalid request, or a missing/blank API key — retrying will not help.</summary>
    Configuration,

    /// <summary>429 — the caller is over the API rate limit.</summary>
    RateLimited,

    /// <summary>The HttpClient timeout elapsed. Never raised for host-shutdown cancellation.</summary>
    Timeout,

    /// <summary>5xx or an overloaded upstream — worth retrying.</summary>
    Transient,

    /// <summary>413 or a context-window overflow — the input itself must shrink.</summary>
    RequestTooLarge,

    /// <summary>A 200 that carried no usable text block, or a body that could not be parsed.</summary>
    InvalidResponse,

    /// <summary>Anything not otherwise classified.</summary>
    Unknown
}
