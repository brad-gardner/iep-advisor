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

    /// <summary>Pilot-gates plan, phase 2: sent once, at deletion-request time, carrying the signed
    /// <c>cancelUrl</c> that is the only way to cancel once the request has deactivated the account.</summary>
    Task SendAccountDeletionCancelLinkEmailAsync(string toEmail, string firstName, string cancelUrl, DateTime purgeDate, CancellationToken ct = default);

    // ----------------------------------------------------------------- Plan 4 additions
    //
    // Historically these methods threw on an ACS send failure while the ones above swallowed it. As of
    // the pilot-gates plan (phase 1, decision 2), EVERY Send* method — these included — only composes
    // and enqueues (see EmailService.EnqueueEmailAsync); none of them talk to ACS any more. The real
    // send, retry, and failure recording now happen entirely in OutboundEmailWorker/IEmailTransport.

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

    /// <summary>Pilot-gates plan, phase 3 (C11 adoption slice): the 15-minute magic sign-in link for a
    /// staff member eligible for magic-link sign-in.</summary>
    Task SendMagicLinkEmailAsync(string toEmail, string firstName, string magicLinkUrl, CancellationToken ct = default);
}
