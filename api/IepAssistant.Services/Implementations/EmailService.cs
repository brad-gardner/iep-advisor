using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _frontendUrl;
    private readonly string? _connectionString;
    private readonly string _senderAddress;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _frontendUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:5173";
        _connectionString = _configuration["Email:ConnectionString"];
        _senderAddress = _configuration["Email:SenderAddress"] ?? "noreply@mail.iep-advisor.com";
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default)
    {
        var resetUrl = $"{_frontendUrl}/reset-password?token={resetToken}";

        var subject = "Reset Your IEP Advisor Password";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">Reset Your Password</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    We received a request to reset your password. Click the button below to choose a new one. This link expires in 15 minutes.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{resetUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Reset Password
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    If you didn't request this, you can safely ignore this email. Your password won't change.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"Reset your IEP Advisor password by visiting: {resetUrl}\n\nThis link expires in 15 minutes. If you didn't request this, ignore this email.";

        await SendEmailAsync(toEmail, subject, html, plainText, ct);
    }

    public async Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default)
    {
        var inviteUrl = $"{_frontendUrl}/accept-invite?token={inviteToken}";
        var roleDisplay = role == "Collaborator" ? "collaborate on" : "view";

        var subject = $"{inviterName} invited you to IEP Advisor";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">You've Been Invited</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    <strong>{inviterName}</strong> has invited you to {roleDisplay} {childName}'s IEP information on IEP Advisor.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    IEP Advisor helps parents understand and advocate for their child's Individualized Education Program.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{inviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Accept Invitation
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    This invitation expires in 7 days. If you don't have an IEP Advisor account, you'll be asked to create one.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"{inviterName} has invited you to {roleDisplay} {childName}'s IEP information on IEP Advisor.\n\nAccept the invitation: {inviteUrl}\n\nThis invitation expires in 7 days.";

        await SendEmailAsync(toEmail, subject, html, plainText, ct);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string plainTextContent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            // Development mode — log instead of sending
            _logger.LogInformation("Email would be sent to {Email}: {Subject}", toEmail, subject);
            _logger.LogDebug("Email HTML content length: {Length} chars", htmlContent.Length);
            return;
        }

        try
        {
            var client = new EmailClient(_connectionString);

            var emailMessage = new EmailMessage(
                senderAddress: _senderAddress,
                recipientAddress: toEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlContent,
                    PlainText = plainTextContent
                });

            var operation = await client.SendAsync(Azure.WaitUntil.Started, emailMessage, ct);

            _logger.LogInformation("Email sent to {Email}: {Subject} (OperationId: {OperationId})",
                toEmail, subject, operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
            // Don't throw — email failure should not break the calling flow
        }
    }
}
