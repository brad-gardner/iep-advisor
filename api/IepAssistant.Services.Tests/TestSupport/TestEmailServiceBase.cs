using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Tests.TestSupport;

/// <summary>
/// No-op base for the several per-test-class <c>CapturingEmailService</c> doubles (one per suite already
/// captures the one method it cares about). Every <see cref="IEmailService"/> member is virtual so a
/// subclass overrides only the method(s) it needs to capture, instead of repeating the other ten as
/// pass-through <c>Task.CompletedTask</c> stubs. Added alongside plan 4's five new throwing methods so
/// every existing double keeps compiling without each suite having to touch them.
/// </summary>
public class TestEmailServiceBase : IEmailService
{
    public virtual Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendAccountDeletionCancelLinkEmailAsync(string toEmail, string firstName, string cancelUrl, DateTime purgeDate, CancellationToken ct = default) => Task.CompletedTask;

    // Plan 4 additions — throw in production on failure; the no-op default here simply "succeeds" so
    // suites that don't exercise these paths aren't forced to stub them.
    public virtual Task SendMeetingInvitationAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendMeetingUpdatedAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendMeetingCancelledAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendNotificationAsync(string toEmail, string title, string body, string linkUrl, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task SendDigestAsync(string toEmail, DigestEmailModel model, CancellationToken ct = default) => Task.CompletedTask;
}
