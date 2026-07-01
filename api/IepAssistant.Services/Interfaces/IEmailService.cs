namespace IepAssistant.Services.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default);
    Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default);
    Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default);
    Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default);
    Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default);
    Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default);
    Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default);
}
