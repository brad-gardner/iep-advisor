namespace IepAssistant.Services.Models;

/// <summary>One attachment on a queued email — the meeting .ics, currently the only user. Stored on the
/// <c>OutboundEmail</c> row as <c>AttachmentsJson</c> (name/contentType/base64).</summary>
public sealed record OutboundEmailAttachmentDraft(string Name, string ContentType, byte[] Content);

/// <summary>
/// A fully-composed email ready to enqueue. <see cref="IEmailService"/>'s Send* methods build one of
/// these (rendering HTML/text) and hand it to <see cref="Interfaces.IOutboundEmailQueue"/> instead of
/// sending it themselves (pilot-gates plan, phase 1, decision 2).
/// </summary>
public sealed class OutboundEmailDraft
{
    public required string ToEmail { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }

    /// <summary>Short machine label for admin-page triage (e.g. "PasswordReset", "MeetingInvitation").</summary>
    public required string Kind { get; init; }

    public IReadOnlyList<OutboundEmailAttachmentDraft>? Attachments { get; init; }
    public string? CorrelationId { get; init; }
}
