namespace IepAssistant.Services.Models;

/// <summary>
/// Why a <see cref="ServiceResult"/>/<see cref="ServiceResult{T}"/> failed, independent of the
/// (possibly localized) <see cref="ServiceResult.Message"/> text. Multilingual plan (2026-10-06)
/// phase 2 review fix P2-A: HTTP status must not depend on substring-matching a message that can be
/// Spanish — a controller's failure mapper should switch on this instead. Default <see cref="None"/>
/// preserves the pre-existing English-substring-heuristic behavior for services not yet converted.
///
/// <para><b>Any service localized in a later phase must set <see cref="ServiceResult.ErrorKind"/> for
/// failures whose controller maps status</b> (i.e. whenever the controller's failure path inspects
/// <c>result.Message</c> to pick 403/404/409/etc. — not every <c>FailureResult</c> needs a non-None
/// kind, only ones a status-mapping controller will see).</para>
/// </summary>
public enum ServiceErrorKind
{
    /// <summary>No specific kind set. Controllers fall back to the English-substring heuristic.</summary>
    None,
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Unavailable,

    /// <summary>402 — an active subscription (or similar paid entitlement) is required. Added in the
    /// multilingual plan's phase 3 for <c>AnalysisRunService</c>'s "Active subscription required."
    /// failure, which its controller previously detected by matching the English word "subscription".</summary>
    PaymentRequired,

    /// <summary>422 — the request is well-formed but cannot be processed as-is (e.g. no Published
    /// document template exists yet to pin). Added in the multilingual plan's phase 5 for
    /// <c>TemplateResolutionService</c>'s "no document template" failure, which
    /// <c>DocumentInstanceController</c> previously detected by matching the English phrase
    /// "no document template".</summary>
    Unprocessable
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>See <see cref="ServiceErrorKind"/>. Defaults to <see cref="ServiceErrorKind.None"/> for
    /// every existing success/failure factory below, so unconverted call sites are unaffected.</summary>
    public ServiceErrorKind ErrorKind { get; init; } = ServiceErrorKind.None;

    public static ServiceResult SuccessResult(string? message = null)
        => new() { Success = true, Message = message };

    public static ServiceResult FailureResult(string message)
        => new() { Success = false, Message = message };

    public static ServiceResult FailureResult(List<string> errors)
        => new() { Success = false, Errors = errors };

    public static ServiceResult FailureResult(ServiceErrorKind kind, string message)
        => new() { Success = false, Message = message, ErrorKind = kind };

    public static ServiceResult NotFound(string message) => FailureResult(ServiceErrorKind.NotFound, message);
    public static ServiceResult Forbidden(string message) => FailureResult(ServiceErrorKind.Forbidden, message);
    public static ServiceResult Conflict(string message) => FailureResult(ServiceErrorKind.Conflict, message);
    public static ServiceResult PaymentRequired(string message) => FailureResult(ServiceErrorKind.PaymentRequired, message);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public new static ServiceResult<T> FailureResult(string message)
        => new() { Success = false, Message = message };

    public new static ServiceResult<T> FailureResult(List<string> errors)
        => new() { Success = false, Errors = errors };

    public new static ServiceResult<T> FailureResult(ServiceErrorKind kind, string message)
        => new() { Success = false, Message = message, ErrorKind = kind };

    public new static ServiceResult<T> NotFound(string message) => FailureResult(ServiceErrorKind.NotFound, message);
    public new static ServiceResult<T> Forbidden(string message) => FailureResult(ServiceErrorKind.Forbidden, message);
    public new static ServiceResult<T> Conflict(string message) => FailureResult(ServiceErrorKind.Conflict, message);
    public new static ServiceResult<T> PaymentRequired(string message) => FailureResult(ServiceErrorKind.PaymentRequired, message);
}
