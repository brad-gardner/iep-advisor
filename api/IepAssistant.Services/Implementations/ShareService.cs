using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ShareService : IShareService
{
    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ShareService> _logger;

    public ShareService(
        ApplicationDbContext context,
        IAccessService accessService,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<ShareService> logger)
    {
        _context = context;
        _accessService = accessService;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ServiceResult<ChildAccessModel>> InviteAsync(int childId, int userId, string email, AccessRole role, CancellationToken ct = default)
    {
        // Verify the inviter is an owner
        var inviterRole = await _accessService.GetRoleAsync(childId, userId, ct);
        if (inviterRole != AccessRole.Owner)
            return ServiceResult<ChildAccessModel>.FailureResult("Only owners can invite users.");

        // Cannot invite as owner
        if (role == AccessRole.Owner)
            return ServiceResult<ChildAccessModel>.FailureResult("Cannot invite as owner.");

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
                return ServiceResult<ChildAccessModel>.FailureResult("This user already has access to this child.");
        }

        // Check for existing pending invite by email
        var existingInvite = await _context.ChildAccesses
            .AnyAsync(ca => ca.ChildProfileId == childId
                         && ca.InviteEmail == email
                         && ca.IsActive
                         && ca.AcceptedAt == null
                         && ca.InviteExpiresAt > DateTime.UtcNow, ct);
        if (existingInvite)
            return ServiceResult<ChildAccessModel>.FailureResult("A pending invite already exists for this email.");

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

        _logger.LogInformation("User {UserId} invited {Email} to child {ChildId} with role {Role}",
            userId, email, childId, role);

        // TODO: Send actual invite email when email service supports it
        _logger.LogInformation("Invite token for {Email}: {Token} (log-only — email not sent yet)", email, rawToken);

        var model = MapToModel(childAccess, inviteeUser);
        return ServiceResult<ChildAccessModel>.SuccessResult(model, "Invite sent successfully.");
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
            return ServiceResult.FailureResult("Invalid or expired invite token.");

        // Check if the user already has accepted access for this child
        var existingAccess = await _context.ChildAccesses
            .AnyAsync(ca => ca.ChildProfileId == invite.ChildProfileId
                         && ca.UserId == userId
                         && ca.IsActive
                         && ca.AcceptedAt != null, ct);
        if (existingAccess)
            return ServiceResult.FailureResult("You already have access to this child.");

        invite.UserId = userId;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.InviteToken = null; // Clear token — single use
        invite.UpdatedById = userId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} accepted invite for child {ChildId}", userId, invite.ChildProfileId);

        return ServiceResult.SuccessResult("Invite accepted successfully.");
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
            return ServiceResult.FailureResult("Only owners can revoke access.");

        var access = await _context.ChildAccesses
            .FirstOrDefaultAsync(ca => ca.Id == accessId
                                    && ca.ChildProfileId == childId
                                    && ca.IsActive, ct);

        if (access == null)
            return ServiceResult.FailureResult("Access record not found.");

        // Cannot revoke the last owner
        if (access.Role == AccessRole.Owner)
        {
            var ownerCount = await _context.ChildAccesses
                .CountAsync(ca => ca.ChildProfileId == childId
                              && ca.Role == AccessRole.Owner
                              && ca.IsActive
                              && ca.AcceptedAt != null, ct);
            if (ownerCount <= 1)
                return ServiceResult.FailureResult("Cannot revoke the last owner.");
        }

        access.IsActive = false;
        access.UpdatedById = userId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} revoked access {AccessId} for child {ChildId}",
            userId, accessId, childId);

        return ServiceResult.SuccessResult("Access revoked successfully.");
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
