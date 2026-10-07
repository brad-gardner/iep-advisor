using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Composes every outbound email's subject/HTML/text and hands the result to
/// <see cref="IOutboundEmailQueue"/> — it no longer sends anything itself (pilot-gates plan, phase 1,
/// decision 2). <c>OutboundEmailWorker</c> + <c>IEmailTransport</c> are the only real sender; this class
/// only renders. Enqueueing does not swallow: a queue write failure (e.g. the database is down)
/// propagates to the caller, same as any other write.
///
/// Plan 2026-10-06 (multilingual) phase 1: the password-reset and magic-link emails render in the
/// recipient's language via <see cref="Emails"/>/<see cref="CultureScope"/>.
///
/// Phase 4: every remaining Send* method follows suit. Each resolves the RECIPIENT's language —
/// <see cref="ResolveRecipientAsync"/> looks up <c>toEmail</c> in <c>Users</c> and uses the account's own
/// saved <c>PreferredLanguage</c> (defaulting to English when unset — the recipient's own language always
/// wins, it is never the sender's). Only a pre-account recipient (no User row for that address) falls back
/// to <see cref="SupportedLanguages.ForRecipient"/> — effectively the sender's current request UI culture,
/// English outside a request — because there is no other signal for someone who has never set a
/// preference and isn't the person driving this request (design decision 5). Pre-account invite/landing
/// links get <c>?lang=</c> appended (<see cref="AppendLangParam"/>) so the public landing page opens in
/// that language before anyone has signed in.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IOutboundEmailQueue _queue;
    private readonly IStringLocalizer<Emails> _localizer;
    private readonly ApplicationDbContext _context;
    private readonly string _frontendUrl;

    public EmailService(IConfiguration configuration, IOutboundEmailQueue queue, IStringLocalizer<Emails> localizer, ApplicationDbContext context)
    {
        _queue = queue;
        _localizer = localizer;
        _context = context;
        _frontendUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string? language = null, CancellationToken ct = default)
    {
        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var resetUrl = $"{_frontendUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

        using var _ = CultureScope.For(language);

        var subject = _localizer["PasswordReset.Subject"].Value;
        var heading = _localizer["PasswordReset.Heading"].Value;
        var body = _localizer["PasswordReset.Body"].Value;
        var buttonText = _localizer["PasswordReset.ButtonText"].Value;
        var footer = _localizer["PasswordReset.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeResetUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["PasswordReset.PlainTextBody"].Value, resetUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "PasswordReset", null, ct);
    }

    public async Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default)
    {
        var (language, hasAccount) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        var inviteUrl = $"{_frontendUrl}/accept-invite?token={Uri.EscapeDataString(inviteToken)}";
        if (!hasAccount)
            inviteUrl = AppendLangParam(inviteUrl, language);
        var safeInviteUrl = WebUtility.HtmlEncode(inviteUrl);

        var safeInviterName = WebUtility.HtmlEncode(inviterName);
        var safeChildName = WebUtility.HtmlEncode(childName);
        var isCollaborator = role == "Collaborator";

        var subject = string.Format(_localizer["ShareInvite.Subject"].Value, inviterName);
        var heading = _localizer["ShareInvite.Heading"].Value;
        var body1Key = isCollaborator ? "ShareInvite.Body1Collaborate" : "ShareInvite.Body1View";
        var body1 = string.Format(_localizer[body1Key].Value, $"<strong>{safeInviterName}</strong>", safeChildName);
        var body2 = _localizer["ShareInvite.Body2"].Value;
        var buttonText = _localizer["ShareInvite.ButtonText"].Value;
        var footer = _localizer["ShareInvite.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeInviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainTextKey = isCollaborator ? "ShareInvite.PlainTextCollaborate" : "ShareInvite.PlainTextView";
        var plainText = string.Format(_localizer[plainTextKey].Value, inviterName, childName, inviteUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "ShareInvite", null, ct);
    }

    public async Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default)
    {
        var (language, hasAccount) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/accept-link?token={Uri.EscapeDataString(inviteToken)}";
        if (!hasAccount)
            inviteUrl = AppendLangParam(inviteUrl, language);
        var safeInviteUrl = WebUtility.HtmlEncode(inviteUrl);

        var safeEducatorName = WebUtility.HtmlEncode(educatorName);
        var safeSchoolName = WebUtility.HtmlEncode(schoolName);
        var safeStudentName = WebUtility.HtmlEncode(studentName);

        var subject = string.Format(_localizer["SchoolLinkInvite.Subject"].Value, educatorName);
        var heading = _localizer["SchoolLinkInvite.Heading"].Value;
        var body1 = string.Format(_localizer["SchoolLinkInvite.Body1"].Value, $"<strong>{safeEducatorName}</strong>", $"<strong>{safeSchoolName}</strong>", $"<strong>{safeStudentName}</strong>");
        var body2 = _localizer["SchoolLinkInvite.Body2"].Value;
        var buttonText = _localizer["SchoolLinkInvite.ButtonText"].Value;
        var footer = _localizer["SchoolLinkInvite.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeInviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["SchoolLinkInvite.PlainTextBody"].Value, educatorName, schoolName, studentName, inviteUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "SchoolLinkInvite", null, ct);
    }

    public async Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default)
    {
        var (language, hasAccount) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/student/accept-invite?token={Uri.EscapeDataString(inviteToken)}";
        if (!hasAccount)
            inviteUrl = AppendLangParam(inviteUrl, language);
        var safeInviteUrl = WebUtility.HtmlEncode(inviteUrl);

        var safeInviterName = WebUtility.HtmlEncode(inviterName);
        // NOTE: `context` (e.g. "to contribute to Sam's IEP" / "at Lincoln High School") arrives from the
        // caller (StudentInviteService, outside this worker's ownership) as a pre-built ENGLISH sentence
        // fragment, not structured data. For a Spanish recipient the surrounding sentence below is
        // Spanish but this clause stays English — see the implementation handoff notes for the fix
        // (pass structured data instead of a pre-formatted phrase), which requires a caller-side change.
        var safeContext = WebUtility.HtmlEncode(context);

        var subject = string.Format(_localizer["StudentInvite.Subject"].Value, inviterName);
        var heading = _localizer["StudentInvite.Heading"].Value;
        var body1 = string.Format(_localizer["StudentInvite.Body1"].Value, $"<strong>{safeInviterName}</strong>", safeContext);
        var body2 = _localizer["StudentInvite.Body2"].Value;
        var buttonText = _localizer["StudentInvite.ButtonText"].Value;
        var footer = _localizer["StudentInvite.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeInviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["StudentInvite.PlainTextBody"].Value, inviterName, context, inviteUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StudentInvite", null, ct);
    }

    public async Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default)
    {
        var (language, hasAccount) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        // Escape the base64 token (may contain +, /, =) so it survives the URL intact.
        var inviteUrl = $"{_frontendUrl}/staff/accept-invite?token={Uri.EscapeDataString(inviteToken)}";
        if (!hasAccount)
            inviteUrl = AppendLangParam(inviteUrl, language);
        var safeInviteUrl = WebUtility.HtmlEncode(inviteUrl);

        var safeDistrictName = WebUtility.HtmlEncode(districtName);
        var safeSchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : WebUtility.HtmlEncode(schoolName);
        // roleName is district-authored org-role content (OrgRole.Name) — stays as written, like
        // district/school names, never translated (design: "Not translated (by design)").
        var safeRoleName = WebUtility.HtmlEncode(roleName);

        var orgLine = safeSchoolName == null
            ? $"<strong>{safeDistrictName}</strong>"
            : $"<strong>{safeSchoolName}</strong> ({safeDistrictName})";
        var orgLinePlain = string.IsNullOrWhiteSpace(schoolName)
            ? districtName
            : $"{schoolName} ({districtName})";

        var subject = string.Format(_localizer["StaffInvite.Subject"].Value, districtName);
        var heading = _localizer["StaffInvite.Heading"].Value;
        var body1 = string.Format(_localizer["StaffInvite.Body1"].Value, orgLine, $"<strong>{safeRoleName}</strong>");
        var body2 = _localizer["StaffInvite.Body2"].Value;
        var buttonText = _localizer["StaffInvite.ButtonText"].Value;
        var footer = _localizer["StaffInvite.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeInviteUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["StaffInvite.PlainTextBody"].Value, orgLinePlain, roleName, inviteUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StaffInvite", null, ct);
    }

    public async Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default)
    {
        // The recipient here is the admin who sent the invite (an existing account), not the invitee —
        // not a pre-account landing link, so no ?lang= and no AppendLangParam.
        var (language, _) = await ResolveRecipientAsync(toEmail, ct);
        using var _scope = CultureScope.For(language);

        // Deep-links the admin straight to the staff management page so they can resend in one click.
        var staffUrl = $"{_frontendUrl}/educator/admin/staff";
        var expiresDisplay = FormatLongDate(expiresAt, language);

        var safeInviteeEmail = WebUtility.HtmlEncode(inviteeEmail);
        var safeDistrictName = WebUtility.HtmlEncode(districtName);
        var safeSchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : WebUtility.HtmlEncode(schoolName);

        var orgLine = safeSchoolName == null
            ? $"<strong>{safeDistrictName}</strong>"
            : $"<strong>{safeSchoolName}</strong> ({safeDistrictName})";
        var orgLinePlain = string.IsNullOrWhiteSpace(schoolName)
            ? districtName
            : $"{schoolName} ({districtName})";

        var subject = string.Format(_localizer["StaffInviteExpiring.Subject"].Value, inviteeEmail);
        var heading = _localizer["StaffInviteExpiring.Heading"].Value;
        var body1 = string.Format(_localizer["StaffInviteExpiring.Body1"].Value, $"<strong>{safeInviteeEmail}</strong>", orgLine, $"<strong>{expiresDisplay}</strong>");
        var body2 = _localizer["StaffInviteExpiring.Body2"].Value;
        var buttonText = _localizer["StaffInviteExpiring.ButtonText"].Value;
        var footer = _localizer["StaffInviteExpiring.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{staffUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["StaffInviteExpiring.PlainTextBody"].Value, inviteeEmail, orgLinePlain, expiresDisplay, staffUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "StaffInviteExpiring", null, ct);
    }

    public async Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default)
    {
        var (language, hasAccount) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        var signupUrl = $"{_frontendUrl}/register?code={Uri.EscapeDataString(inviteCode)}";
        if (!hasAccount)
            signupUrl = AppendLangParam(signupUrl, language);
        var safeSignupUrl = WebUtility.HtmlEncode(signupUrl);
        var safeInviteCode = WebUtility.HtmlEncode(inviteCode);

        var subject = _localizer["BetaInvite.Subject"].Value;
        var greeting = _localizer["BetaInvite.Greeting"].Value;
        var intro1 = _localizer["BetaInvite.Intro1"].Value;
        var intro2 = _localizer["BetaInvite.Intro2"].Value;
        var helpHeading = _localizer["BetaInvite.HelpHeading"].Value;
        var helpBulletsHtml = _localizer["BetaInvite.HelpBulletsHtml"].Value;
        var helpBulletsPlain = _localizer["BetaInvite.HelpBulletsPlain"].Value;
        var emailLine = _localizer["BetaInvite.EmailLine"].Value;
        var betaHeading = _localizer["BetaInvite.BetaHeading"].Value;
        var betaBulletsHtml = _localizer["BetaInvite.BetaBulletsHtml"].Value;
        var betaBulletsPlain = _localizer["BetaInvite.BetaBulletsPlain"].Value;
        var buttonText = _localizer["BetaInvite.ButtonText"].Value;
        var codeLabelHtml = string.Format(_localizer["BetaInvite.CodeLabel"].Value, safeInviteCode);
        var codeLabelPlain = string.Format(_localizer["BetaInvite.CodeLabel"].Value, inviteCode);
        var codeManualNote = _localizer["BetaInvite.CodeManualNote"].Value;
        var signature = _localizer["BetaInvite.Signature"].Value;
        var footerNote = _localizer["BetaInvite.FooterNote"].Value;
        var plainCtaLabel = _localizer["BetaInvite.PlainCtaLabel"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    {greeting}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    {intro1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    {intro2}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; font-weight: 500; color: #1E2A2A;"">
                    {helpHeading}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.9; padding-left: 8px;"">
                    {helpBulletsHtml}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7;"">
                    {emailLine} <a href=""mailto:bradgardner@sevenhillstechnology.com"" style=""color: #1A9478; text-decoration: none;"">bradgardner@sevenhillstechnology.com</a>
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; font-weight: 500; color: #1E2A2A;"">
                    {betaHeading}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.9; padding-left: 8px;"">
                    {betaBulletsHtml}
                </p>
                <div style=""text-align: center; margin: 28px 0;"">
                    <a href=""{safeSignupUrl}"" style=""display: inline-block; padding: 14px 28px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 15px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {codeLabelHtml}<br />
                    {codeManualNote}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.7; margin-top: 24px;"">
                    {signature}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center; line-height: 1.6;"">
                    IEP Advisor · iep-advisor.com<br />
                    {footerNote}
                </p>
            </div>";

        var plainText = $@"{greeting}

{intro1}

{intro2}

{helpHeading}

{helpBulletsPlain}

{emailLine} bradgardner@sevenhillstechnology.com

{betaHeading}
{betaBulletsPlain}

{plainCtaLabel}: {signupUrl}

{codeLabelPlain}
{codeManualNote}

{signature}

—
IEP Advisor · iep-advisor.com
{footerNote}";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "BetaInvite", null, ct);
    }

    public async Task SendAccountDeletionCancelLinkEmailAsync(string toEmail, string firstName, string cancelUrl, DateTime purgeDate, CancellationToken ct = default)
    {
        // The recipient is the account owner (deactivated but still a User row) — not a pre-account
        // invite, and cancelUrl is a signed token URL built by the caller: never mutate its query string.
        var (language, _) = await ResolveRecipientAsync(toEmail, ct);
        using var _scope = CultureScope.For(language);

        var safeFirstName = WebUtility.HtmlEncode(firstName);
        var safeCancelUrl = WebUtility.HtmlEncode(cancelUrl);
        var purgeDateDisplay = FormatLongDate(purgeDate, language);

        var subject = _localizer["AccountDeletionCancelLink.Subject"].Value;
        var heading = _localizer["AccountDeletionCancelLink.Heading"].Value;
        var body1 = string.Format(_localizer["AccountDeletionCancelLink.Body1"].Value, safeFirstName, purgeDateDisplay);
        var body2 = _localizer["AccountDeletionCancelLink.Body2"].Value;
        var buttonText = _localizer["AccountDeletionCancelLink.ButtonText"].Value;
        var footer = _localizer["AccountDeletionCancelLink.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body1}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {body2}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeCancelUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["AccountDeletionCancelLink.PlainTextBody"].Value, firstName, purgeDateDisplay, cancelUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "AccountDeletionCancelLink", null, ct);
    }

    public async Task SendMagicLinkEmailAsync(string toEmail, string firstName, string magicLinkUrl, string? language = null, CancellationToken ct = default)
    {
        var safeFirstName = WebUtility.HtmlEncode(firstName);
        var safeMagicLinkUrl = WebUtility.HtmlEncode(magicLinkUrl);

        using var _ = CultureScope.For(language);

        var subject = _localizer["MagicLink.Subject"].Value;
        var heading = _localizer["MagicLink.Heading"].Value;
        var greeting = string.Format(_localizer["MagicLink.Greeting"].Value, safeFirstName);
        var buttonText = _localizer["MagicLink.ButtonText"].Value;
        var footer = _localizer["MagicLink.Footer"].Value;

        var html = $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{heading}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {greeting}
                </p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{safeMagicLinkUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {footer}
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";

        var plainText = string.Format(_localizer["MagicLink.PlainTextBody"].Value, firstName, magicLinkUrl);

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "MagicLink", null, ct);
    }

    // ----------------------------------------------------------------- Plan 4 additions (throw on failure)

    public Task SendMeetingInvitationAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting.Invitation.SubjectPrefix", "Meeting.Invitation.Intro", "MeetingInvitation", model, ics, ct);

    public Task SendMeetingUpdatedAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting.Updated.SubjectPrefix", "Meeting.Updated.Intro", "MeetingUpdated", model, ics, ct);

    public Task SendMeetingCancelledAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        => SendMeetingEmailAsync(toEmail, "Meeting.Cancelled.SubjectPrefix", "Meeting.Cancelled.Intro", "MeetingCancelled", model, ics, ct);

    /// <summary>Note: the .ics attachment bytes are built by the CALLER (outside this worker's
    /// ownership) before reaching here, so they are not re-rendered in the recipient's language — only
    /// this email's own subject/HTML/plain-text wrapper is. See IcsBuilder.cs for why its one literal
    /// string doesn't need a language hook.</summary>
    private async Task SendMeetingEmailAsync(string toEmail, string subjectPrefixKey, string introTextKey, string kind, MeetingEmailModel model, byte[] ics, CancellationToken ct)
    {
        var (language, _) = await ResolveRecipientAsync(toEmail, ct);
        using var _scope = CultureScope.For(language);

        var subjectPrefix = _localizer[subjectPrefixKey].Value;
        var introText = _localizer[introTextKey].Value;
        var forConnector = _localizer["Meeting.SubjectForConnector"].Value;
        var subject = $"{subjectPrefix}: {model.Title} {forConnector} {model.StudentFirstName}";

        var (datePart, timePart) = FormatDateTimeParts(model.StartsAtUtc, language);
        var atConnector = _localizer["Meeting.At"].Value;
        var whenLine = $"{datePart} {atConnector} {timePart} ({model.TimeZoneId})";
        var durationLabel = string.Format(_localizer["Meeting.DurationMinutes"].Value, model.DurationMinutes);
        var organizedByLine = string.Format(_localizer["Meeting.OrganizedBy"].Value, model.Title, model.StudentFirstName, model.OrganizerName);
        var detailsLabel = _localizer["Meeting.DetailsLabel"].Value;
        var icsAttachedText = _localizer["Meeting.IcsAttached"].Value;

        var rsvpPlain = string.Empty;
        if (!string.IsNullOrWhiteSpace(model.RsvpAcceptUrl) && !string.IsNullOrWhiteSpace(model.RsvpDeclineUrl))
        {
            var acceptLabel = _localizer["Meeting.RsvpAccept"].Value;
            var declineLabel = _localizer["Meeting.RsvpDecline"].Value;
            rsvpPlain = $"\n{acceptLabel}: {model.RsvpAcceptUrl}\n{declineLabel}: {model.RsvpDeclineUrl}\n";
        }

        var html = RenderMeetingHtml(introText, model, language, _localizer);
        var plainText = $"{introText}\n\n{organizedByLine}\n{whenLine} - {durationLabel}{(string.IsNullOrWhiteSpace(model.Location) ? "" : $" - {model.Location}")}\n{rsvpPlain}\n{detailsLabel}: {model.DetailUrl}\n{icsAttachedText}";

        var attachment = new OutboundEmailAttachmentDraft("meeting.ics", "text/calendar", ics);
        await EnqueueEmailAsync(toEmail, subject, html, plainText, kind, new[] { attachment }, ct);
    }

    /// <summary>Renders <see cref="SendMeetingEmailAsync"/>'s HTML body. Every interpolated value that
    /// originates from staff/attacker-controllable input (title, location, organizer/student names, and
    /// the app-constructed URLs) is HTML-encoded — a meeting Title containing markup must not execute in
    /// the recipient's mail client (todos/049). <paramref name="language"/> drives explicit date/time
    /// formatting (Spanish month names) — see <see cref="FormatDateTimeParts"/>; <paramref name="localizer"/>
    /// resolves the surrounding chrome text. Internal + static so it is unit-testable without a live ACS
    /// connection.</summary>
    internal static string RenderMeetingHtml(string introText, MeetingEmailModel model, string language, IStringLocalizer<Emails> localizer)
    {
        // Self-contained: `localizer["key"]` resolves against the AMBIENT CurrentUICulture, not the
        // `language` parameter directly, so this opens its own scope rather than trusting the caller to
        // have one active already (CultureScope.For nests safely — it just saves/restores).
        using var _ = CultureScope.For(language);

        var title = WebUtility.HtmlEncode(model.Title);
        var studentFirstName = WebUtility.HtmlEncode(model.StudentFirstName);
        var organizerName = WebUtility.HtmlEncode(model.OrganizerName);
        var timeZoneId = WebUtility.HtmlEncode(model.TimeZoneId);
        var location = string.IsNullOrWhiteSpace(model.Location) ? null : WebUtility.HtmlEncode(model.Location);
        var detailUrl = WebUtility.HtmlEncode(model.DetailUrl);

        var (datePart, timePart) = FormatDateTimeParts(model.StartsAtUtc, language);
        var atConnector = localizer["Meeting.At"].Value;
        var whenLine = $"{datePart} {atConnector} {timePart} ({timeZoneId})";
        var durationLabel = string.Format(localizer["Meeting.DurationMinutes"].Value, model.DurationMinutes);
        var organizedByLine = string.Format(localizer["Meeting.OrganizedBy"].Value, $"<strong>{title}</strong>", studentFirstName, organizerName);
        var icsAttachedText = localizer["Meeting.IcsAttached"].Value;
        var viewDetailsText = localizer["Meeting.ViewDetails"].Value;

        var rsvpHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(model.RsvpAcceptUrl) && !string.IsNullOrWhiteSpace(model.RsvpDeclineUrl))
        {
            var acceptUrl = WebUtility.HtmlEncode(model.RsvpAcceptUrl);
            var declineUrl = WebUtility.HtmlEncode(model.RsvpDeclineUrl);
            var acceptText = localizer["Meeting.RsvpAccept"].Value;
            var declineText = localizer["Meeting.RsvpDecline"].Value;
            rsvpHtml = $@"
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{acceptUrl}"" style=""display: inline-block; margin: 0 8px; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">{acceptText}</a>
                    <a href=""{declineUrl}"" style=""display: inline-block; margin: 0 8px; padding: 12px 24px; background-color: #E8ECEC; color: #1E2A2A; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">{declineText}</a>
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
                    {organizedByLine}
                </p>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6;"">
                    {whenLine} &middot; {durationLabel}{(location == null ? "" : $" &middot; {location}")}
                </p>
                {rsvpHtml}
                <p style=""font-size: 12px; color: #A8B5B5; line-height: 1.5;"">
                    {icsAttachedText} <a href=""{detailUrl}"" style=""color: #1A9478;"">{viewDetailsText}</a>.
                </p>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";
    }

    public async Task SendNotificationAsync(string toEmail, string title, string body, string linkUrl, CancellationToken ct = default)
    {
        // title/body come from the stored Notification row — the OTHER worker localizes those at
        // creation time (NotificationService.cs, outside this worker's ownership). This method only
        // localizes its own wrapper chrome (the button text), per the task's explicit scope.
        var (language, _) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        var html = RenderNotificationHtml(title, body, linkUrl, _localizer);
        var buttonText = _localizer["Notification.ButtonText"].Value;
        var plainText = $"{title}\n\n{body}\n\n{buttonText}: {linkUrl}";

        await EnqueueEmailAsync(toEmail, title, html, plainText, "Notification", null, ct);
    }

    /// <summary>Renders <see cref="SendNotificationAsync"/>'s HTML body. <paramref name="title"/>/
    /// <paramref name="body"/> ultimately derive from a staff-supplied meeting Title (MeetingService builds
    /// them as e.g. "Meeting updated: {meeting.Title}") and must be HTML-encoded (todos/049). Only the
    /// wrapper's button text is localized via <paramref name="localizer"/> — title/body are rendered
    /// exactly as given (already localized by the notification's creator, or pass-through English).</summary>
    internal static string RenderNotificationHtml(string title, string body, string linkUrl, IStringLocalizer<Emails> localizer)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeBody = WebUtility.HtmlEncode(body);
        var safeLink = WebUtility.HtmlEncode(linkUrl);
        var buttonText = localizer["Notification.ButtonText"].Value;

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
                        {buttonText}
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
        var (language, _) = await ResolveRecipientAsync(toEmail, ct);
        using var _ = CultureScope.For(language);

        var subject = _localizer["Digest.Subject"].Value;
        var html = RenderDigestHtml(model, language, _localizer);

        var greeting = string.Format(_localizer["Digest.Greeting"].Value, model.RecipientFirstName);
        var deadlinesHeadingPlain = _localizer["Digest.DeadlinesHeadingPlain"].Value;
        var noDeadlines = _localizer["Digest.NoDeadlines"].Value;
        var meetingsHeadingPlain = _localizer["Digest.MeetingsHeadingPlain"].Value;
        var noMeetings = _localizer["Digest.NoMeetings"].Value;
        var dueDateSuffixFormat = _localizer["Digest.DueDateSuffix"].Value;
        var buttonText = _localizer["Digest.ButtonText"].Value;

        var plainText = $"{greeting}\n\n{deadlinesHeadingPlain}\n" +
            (model.Obligations.Count == 0
                ? $"{noDeadlines}\n"
                : string.Concat(model.Obligations.Select(o =>
                    $"- {o.Status}: {o.Kind} for {o.StudentName}{(o.DueDate.HasValue ? string.Format(dueDateSuffixFormat, FormatShortDate(o.DueDate.Value, language)) : "")}\n"))) +
            $"\n{meetingsHeadingPlain}\n" +
            (model.UpcomingMeetings.Count == 0
                ? $"{noMeetings}\n"
                : string.Concat(model.UpcomingMeetings.Select(m =>
                    $"- {m.Title} for {m.StudentName} - {FormatShortDateTime(m.StartsAtUtc, language)} ({m.TimeZoneId})\n"))) +
            $"\n{buttonText}: {model.DetailUrl}";

        await EnqueueEmailAsync(toEmail, subject, html, plainText, "Digest", null, ct);
    }

    /// <summary>Renders <see cref="SendDigestAsync"/>'s HTML body. Obligation/meeting StudentName and
    /// meeting Title are staff-supplied and must be HTML-encoded (todos/049); Kind/Status are enums shown
    /// via their raw <see cref="object.ToString"/> name today (e.g. "AnnualReview") in BOTH languages —
    /// intentionally left as-is rather than guessing a translation for pre-existing rough-edge display
    /// text that isn't a localization regression. Dates are formatted explicitly for
    /// <paramref name="language"/> (see <see cref="FormatShortDate"/>/<see cref="FormatShortDateTime"/>);
    /// <paramref name="localizer"/> resolves the surrounding chrome text.</summary>
    internal static string RenderDigestHtml(DigestEmailModel model, string language, IStringLocalizer<Emails> localizer)
    {
        // Self-contained: see RenderMeetingHtml's note on why this opens its own CultureScope.
        using var _ = CultureScope.For(language);

        var recipientFirstName = WebUtility.HtmlEncode(model.RecipientFirstName);
        var detailUrl = WebUtility.HtmlEncode(model.DetailUrl);

        var noDeadlinesText = $"{localizer["Digest.NoDeadlines"].Value} {localizer["Digest.NiceWork"].Value}";
        var dueDateSuffixFormat = localizer["Digest.DueDateSuffix"].Value;

        var obligationRows = model.Obligations.Count == 0
            ? $"<p style=\"font-size: 13px; color: #A8B5B5;\">{noDeadlinesText}</p>"
            : string.Concat(model.Obligations.Select(o =>
                $"<li style=\"font-size: 13px; color: #5A6F6F; margin-bottom: 4px;\"><strong>{o.Status}</strong> — {o.Kind} for {WebUtility.HtmlEncode(o.StudentName)}{(o.DueDate.HasValue ? string.Format(dueDateSuffixFormat, FormatShortDate(o.DueDate.Value, language)) : "")}</li>"));

        var noMeetingsText = localizer["Digest.NoMeetings"].Value;
        var meetingRows = model.UpcomingMeetings.Count == 0
            ? $"<p style=\"font-size: 13px; color: #A8B5B5;\">{noMeetingsText}</p>"
            : string.Concat(model.UpcomingMeetings.Select(m =>
                $"<li style=\"font-size: 13px; color: #5A6F6F; margin-bottom: 4px;\">{WebUtility.HtmlEncode(m.Title)} for {WebUtility.HtmlEncode(m.StudentName)} — {FormatShortDateTime(m.StartsAtUtc, language)} ({WebUtility.HtmlEncode(m.TimeZoneId)})</li>"));

        var greetingHtml = string.Format(localizer["Digest.Greeting"].Value, recipientFirstName);
        var deadlinesHeading = localizer["Digest.DeadlinesHeadingHtml"].Value;
        var meetingsHeading = localizer["Digest.MeetingsHeadingHtml"].Value;
        var buttonText = localizer["Digest.ButtonText"].Value;

        return $@"
            <div style=""font-family: 'DM Sans', Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1E2A2A;"">IEP </span>
                    <span style=""font-family: 'Lora', Georgia, serif; font-size: 24px; color: #1A9478; font-weight: 600;"">Advisor</span>
                </div>
                <h1 style=""font-family: 'Lora', Georgia, serif; font-size: 22px; color: #1E2A2A; margin-bottom: 16px;"">{greetingHtml}</h1>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6; font-weight: 500;"">{deadlinesHeading}</p>
                <ul style=""padding-left: 18px; margin: 0 0 16px;"">{obligationRows}</ul>
                <p style=""font-size: 14px; color: #5A6F6F; line-height: 1.6; font-weight: 500;"">{meetingsHeading}</p>
                <ul style=""padding-left: 18px; margin: 0 0 16px;"">{meetingRows}</ul>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{detailUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #1A9478; color: white; text-decoration: none; border-radius: 8px; font-size: 14px; font-weight: 500;"">
                        {buttonText}
                    </a>
                </div>
                <hr style=""border: none; border-top: 1px solid #E8ECEC; margin: 24px 0;"" />
                <p style=""font-size: 11px; color: #A8B5B5; text-align: center;"">
                    IEP Advisor — Navigate with confidence
                </p>
            </div>";
    }

    // ----------------------------------------------------------------- Recipient language resolution

    /// <summary>
    /// Resolves the language to render <paramref name="toEmail"/>'s email in, and whether that address
    /// belongs to an existing account (plan 2026-10-06, phase 4, decision 5: culture per RECIPIENT, not
    /// sender — "the person reading it is who matters"). An existing account's own saved
    /// <see cref="Domain.Entities.User.PreferredLanguage"/> always wins (defaulting to English when unset,
    /// same as everywhere else in this codebase treats a null preference) — it is never overridden by
    /// whichever UI culture the current request/thread happens to be in, because that reflects the
    /// SENDER, a different person. Only a pre-account recipient (no <c>User</c> row for this address)
    /// falls back to <see cref="SupportedLanguages.ForRecipient"/> (effectively the sender's current
    /// request UI culture; English outside a request), because there is no other language signal for
    /// someone who has never set a preference and isn't the person driving this request.
    /// </summary>
    private async Task<(string Language, bool HasAccount)> ResolveRecipientAsync(string toEmail, CancellationToken ct)
    {
        var normalizedEmail = toEmail.Trim().ToLowerInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Email.ToLower() == normalizedEmail)
            .Select(u => new { u.PreferredLanguage })
            .FirstOrDefaultAsync(ct);

        if (user == null)
            return (SupportedLanguages.ForRecipient(null) ?? SupportedLanguages.English, false);

        return (SupportedLanguages.Normalize(user.PreferredLanguage) ?? SupportedLanguages.English, true);
    }

    /// <summary>Appends <c>?lang=</c>/<c>&amp;lang=</c> (respecting an existing query string) to a
    /// pre-account invite/landing link, so the web app's public pages — which already honor
    /// <c>?lang=</c> — open in the sender's language before anyone has signed in to save a preference of
    /// their own (plan 4, decision 2).</summary>
    private static string AppendLangParam(string url, string language)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}lang={Uri.EscapeDataString(language)}";
    }

    // ----------------------------------------------------------------- Explicit date/time formatting
    //
    // CurrentCulture stays English always by design (CultureScope touches only CurrentUICulture — see
    // its own doc comment), so every date/time shown to a Spanish recipient must be formatted against an
    // explicit culture rather than relying on ambient formatting. Verified against CultureInfo.GetCultureInfo
    // ("en") producing byte-identical output to the prior unculture-qualified ToString calls.

    private static (string DatePart, string TimePart) FormatDateTimeParts(DateTime value, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var datePart = language == SupportedLanguages.Spanish
            ? value.ToString("d 'de' MMMM 'de' yyyy", culture)
            : value.ToString("MMMM d, yyyy", culture);
        var timePart = value.ToString("h:mm tt", culture);
        return (datePart, timePart);
    }

    private static string FormatLongDate(DateTime value, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        return language == SupportedLanguages.Spanish
            ? value.ToString("d 'de' MMMM 'de' yyyy", culture)
            : value.ToString("MMMM d, yyyy", culture);
    }

    private static string FormatShortDate(DateTime value, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        return language == SupportedLanguages.Spanish
            ? value.ToString("d MMM yyyy", culture)
            : value.ToString("MMM d, yyyy", culture);
    }

    private static string FormatShortDateTime(DateTime value, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        return language == SupportedLanguages.Spanish
            ? value.ToString("d MMM, h:mm tt", culture)
            : value.ToString("MMM d, h:mm tt", culture);
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
