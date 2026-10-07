using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Multilingual plan (2026-10-06) phase 6 (todos/249): <see cref="ServiceErrorKind"/> added to every
/// failure for consistency with later-phase services, even though <c>ShareController</c> maps every one
/// of these to a fixed status today (not via <c>MapServiceFailure</c>) — see todos/249.
/// </summary>
public class ShareService : IShareService
{
    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ShareService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    public ShareService(
        ApplicationDbContext context,
        IAccessService accessService,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<ShareService> logger,
        IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _accessService = accessService;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<ServiceResult<ChildAccessModel>> InviteAsync(int childId, int userId, string email, AccessRole role, CancellationToken ct = default)
    {
        // Verify the inviter is an owner
        var inviterRole = await _accessService.GetRoleAsync(childId, userId, ct);
        if (inviterRole != AccessRole.Owner)
            return ServiceResult<ChildAccessModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Sharing.OnlyOwnersCanInvite"]);

        // Cannot invite as owner
        if (role == AccessRole.Owner)
            return ServiceResult<ChildAccessModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Sharing.CannotInviteAsOwner"]);

        // Check if invitee already has an account
        var inviteeUser = await _userRepository.GetByEmailAsync(email, ct);

        // Check for existing active access
        if (inviteeUser != null)
        {
            var existingAccess = await _context.ChildAccesses
                .AnyAsync(ca => ca.ChildProfileId == childId
                             && ca.UserId == inviteeUser.Id
                             && ca.IsActive, ct);
            if (existingAccess)
                return ServiceResult<ChildAccessModel>.FailureResult(ServiceErrorKind.Conflict, _localizer["Sharing.UserAlreadyHasAccess"]);
        }

        // Check for existing pending invite by email
        var existingInvite = await _context.ChildAccesses
            .AnyAsync(ca => ca.ChildProfileId == childId
                         && ca.InviteEmail == email
                         && ca.IsActive
                         && ca.AcceptedAt == null
                         && ca.InviteExpiresAt > DateTime.UtcNow, ct);
        if (existingInvite)
            return ServiceResult<ChildAccessModel>.FailureResult(ServiceErrorKind.Conflict, _localizer["Sharing.PendingInviteExistsForEmail"]);

        // Generate token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var childAccess = new ChildAccess
        {
            ChildProfileId = childId,
            UserId = inviteeUser?.Id,
            Role = role,
            InvitedByUserId = userId,
            InviteEmail = email,
            InviteToken = tokenHash,
            InviteExpiresAt = DateTime.UtcNow.AddDays(7),
            AcceptedAt = null,
            IsActive = true,
            CreatedById = userId,
            UpdatedById = userId
        };

        _context.ChildAccesses.Add(childAccess);
        await _context.SaveChangesAsync(ct);

        // Send invite email
        var inviter = await _context.Users.FindAsync(new object[] { userId }, ct);
        var child = await _context.ChildProfiles.FindAsync(new object[] { childId }, ct);
        var inviterName = inviter != null ? $"{inviter.FirstName} {inviter.LastName}" : "Someone";
        var childName = child != null ? child.FirstName : "their child";

        await _emailService.SendShareInviteEmailAsync(
            email, inviterName, childName, role.ToString(), rawToken, ct);

        _logger.LogInformation("Invite created and email sent for {Email} on child {ChildId} with role {Role}",
            email, childId, role);

        var model = MapToModel(childAccess, inviteeUser);
        return ServiceResult<ChildAccessModel>.SuccessResult(model, _localizer["Invites.SentSuccessfully"]);
    }

    public async Task<ServiceResult> AcceptInviteAsync(int userId, string token, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        var invite = await _context.ChildAccesses
            .FirstOrDefaultAsync(ca => ca.InviteToken == tokenHash
                                    && ca.IsActive
                                    && ca.AcceptedAt == null
                                    && ca.InviteExpiresAt > DateTime.UtcNow, ct);

        if (invite == null)
            return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["Invites.InvalidOrExpired"]);

        // Verify the accepting user's email matches the invite
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Auth.UserNotFound"]);

        if (!string.IsNullOrEmpty(invite.InviteEmail) &&
            !string.Equals(user.Email, invite.InviteEmail, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.FailureResult(ServiceErrorKind.Forbidden, _localizer["Invites.SentToDifferentEmail"]);

        // Check if the user already has accepted access for this child
        var existingAccess = await _context.ChildAccesses
            .AnyAsync(ca => ca.ChildProfileId == invite.ChildProfileId
                         && ca.UserId == userId
                         && ca.IsActive
                         && ca.AcceptedAt != null, ct);
        if (existingAccess)
            return ServiceResult.FailureResult(ServiceErrorKind.Conflict, _localizer["Sharing.AlreadyHaveAccess"]);

        invite.UserId = userId;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.InviteToken = null; // Clear token — single use
        invite.UpdatedById = userId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} accepted invite for child {ChildId}", userId, invite.ChildProfileId);

        return ServiceResult.SuccessResult(_localizer["Sharing.InviteAcceptedSuccessfully"]);
    }

    public async Task<IEnumerable<ChildAccessModel>> GetAccessListAsync(int childId, int userId, CancellationToken ct = default)
    {
        var role = await _accessService.GetRoleAsync(childId, userId, ct);
        if (role != AccessRole.Owner)
            return Enumerable.Empty<ChildAccessModel>();

        var accesses = await _context.ChildAccesses
            .Include(ca => ca.User)
            .Where(ca => ca.ChildProfileId == childId && ca.IsActive)
            .OrderByDescending(ca => ca.Role)
            .ThenBy(ca => ca.CreatedAt)
            .ToListAsync(ct);

        return accesses.Select(ca => MapToModel(ca, ca.User));
    }

    public async Task<ServiceResult> RevokeAccessAsync(int childId, int accessId, int userId, CancellationToken ct = default)
    {
        var role = await _accessService.GetRoleAsync(childId, userId, ct);
        if (role != AccessRole.Owner)
            return ServiceResult.FailureResult(ServiceErrorKind.Forbidden, _localizer["Sharing.OnlyOwnersCanRevoke"]);

        var access = await _context.ChildAccesses
            .FirstOrDefaultAsync(ca => ca.Id == accessId
                                    && ca.ChildProfileId == childId
                                    && ca.IsActive, ct);

        if (access == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Sharing.AccessRecordNotFound"]);

        // Cannot revoke the last owner
        if (access.Role == AccessRole.Owner)
        {
            var ownerCount = await _context.ChildAccesses
                .CountAsync(ca => ca.ChildProfileId == childId
                              && ca.Role == AccessRole.Owner
                              && ca.IsActive
                              && ca.AcceptedAt != null, ct);
            if (ownerCount <= 1)
                return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["Sharing.CannotRevokeLastOwner"]);
        }

        access.IsActive = false;
        access.UpdatedById = userId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} revoked access {AccessId} for child {ChildId}",
            userId, accessId, childId);

        return ServiceResult.SuccessResult(_localizer["Sharing.AccessRevokedSuccessfully"]);
    }

    private static ChildAccessModel MapToModel(ChildAccess access, User? user)
    {
        return new ChildAccessModel
        {
            Id = access.Id,
            ChildProfileId = access.ChildProfileId,
            UserId = access.UserId,
            UserEmail = user?.Email,
            UserName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
            InviteEmail = access.InviteEmail,
            Role = access.Role.ToString(),
            AcceptedAt = access.AcceptedAt,
            IsPending = access.AcceptedAt == null,
            CreatedAt = access.CreatedAt
        };
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
