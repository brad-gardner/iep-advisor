using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// The one place an email is actually sent (pilot-gates plan, phase 1, decision 2). Used exclusively by
/// <c>OutboundEmailWorker</c>. The Development (no ACS connection string) path logs "would be sent" and
/// returns normally — that is success, matching the product's existing local/CI behavior. A real send
/// failure throws <see cref="Models.EmailDeliveryException"/> so the worker can record it.
/// </summary>
public interface IEmailTransport
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        IReadOnlyList<OutboundEmailAttachmentDraft>? attachments,
        CancellationToken ct = default);

    /// <summary>True when a real ACS connection string is configured (or the environment is
    /// Development, where sending is intentionally faked). Backs the startup warning and
    /// <c>GET /api/admin/email/status</c>.</summary>
    bool IsConfigured { get; }
}
