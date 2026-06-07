using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// P4 staff invites + staff management. Mirrors the <c>StudentInvite</c>/<c>ChildLink</c> token pattern
/// (32-byte raw token emailed, SHA-256 hash stored, single-use, email-bound, 14-day). Org authorization
/// is resolved per-request from the caller's active <see cref="StaffContext"/>:
/// <list type="bullet">
///   <item>DistrictAdmin invites any of the 3 roles (DistrictAdmin invite ⇒ SchoolId null;
///         SchoolAdmin/Teacher invite ⇒ SchoolId required, active, in caller's district).</item>
///   <item>SchoolAdmin invites SchoolAdmin/Teacher into their OWN school only.</item>
///   <item>Teacher: denied.</item>
/// </list>
/// Accept is anonymous, transactional (claim-first), and mints a JWT.
/// </summary>
public class StaffInviteService : IStaffInviteService
{
    private const int InviteExpiryDays = 14;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IEmailService _emailService;
    private readonly JwtTokenFactory _jwtTokenFactory;
    private readonly InviteLinkExposure _linkExposure;
    private readonly string _frontendUrl;
    private readonly ILogger<StaffInviteService> _logger;

    public StaffInviteService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IEmailService emailService,
        JwtTokenFactory jwtTokenFactory,
        InviteLinkExposure linkExposure,
        IConfiguration configuration,
        ILogger<StaffInviteService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _emailService = emailService;
        _jwtTokenFactory = jwtTokenFactory;
        _linkExposure = linkExposure;
        _frontendUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
        _logger = logger;
    }

    // ================================================================= Invite

    public async Task<ServiceResult<StaffInviteModel>> InviteAsync(int callerUserId, CreateStaffInviteModel model, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult<StaffInviteModel>.FailureResult("Staff profile not found.");

        if (string.IsNullOrWhiteSpace(model.Email))
            return ServiceResult<StaffInviteModel>.FailureResult("Email is required.");
        var email = model.Email.Trim();
        if (email.Length > 256)
            return ServiceResult<StaffInviteModel>.FailureResult("Email must be 256 characters or fewer.");

        if (model.OrgRoleId is not (OrgRoleIds.DistrictAdmin or OrgRoleIds.SchoolAdmin or OrgRoleIds.Teacher))
            return ServiceResult<StaffInviteModel>.FailureResult("Invalid org role.");

        // ------- Caller role gate + school resolution -------
        int? schoolId;
        if (caller.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            if (model.OrgRoleId == OrgRoleIds.DistrictAdmin)
            {
                // DistrictAdmin invite is district-scoped only; a school target is meaningless.
                if (model.SchoolId != null)
                    return ServiceResult<StaffInviteModel>.FailureResult("A District Admin invite must not specify a school.");
                schoolId = null;
            }
            else
            {
                if (model.SchoolId == null)
                    return ServiceResult<StaffInviteModel>.FailureResult("A school is required for School Admin and Teacher invites.");

                var schoolOk = await _context.Schools.AsNoTracking()
                    .AnyAsync(s => s.Id == model.SchoolId.Value && s.DistrictId == caller.DistrictId && s.IsActive, ct);
                if (!schoolOk)
                    return ServiceResult<StaffInviteModel>.FailureResult("School not found.");
                schoolId = model.SchoolId.Value;
            }
        }
        else if (caller.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            // SchoolAdmin may invite SchoolAdmin/Teacher only, and only into their OWN school.
            if (model.OrgRoleId == OrgRoleIds.DistrictAdmin)
                return ServiceResult<StaffInviteModel>.FailureResult("You do not have permission to invite a District Admin.");
            if (caller.SchoolId == null)
                return ServiceResult<StaffInviteModel>.FailureResult("Your account is not assigned to a school.");
            // Force/validate the school to the caller's own; a mismatched explicit school is denied.
            if (model.SchoolId != null && model.SchoolId.Value != caller.SchoolId.Value)
                return ServiceResult<StaffInviteModel>.FailureResult("You do not have permission to invite staff to another school.");
            schoolId = caller.SchoolId.Value;
        }
        else
        {
            return ServiceResult<StaffInviteModel>.FailureResult("You do not have permission to invite staff.");
        }

        // ------- Rejections that must precede token generation -------
        // Any existing user account on this email cannot be (re)used for staff — work-email guidance.
        var emailHasAccount = await _context.Users.AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (emailHasAccount)
            return ServiceResult<StaffInviteModel>.FailureResult(
                "That email already has an account. Staff must be invited with an email that isn't already registered — please use your work email.");

        // Global duplicate-pending guard (single-role world): one live staff invite per email anywhere.
        // A "live" row (IsActive && AcceptedAt == null && InviteToken != null) occupies the filtered unique
        // index slot regardless of expiry, so we fetch it tracked and branch:
        //   - still-valid (not expired)  → reject as a duplicate;
        //   - expired                    → REFRESH that same row in place (new token/expiry/role/school),
        //                                   which keeps the one-row-per-email invariant the index enforces.
        var now = DateTime.UtcNow;
        var existingLive = await _context.StaffInvites
            .FirstOrDefaultAsync(i => i.Email.ToLower() == email.ToLower()
                        && i.IsActive
                        && i.AcceptedAt == null
                        && i.InviteToken != null, ct);
        if (existingLive != null && existingLive.InviteExpiresAt > now)
            return ServiceResult<StaffInviteModel>.FailureResult("That email has already been invited.");

        // ------- Create (or refresh an expired row) + email -------
        var rawToken = InviteTokenHelper.Generate();
        StaffInvite invite;
        if (existingLive != null)
        {
            // Reuse the expired live row so the filtered unique index is never tripped by a second insert.
            invite = existingLive;
            invite.DistrictId = caller.DistrictId;
            invite.SchoolId = schoolId;
            invite.OrgRoleId = model.OrgRoleId;
            invite.InviteToken = InviteTokenHelper.Hash(rawToken);
            invite.InviteExpiresAt = now.AddDays(InviteExpiryDays);
            invite.InvitedByUserId = callerUserId;
            invite.UpdatedById = callerUserId;
        }
        else
        {
            invite = new StaffInvite
            {
                Email = email,
                DistrictId = caller.DistrictId,
                SchoolId = schoolId,
                OrgRoleId = model.OrgRoleId,
                InviteToken = InviteTokenHelper.Hash(rawToken),
                InviteExpiresAt = now.AddDays(InviteExpiryDays),
                InvitedByUserId = callerUserId,
                IsActive = true,
                CreatedById = callerUserId,
                UpdatedById = callerUserId
            };
            await _context.StaffInvites.AddAsync(invite, ct);
        }
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // DB backstop for the pre-check above: the filtered unique index on Email (live-pending only)
            // rejected a concurrent duplicate that slipped past the check-then-insert window. Detach the
            // unsaved entity and surface the SAME friendly message the pre-check does.
            _context.Entry(invite).State = EntityState.Detached;
            return ServiceResult<StaffInviteModel>.FailureResult("That email has already been invited.");
        }

        var (districtName, schoolName, roleName) = await ResolveNamesAsync(invite.DistrictId, invite.SchoolId, invite.OrgRoleId, ct);
        await _emailService.SendStaffInviteEmailAsync(email, districtName, schoolName, roleName, rawToken, ct);

        _logger.LogInformation("Staff invite {InviteId} created for {Email} (role {OrgRoleId}, school {SchoolId}) by user {CallerId}",
            invite.Id, email, invite.OrgRoleId, invite.SchoolId, callerUserId);

        return ServiceResult<StaffInviteModel>.SuccessResult(
            MapInvite(invite, districtName, schoolName, roleName, rawToken), "Invite sent successfully.");
    }

    // ================================================================= List

    public async Task<ServiceResult<StaffListModel>> ListAsync(int callerUserId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult<StaffListModel>.FailureResult("Staff profile not found.");

        if (caller.OrgRoleId == OrgRoleIds.Teacher)
            return ServiceResult<StaffListModel>.FailureResult("You do not have permission to view the staff list.");

        var isDistrictAdmin = caller.OrgRoleId == OrgRoleIds.DistrictAdmin;
        var now = DateTime.UtcNow;

        // ------- Members (active + inactive StaffProfiles) -------
        var membersQuery = _context.StaffProfiles.AsNoTracking()
            .Where(p => p.DistrictId == caller.DistrictId);
        if (!isDistrictAdmin)
        {
            // SchoolAdmin: own school only, and DistrictAdmin (school-null) entries hidden.
            membersQuery = membersQuery.Where(p => p.SchoolId == caller.SchoolId && p.OrgRoleId != OrgRoleIds.DistrictAdmin);
        }

        var members = await membersQuery
            .OrderBy(p => p.User.LastName).ThenBy(p => p.User.FirstName)
            .Select(p => new StaffMemberModel
            {
                StaffProfileId = p.Id,
                UserId = p.UserId,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                Email = p.User.Email,
                OrgRoleId = p.OrgRoleId,
                OrgRoleName = p.OrgRole.Name,
                SchoolId = p.SchoolId,
                SchoolName = p.School != null ? p.School.Name : null,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        // ------- Pending + expired invites (not accepted, still active) -------
        var invitesQuery = _context.StaffInvites.AsNoTracking()
            .Where(i => i.DistrictId == caller.DistrictId && i.IsActive && i.AcceptedAt == null);
        if (!isDistrictAdmin)
            invitesQuery = invitesQuery.Where(i => i.SchoolId == caller.SchoolId && i.OrgRoleId != OrgRoleIds.DistrictAdmin);

        var invites = await invitesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new StaffPendingInviteModel
            {
                Id = i.Id,
                Email = i.Email,
                OrgRoleId = i.OrgRoleId,
                OrgRoleName = i.OrgRole.Name,
                SchoolId = i.SchoolId,
                SchoolName = i.School != null ? i.School.Name : null,
                InviteExpiresAt = i.InviteExpiresAt,
                Status = i.InviteExpiresAt > now ? "pending" : "expired"
            })
            .ToListAsync(ct);

        return ServiceResult<StaffListModel>.SuccessResult(new StaffListModel
        {
            Members = members,
            PendingInvites = invites
        });
    }

    // ================================================================= Revoke

    public async Task<ServiceResult> RevokeAsync(int callerUserId, int inviteId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult.FailureResult("Staff profile not found.");

        var invite = await _context.StaffInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.DistrictId == caller.DistrictId, ct);
        if (invite == null)
            return ServiceResult.FailureResult("Invite not found.");

        var scope = CheckInviteScope(caller, invite.SchoolId, invite.OrgRoleId);
        if (!scope.Success)
            return scope;

        if (!invite.IsActive || invite.AcceptedAt != null)
            return ServiceResult.SuccessResult("Invite is no longer pending.");

        invite.IsActive = false;
        invite.InviteToken = null; // dead token can't preview/accept after revoke
        invite.UpdatedById = callerUserId;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff invite {InviteId} revoked by user {CallerId}", inviteId, callerUserId);
        return ServiceResult.SuccessResult("Invite revoked.");
    }

    // ================================================================= Resend

    public async Task<ServiceResult<StaffInviteModel>> ResendAsync(int callerUserId, int inviteId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult<StaffInviteModel>.FailureResult("Staff profile not found.");

        var invite = await _context.StaffInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.DistrictId == caller.DistrictId, ct);
        if (invite == null)
            return ServiceResult<StaffInviteModel>.FailureResult("Invite not found.");

        var scope = CheckInviteScope(caller, invite.SchoolId, invite.OrgRoleId);
        if (!scope.Success)
            return ServiceResult<StaffInviteModel>.FailureResult(scope.Message!);

        if (!invite.IsActive || invite.AcceptedAt != null)
            return ServiceResult<StaffInviteModel>.FailureResult("Invite is no longer pending.");

        // New token + fresh clock on the SAME row; the old raw token stops working immediately.
        var rawToken = InviteTokenHelper.Generate();
        invite.InviteToken = InviteTokenHelper.Hash(rawToken);
        invite.InviteExpiresAt = DateTime.UtcNow.AddDays(InviteExpiryDays);
        invite.UpdatedById = callerUserId;
        await _context.SaveChangesAsync(ct);

        var (districtName, schoolName, roleName) = await ResolveNamesAsync(invite.DistrictId, invite.SchoolId, invite.OrgRoleId, ct);
        await _emailService.SendStaffInviteEmailAsync(invite.Email, districtName, schoolName, roleName, rawToken, ct);

        _logger.LogInformation("Staff invite {InviteId} resent by user {CallerId}", inviteId, callerUserId);
        return ServiceResult<StaffInviteModel>.SuccessResult(
            MapInvite(invite, districtName, schoolName, roleName, rawToken), "Invite resent.");
    }

    // ================================================================= Deactivate / reactivate staff

    public async Task<ServiceResult> DeactivateStaffAsync(int callerUserId, int staffProfileId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult.FailureResult("Staff profile not found.");

        var target = await _context.StaffProfiles
            .FirstOrDefaultAsync(p => p.Id == staffProfileId && p.DistrictId == caller.DistrictId, ct);
        if (target == null)
            return ServiceResult.FailureResult("Staff member not found.");

        var scope = CheckStaffMutationScope(caller, target);
        if (!scope.Success)
            return scope;

        if (!target.IsActive)
            return ServiceResult.SuccessResult("Staff member is already deactivated.");

        // Last-admin guard: never strip a district of its final active DistrictAdmin (incl. self).
        if (target.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            var activeAdmins = await _context.StaffProfiles.AsNoTracking()
                .CountAsync(p => p.DistrictId == target.DistrictId
                              && p.OrgRoleId == OrgRoleIds.DistrictAdmin
                              && p.IsActive, ct);
            if (activeAdmins <= 1)
                return ServiceResult.FailureResult("You cannot deactivate the last active District Admin of the district.");
        }

        target.IsActive = false;
        target.UpdatedById = callerUserId;

        // Bump SecurityStamp so the staff member's live JWT fails on its next request (Program.cs check).
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == target.UserId, ct);
        if (user != null)
        {
            user.SecurityStamp = unchecked(user.SecurityStamp + 1);
            user.UpdatedById = callerUserId;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff profile {StaffProfileId} (user {UserId}) deactivated by user {CallerId}",
            staffProfileId, target.UserId, callerUserId);
        return ServiceResult.SuccessResult("Staff member deactivated.");
    }

    public async Task<ServiceResult> ReactivateStaffAsync(int callerUserId, int staffProfileId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(callerUserId, ct);
        if (caller == null)
            return ServiceResult.FailureResult("Staff profile not found.");

        var target = await _context.StaffProfiles
            .FirstOrDefaultAsync(p => p.Id == staffProfileId && p.DistrictId == caller.DistrictId, ct);
        if (target == null)
            return ServiceResult.FailureResult("Staff member not found.");

        var scope = CheckStaffMutationScope(caller, target);
        if (!scope.Success)
            return scope;

        if (target.IsActive)
            return ServiceResult.SuccessResult("Staff member is already active.");

        target.IsActive = true;
        target.UpdatedById = callerUserId;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff profile {StaffProfileId} reactivated by user {CallerId}", staffProfileId, callerUserId);
        return ServiceResult.SuccessResult("Staff member reactivated.");
    }

    // ================================================================= Preview (anonymous)

    public async Task<StaffInvitePreviewModel?> PreviewAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = InviteTokenHelper.Hash(token);
        // Look up by hash + active + unaccepted; expired vs valid is distinguished below. A null/missing
        // row (claimed/revoked/unknown) collapses to "invalid" with no email enumeration.
        var invite = await _context.StaffInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InviteToken == tokenHash && i.IsActive && i.AcceptedAt == null, ct);
        if (invite == null)
            return new StaffInvitePreviewModel { Status = "invalid" };

        var (districtName, schoolName, roleName) = await ResolveNamesAsync(invite.DistrictId, invite.SchoolId, invite.OrgRoleId, ct);

        return new StaffInvitePreviewModel
        {
            DistrictName = districtName,
            SchoolName = schoolName,
            RoleName = roleName,
            Email = invite.Email,
            Status = invite.InviteExpiresAt > DateTime.UtcNow ? "valid" : "expired"
        };
    }

    // ================================================================= Accept (anonymous)

    public async Task<AcceptStaffInviteResult> AcceptAsync(AcceptStaffInviteModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Token))
            return AcceptStaffInviteResult.Failure("Invalid or expired invite.");
        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
            return AcceptStaffInviteResult.Failure("First and last name are required.");
        if (string.IsNullOrWhiteSpace(model.Password))
            return AcceptStaffInviteResult.Failure("A password is required.");

        var tokenHash = InviteTokenHelper.Hash(model.Token);

        // Resolve the invite (active + unaccepted). Distinguish expired from invalid.
        var invite = await _context.StaffInvites
            .FirstOrDefaultAsync(i => i.InviteToken == tokenHash && i.IsActive && i.AcceptedAt == null, ct);
        if (invite == null)
            return AcceptStaffInviteResult.Failure("This invite is no longer valid.");
        if (invite.InviteExpiresAt <= DateTime.UtcNow)
            return AcceptStaffInviteResult.Failure("This invite has expired.");

        var email = invite.Email;

        // Email-already-registered guard (covers parent-registered-AFTER-invite). Checked before the
        // claim so a rejected accept never burns the token.
        var emailHasAccount = await _context.Users.AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (emailHasAccount)
            return AcceptStaffInviteResult.Failure(
                "An account already exists for this email. This invite can't be used with an existing account.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;

            // CLAIM FIRST: atomic guarded update is the race-loser gate. Only the request that flips an
            // as-yet-unclaimed invite proceeds; others see 0 rows and roll back with "already claimed".
            var claimed = await _context.StaffInvites
                .Where(i => i.Id == invite.Id
                         && i.InviteToken == tokenHash
                         && i.AcceptedAt == null
                         && i.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.AcceptedAt, now)
                    .SetProperty(i => i.InviteToken, (string?)null), ct);
            if (claimed == 0)
            {
                await transaction.RollbackAsync(ct);
                return AcceptStaffInviteResult.Failure("This invite has already been used.");
            }

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Role = UserRole.Educator,
                IsActive = true,
                SubscriptionStatus = "active",
                SubscriptionExpiresAt = now.AddYears(1),
                CreatedAt = now,
                UpdatedAt = now
            };
            await _context.Users.AddAsync(user, ct);
            await _context.SaveChangesAsync(ct);

            var staffProfile = new StaffProfile
            {
                UserId = user.Id,
                DistrictId = invite.DistrictId,
                SchoolId = invite.SchoolId,
                OrgRoleId = invite.OrgRoleId,
                IsActive = true,
                CreatedById = user.Id,
                UpdatedById = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _context.StaffProfiles.AddAsync(staffProfile, ct);

            // Record who claimed it (the claim already set AcceptedAt + nulled the token out-of-band).
            await _context.StaffInvites
                .Where(i => i.Id == invite.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.AcceptedByUserId, user.Id), ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Staff invite {InviteId} accepted; created user {UserId} + staff profile {StaffProfileId}",
                invite.Id, user.Id, staffProfile.Id);

            return AcceptStaffInviteResult.Ok(_jwtTokenFactory.CreateAuthResult(user));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // ================================================================= Helpers

    /// <summary>Scope check for revoke/resend against an invite's school + role.</summary>
    private static ServiceResult CheckInviteScope(StaffContext caller, int? inviteSchoolId, int inviteOrgRoleId)
    {
        if (caller.OrgRoleId == OrgRoleIds.DistrictAdmin)
            return ServiceResult.SuccessResult();

        if (caller.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            if (inviteOrgRoleId == OrgRoleIds.DistrictAdmin)
                return ServiceResult.FailureResult("You do not have permission to manage District Admin invites.");
            if (caller.SchoolId == null || inviteSchoolId != caller.SchoolId)
                return ServiceResult.FailureResult("You do not have permission to manage invites for another school.");
            return ServiceResult.SuccessResult();
        }

        return ServiceResult.FailureResult("You do not have permission to manage staff invites.");
    }

    /// <summary>Scope check for deactivate/reactivate against a target StaffProfile.</summary>
    private static ServiceResult CheckStaffMutationScope(StaffContext caller, StaffProfile target)
    {
        if (caller.OrgRoleId == OrgRoleIds.DistrictAdmin)
            return ServiceResult.SuccessResult(); // any staff in the district (subject to last-admin guard)

        if (caller.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            // Own-school SchoolAdmin/Teacher only; never a DistrictAdmin or other-school staff.
            if (target.OrgRoleId == OrgRoleIds.DistrictAdmin)
                return ServiceResult.FailureResult("You do not have permission to manage a District Admin.");
            if (caller.SchoolId == null || target.SchoolId != caller.SchoolId)
                return ServiceResult.FailureResult("You do not have permission to manage staff at another school.");
            return ServiceResult.SuccessResult();
        }

        return ServiceResult.FailureResult("You do not have permission to manage staff.");
    }

    private async Task<(string districtName, string? schoolName, string roleName)> ResolveNamesAsync(
        int districtId, int? schoolId, int orgRoleId, CancellationToken ct)
    {
        var districtName = await _context.Districts.AsNoTracking()
            .Where(d => d.Id == districtId).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? "your district";

        string? schoolName = null;
        if (schoolId != null)
            schoolName = await _context.Schools.AsNoTracking()
                .Where(s => s.Id == schoolId.Value).Select(s => s.Name).FirstOrDefaultAsync(ct);

        var roleName = await _context.OrgRoles.AsNoTracking()
            .Where(r => r.Id == orgRoleId).Select(r => r.Name).FirstOrDefaultAsync(ct) ?? "Staff";

        return (districtName, schoolName, roleName);
    }

    private StaffInviteModel MapInvite(StaffInvite invite, string districtName, string? schoolName, string roleName, string rawToken) => new()
    {
        Id = invite.Id,
        Email = invite.Email,
        OrgRoleId = invite.OrgRoleId,
        OrgRoleName = roleName,
        SchoolId = invite.SchoolId,
        SchoolName = schoolName,
        InviteExpiresAt = invite.InviteExpiresAt,
        // Gated: only surface the raw accept URL when the testing exposure is enabled.
        InviteUrl = _linkExposure.Enabled
            ? $"{_frontendUrl}/staff/accept-invite?token={Uri.EscapeDataString(rawToken)}"
            : null
    };
}
