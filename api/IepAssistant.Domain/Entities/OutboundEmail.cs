namespace IepAssistant.Domain.Entities;

/// <summary>Lifecycle state of a queued outbound email (pilot-gates plan, phase 1, decision 2).</summary>
public enum OutboundEmailStatus
{
    Queued,
    Sending,
    Sent,
    Failed,
    Cancelled
}

/// <summary>
/// A composed email awaiting real delivery. <see cref="IEmailService"/>'s Send* methods render the
/// subject/body and enqueue a row here rather than calling the mail transport directly;
/// <c>OutboundEmailWorker</c> is the only component that actually sends (<c>IEmailTransport</c>), with
/// retry/backoff on failure. This is what makes a delivery failure visible and resendable by a platform
/// admin (<c>/admin/email</c>) instead of disappearing into a caught-and-logged exception.
/// </summary>
public class OutboundEmail : BaseEntity
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }

    /// <summary>Short machine label for the composing call site (e.g. "PasswordReset", "MeetingInvitation") —
    /// used for admin-page filtering/triage, never parsed.</summary>
    public string Kind { get; set; } = string.Empty;

    public OutboundEmailStatus Status { get; set; } = OutboundEmailStatus.Queued;
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    /// <summary>When this row becomes eligible for the next send attempt. Set to "now" on enqueue so it
    /// is immediately eligible; bumped by <c>OutboundEmailWorker</c>'s backoff schedule after a failure.</summary>
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    /// <summary>Opaque caller-supplied correlation id (e.g. a meeting or notification id) for support triage.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>JSON array of {name, contentType, base64} — e.g. the meeting .ics attachment. Null when
    /// the email carries no attachment.</summary>
    public string? AttachmentsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
