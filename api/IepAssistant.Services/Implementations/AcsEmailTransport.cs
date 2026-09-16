using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The only component that actually calls Azure Communication Email (pilot-gates plan, phase 1,
/// decision 2). Used exclusively by <c>OutboundEmailWorker</c>. In Development with no ACS connection
/// string configured, sending is intentionally faked (logged, not sent) so local/CI runs need no live
/// credential — matching the product's pre-existing behavior. Outside Development, an unconfigured
/// transport is itself a delivery failure (see <see cref="SendAsync"/>): the row still gets recorded
/// as Failed rather than silently "succeeding" without ever reaching a mailbox.
/// </summary>
public class AcsEmailTransport : IEmailTransport
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AcsEmailTransport> _logger;
    private readonly string? _connectionString;
    private readonly string _senderAddress;

    public AcsEmailTransport(IConfiguration configuration, IHostEnvironment environment, ILogger<AcsEmailTransport> logger)
    {
        _environment = environment;
        _logger = logger;
        _connectionString = configuration["Email:ConnectionString"];
        _senderAddress = configuration["Email:SenderAddress"] ?? "DoNotReply@mail.iep-advisor.com";
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_connectionString) || _environment.IsDevelopment();

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        IReadOnlyList<OutboundEmailAttachmentDraft>? attachments,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            if (_environment.IsDevelopment())
            {
                // Development mode — log instead of sending. This IS success.
                _logger.LogInformation("Email would be sent to {Email}: {Subject}", toEmail, subject);
                return;
            }

            // Outside Development, an unconfigured transport must not silently "succeed" — the caller
            // needs a Failed row it can see and retry once ACS is configured.
            throw new EmailDeliveryException(toEmail, subject,
                new InvalidOperationException("Email:ConnectionString is not configured."));
        }

        try
        {
            var client = new EmailClient(_connectionString);
            var message = new EmailMessage(
                senderAddress: _senderAddress,
                recipientAddress: toEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlBody,
                    PlainText = textBody
                });

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                    message.Attachments.Add(new EmailAttachment(attachment.Name, attachment.ContentType, new BinaryData(attachment.Content)));
            }

            var operation = await client.SendAsync(WaitUntil.Started, message, ct);
            _logger.LogInformation("Email sent to {Email}: {Subject} (OperationId: {OperationId})", toEmail, subject, operation.Id);
        }
        catch (Exception ex) when (ex is not EmailDeliveryException)
        {
            throw new EmailDeliveryException(toEmail, subject, ex);
        }
    }
}
