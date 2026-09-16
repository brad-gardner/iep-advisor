using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Composes every outbound email's subject/HTML/text and hands the result to
/// <see cref="IOutboundEmailQueue"/> — it no longer sends anything itself (pilot-gates plan, phase 1,
/// decision 2). <c>OutboundEmailWorker</c> + <c>IEmailTransport</c> are the only real sender; this class
/// only renders. Enqueueing does not swallow: a queue write failure (e.g. the database is down)
/// propagates to the caller, same as any other write.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IOutboundEmailQueue _queue;
    private readonly string _frontendUrl;

    public EmailService(IConfiguration configuration, IOutboundEmailQueue queue)
    {
        _queue = queue;
        _frontendUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
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

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "PasswordReset", null, ct);
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

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "ShareInvite", null, ct);
    }

    public async Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default)
    {
        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/accept-link?token={Uri.EscapeDataString(inviteToken)}";

        var subject = $"{educatorName} invited you to connect on IEP Advisor";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">You've Been Invited to Connect</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    <strong>{educatorName}</strong> at <strong>{schoolName}</strong> has invited you to connect with <strong>{studentName}</strong>'s record on IEP Advisor.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    Linking lets you receive and understand your child's IEP information directly from the school.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{inviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Accept Invitation
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    This invitation expires in 14 days. If you don't have an IEP Advisor account, you'll be asked to create one.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"{educatorName} at {schoolName} has invited you to connect with {studentName}'s record on IEP Advisor.\n\nAccept the invitation: {inviteUrl}\n\nThis invitation expires in 14 days.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "SchoolLinkInvite", null, ct);
    }

    public async Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default)
    {
        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/student/accept-invite?token={Uri.EscapeDataString(inviteToken)}";

        var subject = $"{inviterName} invited you to your IEP Advisor student account";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">You've Been Invited</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    <strong>{inviterName}</strong> has invited you to set up your own student account on IEP Advisor {context}.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    Your student workspace lets you share your strengths, interests, and goals so your voice is part of your IEP.
                    You'll be asked to review and accept a short consent before your account is activated.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{inviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Set Up My Account
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    This invitation expires in 14 days.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"{inviterName} has invited you to set up your own student account on IEP Advisor {context}.\n\nYou'll be asked to accept a short consent before your account is activated.\n\nAccept the invitation: {inviteUrl}\n\nThis invitation expires in 14 days.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StudentInvite", null, ct);
    }

    public async Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default)
    {
        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/staff/accept-invite?token={Uri.EscapeDataString(inviteToken)}";

        var orgLine = string.IsNullOrWhiteSpace(schoolName)
            ? $"<strong>{districtName}</strong>"
            : $"<strong>{schoolName}</strong> ({districtName})";
        var orgLinePlain = string.IsNullOrWhiteSpace(schoolName)
            ? districtName
            : $"{schoolName} ({districtName})";

        var subject = $"You've been invited to join {districtName} on IEP Advisor";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">You've Been Invited to Join</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    You've been invited to join {orgLine} on IEP Advisor as a <strong>{roleName}</strong>.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    IEP Advisor helps school teams author, manage, and collaborate on IEPs. Accept the invitation
                    below to create your account and get started.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{inviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Accept Invitation
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    This invitation expires in 14 days and is tied to this email address.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"You've been invited to join {orgLinePlain} on IEP Advisor as a {roleName}.\n\nAccept the invitation: {inviteUrl}\n\nThis invitation expires in 14 days and is tied to this email address.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StaffInvite", null, ct);
    }

    public async Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default)
    {
        // Deep-links the admin straight to the staff management page so they can resend in one click.
        var staffUrl = $"{_frontendUrl}/educator/admin/staff";
        var expiresDisplay = expiresAt.ToString("MMMM d, yyyy");

        var orgLine = string.IsNullOrWhiteSpace(schoolName)
            ? $"<strong>{districtName}</strong>"
            : $"<strong>{schoolName}</strong> ({districtName})";
        var orgLinePlain = string.IsNullOrWhiteSpace(schoolName)
            ? districtName
            : $"{schoolName} ({districtName})";

        var subject = $"A staff invite for {inviteeEmail} is about to expire";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">A Staff Invite Is About to Expire</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    The staff invite you sent to <strong>{inviteeEmail}</strong> to join {orgLine} on IEP Advisor
                    expires on <strong>{expiresDisplay}</strong> and hasn't been accepted yet.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    If they still need access, you can resend the invite to reset the clock. If not, no action is needed —
                    the invite will simply expire.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{staffUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Manage Staff Invites
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    You're receiving this because you sent this invite. Only you are notified.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"The staff invite you sent to {inviteeEmail} to join {orgLinePlain} on IEP Advisor expires on {expiresDisplay} and hasn't been accepted yet.\n\nIf they still need access, resend the invite to reset the clock: {staffUrl}\n\nIf not, no action is needed — the invite will simply expire. Only you are notified.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StaffInviteExpiring", null, ct);
    }

    public async Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default)
    {
        var signupUrl = $"{_frontendUrl}/register?code={Uri.EscapeDataString(inviteCode)}";

        var subject = "Welcome to the IEP Advisor Beta";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    Hi there,
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    Welcome to the IEP Advisor beta. I'm Brad, the founder — and I wanted to reach out personally to say thank you for being here.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    IEP Advisor exists because parents deserve the same clarity and confidence at the IEP table that the school district's team already has. You're one of the first people to actually use it, which means your experience over the next few weeks will directly shape what this product becomes.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; font-weight: 500; color: #1E2A2A;"">
                    Here's what I'd love your help with:
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.9; padding-left: 8px;"">
                    → Try uploading a real IEP document and tell me if the plain-language explanations actually make sense<br />
                    → Let me know if anything is confusing, missing, or feels off<br />
                    → If you hit a bug or something breaks, please don't just close the tab — let me know directly or use the support link on the site!
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    You can email me directly at <a href=""mailto:bradgardner@sevenhillstechnology.com"" style=""color: #1A9478; text-decoration: none;"">bradgardner@sevenhillstechnology.com</a>
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; font-weight: 500; color: #1E2A2A;"">
                    A few things to know about the beta:
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.9; padding-left: 8px;"">
                    • Some features are still in progress — you may see rough edges<br />
                    • Your data is private and handled with care — not exposed or sold under any circumstance<br />
                    • This is the best time to influence what gets built next — I'd love to hear what other features you would find useful
                </p>
                <div style=""text-align: center; margin: 28px 0;"">
                    <a href=""{signupUrl}"" style=""display: inline-block; padding: 14px 28px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 15px; font-weight: 500;"">
                        Get Started
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    Your invite code: <strong>{inviteCode}</strong><br />
                    You can also enter this code manually at the sign-up page.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; margin-top: 24px;"">
                    Brad Gardner
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center; line-height: 1.6;"">
                    IEP Advisor · iep-advisor.com<br />
                    You're receiving this because you signed up for the beta.
                </p>
            </div>";

        var plainText = $@"Hi there,

Welcome to the IEP Advisor beta. I'm Brad, the founder — and I wanted to reach out personally to say thank you for being here.

IEP Advisor exists because parents deserve the same clarity and confidence at the IEP table that the school district's team already has. You're one of the first people to actually use it, which means your experience over the next few weeks will directly shape what this product becomes.

Here's what I'd love your help with:

→ Try uploading a real IEP document and tell me if the plain-language explanations actually make sense
→ Let me know if anything is confusing, missing, or feels off
→ If you hit a bug or something breaks, please don't just close the tab — let me know directly or use the support link on the site!

You can email me directly at bradgardner@sevenhillstechnology.com

A few things to know about the beta:
• Some features are still in progress — you may see rough edges
• Your data is private and handled with care — not exposed or sold under any circumstance
• This is the best time to influence what gets built next — I'd love to hear what other features you would find useful

To get started: {signupUrl}

Your invite code: {inviteCode}
You can also enter this code manually at the sign-up page.

Brad Gardner

—
IEP Advisor · iep-advisor.com
You're receiving this because you signed up for the beta.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "BetaInvite", null, ct);
    }

    public async Task SendAccountDeletionCancelLinkEmailAsync(string toEmail, string firstName, string cancelUrl, DateTime purgeDate, CancellationToken ct = default)
    {
        var safeFirstName = WebUtility.HtmlEncode(firstName);
        var safeCancelUrl = WebUtility.HtmlEncode(cancelUrl);
        var purgeDateDisplay = purgeDate.ToString("MMMM d, yyyy");

        var subject = "Your IEP Advisor account is scheduled for deletion";
        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">Your Account Is Scheduled for Deletion</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    Hi {safeFirstName}, we received a request to delete your IEP Advisor account. It will be
                    permanently deleted on <strong>{purgeDateDisplay}</strong> unless you cancel before then.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    If this was you and you want your account deleted, no action is needed. If you didn't
                    request this — or changed your mind — click below to cancel.
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeCancelUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Cancel Deletion
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    Your account has been deactivated in the meantime, so this link is the only way to cancel — signing in will not work until you use it.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = $"Hi {firstName}, we received a request to delete your IEP Advisor account. It will be permanently deleted on {purgeDateDisplay} unless you cancel before then.\n\nCancel deletion: {cancelUrl}\n\nYour account has been deactivated in the meantime, so this link is the only way to cancel.";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "AccountDeletionCancelLink", null, ct);
    }

    // ----------------------------------------------------------------- Plan 4 additions (throw on failure)

    public Task SendMeetingInvitationAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting scheduled", "A meeting has been scheduled", "MeetingInvitation", model, ics, ct);

    public Task SendMeetingUpdatedAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting updated", "A meeting has been updated", "MeetingUpdated", model, ics, ct);

    public Task SendMeetingCancelledAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting cancelled", "A meeting has been cancelled", "MeetingCancelled", model, ics, ct);

    private async Task SendMeetingEmailAsync(string toEmail, string subjectPrefix, string introText, string kind, MeetingEmailModel model, byte[] ics, CancellationToken ct)
    {
        var subject = $"{subjectPrefix}: {model.Title} for {model.StudentFirstName}";
        var whenLine = $"{model.StartsAtUtc:MMMM d, yyyy} at {model.StartsAtUtc:h:mm tt} ({model.TimeZoneId})";

        var rsvpPlain = string.Empty;
        if (!string.IsNullOrWhiteSpace(model.RsvpAcceptUrl) && !string.IsNullOrWhiteSpace(model.RsvpDeclineUrl))
            rsvpPlain = $"\nAccept: {model.RsvpAcceptUrl}\nDecline: {model.RsvpDeclineUrl}\n";

        var html = RenderMeetingHtml(introText, model);
        var plainText = $"{introText}\n\n{model.Title} for {model.StudentFirstName}, organized by {model.OrganizerName}.\n{whenLine} - {model.DurationMinutes} minutes{(string.IsNullOrWhiteSpace(model.Location) ? "" : $" - {model.Location}")}\n{rsvpPlain}\nDetails: {model.DetailUrl}\nA calendar invite (.ics) is attached.";

        var attachment = new OutboundEmailAttachmentDraft("meeting.ics", "text/calendar", ics);
        await EnqueueEmailAsync(toEmail, subject, html, plainText, kind, new[] { attachment }, ct);
    }

    /// <summary>Renders <see cref="SendMeetingEmailAsync"/>'s HTML body. Every interpolated value that
    /// originates from staff/attacker-controllable input (title, location, organizer/student names, and
    /// the app-constructed URLs) is HTML-encoded — a meeting Title containing markup must not execute in
    /// the recipient's mail client (todos/049). Internal + static so it is unit-testable without a live
    /// ACS connection.</summary>
    internal static string RenderMeetingHtml(string introText, MeetingEmailModel model)
    {
        var title = WebUtility.HtmlEncode(model.Title);
        var studentFirstName = WebUtility.HtmlEncode(model.StudentFirstName);
        var organizerName = WebUtility.HtmlEncode(model.OrganizerName);
        var timeZoneId = WebUtility.HtmlEncode(model.TimeZoneId);
        var location = string.IsNullOrWhiteSpace(model.Location) ? null : WebUtility.HtmlEncode(model.Location);
        var detailUrl = WebUtility.HtmlEncode(model.DetailUrl);
        var whenLine = $"{model.StartsAtUtc:MMMM d, yyyy} at {model.StartsAtUtc:h:mm tt} ({timeZoneId})";

        var rsvpHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(model.RsvpAcceptUrl) && !string.IsNullOrWhiteSpace(model.RsvpDeclineUrl))
        {
            var acceptUrl = WebUtility.HtmlEncode(model.RsvpAcceptUrl);
            var declineUrl = WebUtility.HtmlEncode(model.RsvpDeclineUrl);
            rsvpHtml = $@"
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{acceptUrl}"" style=""display: inline-block; margin: 0 8px; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">Accept</a>
                    <a href=""{declineUrl}"" style=""display: inline-block; margin: 0 8px; padding: 12px 24px; background-color: #E8ECEC; color: #1E2A2A; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">Decline</a>
                </div>";
        }

        return $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{WebUtility.HtmlEncode(introText)}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    <strong>{title}</strong> for {studentFirstName}, organized by {organizerName}.
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {whenLine} &middot; {model.DurationMinutes} minutes{(location == null ? "" : $" &middot; {location}")}
                </p>
                {rsvpHtml}
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    A calendar invite (.ics) is attached. <a href=""{detailUrl}"" style=""color: #1A9478;"">View meeting details</a>.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";
    }

    public async Task SendNotificationAsync(string toEmail, string title, string body, string linkUrl, CancellationToken ct = default)
    {
        var html = RenderNotificationHtml(title, body, linkUrl);
        var plainText = $"{title}\n\n{body}\n\nView in IEP Advisor: {linkUrl}";

        await EnqueueEmailAsync(toEmail, title, html, plainText, "Notification", null, ct);
    }

    /// <summary>Renders <see cref="SendNotificationAsync"/>'s HTML body. <paramref name="title"/>/
    /// <paramref name="body"/> ultimately derive from a staff-supplied meeting Title (MeetingService builds
    /// them as e.g. "Meeting updated: {meeting.Title}") and must be HTML-encoded (todos/049).</summary>
    internal static string RenderNotificationHtml(string title, string body, string linkUrl)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeBody = WebUtility.HtmlEncode(body);
        var safeLink = WebUtility.HtmlEncode(linkUrl);

        return $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{safeTitle}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">{safeBody}</p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeLink}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        View in IEP Advisor
                    </a>
                </div>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";
    }

    public async Task SendDigestAsync(string toEmail, DigestEmailModel model, CancellationToken ct = default)
    {
        var subject = "Your daily IEP Advisor digest";
        var html = RenderDigestHtml(model);

        var plainText = $"Good morning, {model.RecipientFirstName}\n\nDeadlines:\n" +
            (model.Obligations.Count == 0 ? "No overdue or upcoming deadlines.\n" : string.Concat(model.Obligations.Select(o => $"- {o.Status}: {o.Kind} for {o.StudentName}{(o.DueDate.HasValue ? $" (due {o.DueDate.Value:MMM d, yyyy})" : "")}\n"))) +
            "\nMeetings in the next 7 days:\n" +
            (model.UpcomingMeetings.Count == 0 ? "No meetings in the next 7 days.\n" : string.Concat(model.UpcomingMeetings.Select(m => $"- {m.Title} for {m.StudentName} - {m.StartsAtUtc:MMM d, h:mm tt} ({m.TimeZoneId})\n"))) +
            $"\nOpen IEP Advisor: {model.DetailUrl}";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "Digest", null, ct);
    }

    /// <summary>Renders <see cref="SendDigestAsync"/>'s HTML body. Obligation/meeting StudentName and
    /// meeting Title are staff-supplied and must be HTML-encoded (todos/049); Kind/Status are enums and
    /// need no encoding, but are included via <see cref="object.ToString"/> either way.</summary>
    internal static string RenderDigestHtml(DigestEmailModel model)
    {
        var recipientFirstName = WebUtility.HtmlEncode(model.RecipientFirstName);
        var detailUrl = WebUtility.HtmlEncode(model.DetailUrl);

        var obligationRows = model.Obligations.Count == 0
            ? "<p style=\"font-size: 13px; color: #A8B5B5;\">No overdue or upcoming deadlines. Nice work.</p>"
            : string.Concat(model.Obligations.Select(o =>
                $"<li style=\"font-size: 13px; color: #5A6F6F; margin-bottom: 4px;\"><strong>{o.Status}</strong> — {o.Kind} for {WebUtility.HtmlEncode(o.StudentName)}{(o.DueDate.HasValue ? $" (due {o.DueDate.Value:MMM d, yyyy})" : "")}</li>"));
        var meetingRows = model.UpcomingMeetings.Count == 0
            ? "<p style=\"font-size: 13px; color: #A8B5B5;\">No meetings in the next 7 days.</p>"
            : string.Concat(model.UpcomingMeetings.Select(m =>
                $"<li style=\"font-size: 13px; color: #5A6F6F; margin-bottom: 4px;\">{WebUtility.HtmlEncode(m.Title)} for {WebUtility.HtmlEncode(m.StudentName)} — {m.StartsAtUtc:MMM d, h:mm tt} ({WebUtility.HtmlEncode(m.TimeZoneId)})</li>"));

        return $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">Good morning, {recipientFirstName}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6; font-weight: 500;"">Deadlines</p>
                <ul style=""padding-left: 18px; margin: 0 0 16px;"">{obligationRows}</ul>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6; font-weight: 500;"">Meetings in the next 7 days</p>
                <ul style=""padding-left: 18px; margin: 0 0 16px;"">{meetingRows}</ul>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{detailUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        Open IEP Advisor
                    </a>
                </div>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";
    }

    /// <summary>Composes an <see cref="OutboundEmailDraft"/> and enqueues it. This is the ONLY place any
    /// Send* method reaches the queue — a queue write failure (not a delivery failure; there is no send
    /// attempt yet) propagates to the caller unchanged, which is decision 2's "no longer swallows".</summary>
    private Task EnqueueEmailAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        string kind,
        IReadOnlyList<OutboundEmailAttachmentDraft>? attachments,
        CancellationToken ct)
        => _queue.EnqueueAsync(new OutboundEmailDraft
        {
            ToEmail = toEmail,
            Subject = subject,
            HtmlBody = htmlContent,
            TextBody = plainTextContent,
            Kind = kind,
            Attachments = attachments
        }, ct);
}
