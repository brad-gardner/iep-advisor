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
/// Magic-link sign-in for staff invited as RelatedServiceProvider/GeneralEducator (pilot-gates plan,
/// phase 3, C11 adoption slice, decision 9). Mirrors the StaffInvite/ChildLink SHA-token pattern: a
/// 32-byte raw token is emailed, only its SHA-256 hash is stored (<see cref="InviteTokenHelper"/>), and
/// the token is single-use (claim-first <c>ExecuteUpdateAsync</c>, same race-loser gate as
/// <c>StaffInviteService.AcceptAsync</c>).
/// </summary>
public class MagicLinkService : IMagicLinkService
{
    private const int TokenExpiryMinutes = 15;
    private const int MaxRequestsPerWindow = 5;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);
    private static readonly int[] EligibleOrgRoleIds = { OrgRoleIds.RelatedServiceProvider, OrgRoleIds.GeneralEducator };

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly JwtTokenFactory _jwtTokenFactory;
    private readonly string _frontendUrl;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(
        ApplicationDbContext context,
        IEmailService emailService,
        JwtTokenFactory jwtTokenFactory,
        IConfiguration configuration,
        ILogger<MagicLinkService> logger)
    {
        _context = context;
        _emailService = emailService;
        _jwtTokenFactory = jwtTokenFactory;
        _frontendUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
        _logger = logger;
    }

    public async Task RequestAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return; // still a no-op from the caller's point of view — no enumeration either way.

        var normalizedEmail = email.Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail.ToLower() && u.IsActive, ct);
        if (user == null)
        {
            _logger.LogInformation("Magic-link requested for an unknown or inactive email — no email sent.");
            return;
        }

        var staffProfile = await _context.StaffProfiles
            .Include(sp => sp.District)
            .FirstOrDefaultAsync(sp => sp.UserId == user.Id && sp.IsActive && EligibleOrgRoleIds.Contains(sp.OrgRoleId), ct);
        if (staffProfile == null || !staffProfile.District.MagicLinkEnabled)
        {
            _logger.LogInformation("Magic-link requested for user {UserId}, who is not eligible — no email sent.", user.Id);
            return;
        }

        var rateLimitCutoff = DateTime.UtcNow - RateLimitWindow;
        var recentRequestCount = await _context.MagicLinkTokens
            .CountAsync(t => t.UserId == user.Id && t.CreatedAt > rateLimitCutoff, ct);
        if (recentRequestCount >= MaxRequestsPerWindow)
        {
            _logger.LogWarning("Magic-link request rate limit exceeded for user {UserId}.", user.Id);
            return;
        }

        var rawToken = InviteTokenHelper.Generate();
        await _context.MagicLinkTokens.AddAsync(new MagicLinkToken
        {
            UserId = user.Id,
            TokenHash = InviteTokenHelper.Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(TokenExpiryMinutes)
        }, ct);
        await _context.SaveChangesAsync(ct);

        var magicLinkUrl = $"{_frontendUrl}/auth/magic?token={Uri.EscapeDataString(rawToken)}";
        await _emailService.SendMagicLinkEmailAsync(user.Email, user.FirstName, magicLinkUrl, ct);
    }

    public async Task<MagicLinkConsumeResult> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return MagicLinkConsumeResult.Failure("Invalid or expired sign-in link.");

        var tokenHash = InviteTokenHelper.Hash(token);
        var record = await _context.MagicLinkTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (record == null || record.UsedAt != null || record.ExpiresAt <= DateTime.UtcNow)
            return MagicLinkConsumeResult.Failure("Invalid or expired sign-in link.");

        // Claim-first: atomic guarded update is the race-loser gate (mirrors StaffInviteService.AcceptAsync).
        var claimed = await _context.MagicLinkTokens
            .Where(t => t.Id == record.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);
        if (claimed == 0)
            return MagicLinkConsumeResult.Failure("This sign-in link has already been used.");

        var user = record.User;
        if (!user.IsActive)
            return MagicLinkConsumeResult.Failure("This account is not active.");

        // Same branch a password login takes when MFA is already enrolled — identical next step
        // (POST /api/auth/mfa/verify) for either entry point.
        if (user.MfaEnabled)
            return MagicLinkConsumeResult.MfaRequired(_jwtTokenFactory.CreateMfaPendingToken(user));

        var staffProfile = await _context.StaffProfiles
            .Include(sp => sp.District)
            .FirstOrDefaultAsync(sp => sp.UserId == user.Id && sp.IsActive, ct);
        var requireMfa = staffProfile?.District.RequireMfaForMagicLink ?? true;

        if (requireMfa)
        {
            // No login flow in this codebase forces MFA enrollment before granting a full session (a
            // password login for an unenrolled user also succeeds outright — see JwtTokenFactory doc),
            // so there is no existing "MFA setup required" shape to reuse here. This only refuses the
            // magic-link SHORTCUT against district policy; the user's password still works exactly as
            // it does today, unaffected by this district setting.
            _logger.LogInformation("Magic-link consume for user {UserId} refused — district requires MFA and none is enrolled.", user.Id);
            return MagicLinkConsumeResult.MfaSetupRequiredResult();
        }

        return MagicLinkConsumeResult.Ok(_jwtTokenFactory.CreateAuthResult(user));
    }
}
