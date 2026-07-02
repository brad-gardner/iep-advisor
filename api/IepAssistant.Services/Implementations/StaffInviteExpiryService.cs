using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Phase 3 pre-expiry reminder logic for pending staff invites (the I5–I8 correctness cluster). Warns the
/// inviting admin only, exactly once, when a still-pending invite is within 72h of expiring. Idempotency is
/// carried solely by <c>StaffInvite.ExpiryReminderSentAt</c>; <c>ResendAsync</c> nulls it so an extended
/// invite re-arms one fresh warning. Send is best-effort — <see cref="IEmailService"/> swallows ACS failures,
/// so a swallowed failure still consumes the reminder (accepted pilot risk; the dashboard invites tile is the
/// backstop). This service holds all decision logic so it can be unit-tested without the hosted worker.
/// </summary>
public class StaffInviteExpiryService : IStaffInviteExpiryService
{
    /// <summary>Reminder fires when an invite is within this many hours of expiring (~3 days).</summary>
    private const int ReminderWindowHours = 72;

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<StaffInviteExpiryService> _logger;

    public StaffInviteExpiryService(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<StaffInviteExpiryService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> FindExpiringInviteIdsAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var windowEnd = utcNow.AddHours(ReminderWindowHours);

        // Candidate window: utcNow < InviteExpiresAt <= utcNow + 72h, pending, un-reminded. The strict lower
        // bound (> utcNow) excludes already-expired invites entirely, so a first-deploy backlog is never mailed.
        return await _context.StaffInvites.AsNoTracking()
            .Where(i => i.AcceptedAt == null
                     && i.IsActive
                     && i.ExpiryReminderSentAt == null
                     && i.InviteExpiresAt > utcNow
                     && i.InviteExpiresAt <= windowEnd)
            .Select(i => i.Id)
            .ToListAsync(ct);
    }

    public async Task ProcessInviteAsync(int inviteId, DateTime utcNow, CancellationToken ct = default)
    {
        var windowEnd = utcNow.AddHours(ReminderWindowHours);

        // Re-load fresh (race guard): a concurrent ResendAsync may have extended expiry and nulled the stamp,
        // or an accept/revoke may have closed the invite, since the candidate scan ran.
        var invite = await _context.StaffInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inviteId, ct);
        if (invite == null)
            return;

        // Re-verify the full candidate condition immediately before sending.
        if (invite.AcceptedAt != null
            || !invite.IsActive
            || invite.ExpiryReminderSentAt != null
            || invite.InviteExpiresAt <= utcNow
            || invite.InviteExpiresAt > windowEnd)
        {
            return;
        }

        // Inviting admin must still be an active staff member. Scoped to the invite's district so the check
        // stays correct even if the "one StaffProfile per user" invariant ever relaxes (multi-district staff).
        var inviterActive = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.UserId == invite.InvitedByUserId && p.DistrictId == invite.DistrictId)
            .Select(p => (bool?)p.IsActive)
            .FirstOrDefaultAsync(ct);
        if (inviterActive != true)
        {
            _logger.LogInformation(
                "Expiry reminder skipped for invite {InviteId}: inviting admin (user {UserId}) is not an active staff member.",
                inviteId, invite.InvitedByUserId);
            return;
        }

        // The invite's school (if school-scoped) must still be active. One read resolves both the active
        // guard and the name used in the email body.
        string? schoolName = null;
        if (invite.SchoolId != null)
        {
            var school = await _context.Schools.AsNoTracking()
                .Where(s => s.Id == invite.SchoolId.Value)
                .Select(s => new { s.IsActive, s.Name })
                .FirstOrDefaultAsync(ct);
            if (school is null || !school.IsActive)
            {
                _logger.LogInformation(
                    "Expiry reminder skipped for invite {InviteId}: school {SchoolId} is deactivated or missing.",
                    inviteId, invite.SchoolId);
                return;
            }
            schoolName = school.Name;
        }

        // Recipient is the INVITER's own email — never the invitee's.
        var inviterEmail = await _context.Users.AsNoTracking()
            .Where(u => u.Id == invite.InvitedByUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(inviterEmail))
        {
            _logger.LogWarning(
                "Expiry reminder skipped for invite {InviteId}: inviting admin (user {UserId}) has no email on file.",
                inviteId, invite.InvitedByUserId);
            return;
        }

        var districtName = await _context.Districts.AsNoTracking()
            .Where(d => d.Id == invite.DistrictId).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? "your district";

        // Send first — best-effort, EmailService swallows ACS failures internally (we add no throwing behavior).
        await _emailService.SendStaffInviteExpiringEmailAsync(
            inviterEmail, invite.Email, districtName, schoolName, invite.InviteExpiresAt, ct);

        // Guarded stamp: only marks reminded if the invite STILL sits in the reminder window and is
        // un-reminded/pending. A concurrent resend that extended expiry (InviteExpiresAt now > windowEnd) or
        // an accept/revoke makes this match zero rows, so the resend's fresh warning window is never clobbered.
        var stamped = await _context.StaffInvites
            .Where(i => i.Id == inviteId
                     && i.ExpiryReminderSentAt == null
                     && i.AcceptedAt == null
                     && i.IsActive
                     && i.InviteExpiresAt > utcNow
                     && i.InviteExpiresAt <= windowEnd)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiryReminderSentAt, utcNow), ct);

        if (stamped == 0)
        {
            _logger.LogInformation(
                "Expiry reminder emailed for invite {InviteId} but stamp skipped (concurrent resend/accept/revoke moved it out of the window).",
                inviteId);
        }
        else
        {
            _logger.LogInformation(
                "Expiry reminder sent to inviting admin for invite {InviteId} (invitee {Invitee}, expires {ExpiresAt:u}).",
                inviteId, invite.Email, invite.InviteExpiresAt);
        }
    }

    public async Task ProcessExpiringInvitesAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var ids = await FindExpiringInviteIdsAsync(utcNow, ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ProcessInviteAsync(id, utcNow, ct);
            }
            catch (Exception ex)
            {
                // Isolation: one bad invite must never abort the batch.
                _logger.LogError(ex, "Failed to process expiry reminder for staff invite {InviteId}.", id);
            }
        }
    }
}
