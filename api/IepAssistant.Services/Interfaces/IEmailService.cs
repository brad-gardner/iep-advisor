namespace IepAssistant.Services.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default);
    Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default);
}
