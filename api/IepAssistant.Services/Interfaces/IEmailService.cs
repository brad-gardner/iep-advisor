using IepAssistant.Services.Models;

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

    // ----------------------------------------------------------------- Plan 4 additions
    //
    // Unlike every method above (which swallows an ACS send failure and logs it — see EmailService's
    // private SendEmailAsync), these THROW on failure. The callers are background workers/services that
    // must record the failure on a Notification row rather than have it silently disappear. The dev-mode
    // (no ACS connection string configured) path still logs-and-returns success, matching the existing
    // methods' local/CI behavior.

    /// <summary>New meeting invitation. <paramref name="ics"/> is attached as a .ics file (METHOD:REQUEST).</summary>
    Task SendMeetingInvitationAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default);

    /// <summary>Meeting reschedule/detail change. <paramref name="ics"/> carries the bumped SEQUENCE.</summary>
    Task SendMeetingUpdatedAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default);

    /// <summary>Meeting cancellation. <paramref name="ics"/> is a METHOD:CANCEL calendar object.</summary>
    Task SendMeetingCancelledAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default);

    /// <summary>Generic in-app notification email (reminders, digests-as-fallback, drafts shared, etc.).</summary>
    Task SendNotificationAsync(string toEmail, string title, string body, string linkUrl, CancellationToken ct = default);

    /// <summary>Daily obligations + upcoming-meetings digest.</summary>
    Task SendDigestAsync(string toEmail, DigestEmailModel model, CancellationToken ct = default);
}
