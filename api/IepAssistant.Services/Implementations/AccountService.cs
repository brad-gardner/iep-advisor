using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class AccountService : IAccountService
{
    /// <summary>Grace period before <c>AccountPurgeWorker</c> is eligible to purge a pending deletion
    /// (pilot-gates plan, phase 2, decision 3) — kept as one constant so the email's stated purge date,
    /// the signed link's implicit validity window, and the worker's eligibility check can never drift.</summary>
    public const int DeletionGraceDays = 30;

    internal const string DeletionTokenPurpose = "account-deletion";

    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly ITotpService _totpService;
    private readonly MfaSecretProtector _protector;
    private readonly IEmailService _emailService;
    private readonly IDataProtector _deletionTokenProtector;
    private readonly string _frontendUrl;

    public AccountService(
        IUserRepository userRepository,
        ApplicationDbContext context,
        ITotpService totpService,
        MfaSecretProtector protector,
        IEmailService emailService,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _context = context;
        _totpService = totpService;
        _protector = protector;
        _emailService = emailService;
        _deletionTokenProtector = dataProtectionProvider.CreateProtector(DeletionTokenPurpose);
        _frontendUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    }

    public async Task<object> ExportDataAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return new { error = "User not found" };

        // ChildAccess is the authoritative authz plane (mirrors ChildProfileRepository): include
        // children where the user holds an accepted, active Owner ChildAccess, so a co-owner's
        // export covers co-owned children — not only those they are the denormalized primary owner of.
        var children = await _context.ChildProfiles
            .AsNoTracking()
            .Where(c => _context.Set<ChildAccess>()
                .Any(ca => ca.ChildProfileId == c.Id
                        && ca.UserId == userId
                        && ca.Role == AccessRole.Owner
                        && ca.IsActive
                        && ca.AcceptedAt != null))
            .ToListAsync(ct);

        var childIds = children.Select(c => c.Id).ToList();

        var documents = await _context.IepDocuments
            .AsNoTracking()
            .Where(d => childIds.Contains(d.ChildProfileId))
            .ToListAsync(ct);

        var documentIds = documents.Select(d => d.Id).ToList();

        var sections = await _context.IepSections
            .AsNoTracking()
            .Where(s => documentIds.Contains(s.IepDocumentId))
            .Include(s => s.Goals)
            .ToListAsync(ct);

        var analyses = await _context.IepAnalyses
            .AsNoTracking()
            .Where(a => documentIds.Contains(a.IepDocumentId))
            .ToListAsync(ct);

        var advocacyGoals = await _context.ParentAdvocacyGoals
            .AsNoTracking()
            .Where(g => childIds.Contains(g.ChildProfileId))
            .ToListAsync(ct);

        return new
        {
            exportDate = DateTime.UtcNow,
            profile = new
            {
                user.Email,
                user.FirstName,
                user.LastName,
                user.State,
                Role = user.Role.ToString(),
                user.MfaEnabled,
                user.CreatedAt,
                user.UpdatedAt
            },
            children = children.Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.DateOfBirth,
                c.GradeLevel,
                c.DisabilityCategory,
                c.SchoolDistrict,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt
            }),
            iepDocuments = documents.Select(d => new
            {
                d.Id,
                d.ChildProfileId,
                d.FileName,
                d.UploadDate,
                d.IepDate,
                d.MeetingType,
                d.Attendees,
                d.Notes,
                d.Status,
                d.FileSizeBytes,
                d.CreatedAt,
                d.UpdatedAt,
                sections = sections
                    .Where(s => s.IepDocumentId == d.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.SectionType,
                        s.RawText,
                        s.ParsedContent,
                        s.DisplayOrder,
                        goals = s.Goals.Select(g => new
                        {
                            g.Id,
                            g.GoalText,
                            g.Domain,
                            g.Baseline,
                            g.TargetCriteria,
                            g.MeasurementMethod,
                            g.Timeframe
                        })
                    }),
                analyses = analyses
                    .Where(a => a.IepDocumentId == d.Id)
                    .Select(a => new
                    {
                        a.Id,
                        a.Status,
                        a.SectionAnalyses,
                        a.GoalAnalyses,
                        a.OverallSummary,
                        a.OverallRedFlags,
                        a.AdvocacyGapAnalysis,
                        a.ParentGoalsSnapshot,
                        a.CreatedAt
                    })
            }),
            parentAdvocacyGoals = advocacyGoals.Select(g => new
            {
                g.Id,
                g.ChildProfileId,
                g.GoalText,
                g.Category,
                g.DisplayOrder,
                g.IsActive,
                g.CreatedAt,
                g.UpdatedAt
            })
        };
    }

    public async Task<ServiceResult> ScheduleDeletionAsync(int userId, string password, string? mfaCode, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return ServiceResult.FailureResult("Invalid password");

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(mfaCode))
                return ServiceResult.FailureResult("MFA code is required");

            var decryptedSecret = _protector.Unprotect(user.MfaSecret!);
            var valid = _totpService.ValidateCode(decryptedSecret, mfaCode);
            if (!valid)
                return ServiceResult.FailureResult("Invalid MFA code");
        }

        user.DeletionRequestedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.SecurityStamp++;
        _userRepository.Update(user);
        await _context.SaveChangesAsync(ct);

        // Deactivation above just invalidated this very session's token for every request after this
        // one, so the authenticated CancelDeletionAsync path is unreachable from here on — the signed
        // link is the only way back in. Not wrapped in try/catch: EnqueueAsync failing means the write
        // path itself is broken, which should surface as a failure rather than silently leave the user
        // unable to ever cancel.
        var purgeDate = user.DeletionRequestedAt.Value.AddDays(DeletionGraceDays);
        var token = _deletionTokenProtector.Protect($"{user.Id}|{user.DeletionRequestedAt.Value.Ticks}");
        var cancelUrl = $"{_frontendUrl}/account/cancel-deletion?token={Uri.EscapeDataString(token)}";
        await _emailService.SendAccountDeletionCancelLinkEmailAsync(user.Email, user.FirstName, cancelUrl, purgeDate, ct);

        return ServiceResult.SuccessResult("Account scheduled for deletion. You have 30 days to cancel.");
    }

    public async Task<ServiceResult> CancelDeletionAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found");

        if (user.DeletionRequestedAt == null)
            return ServiceResult.FailureResult("No pending deletion request");

        var daysSinceRequest = (DateTime.UtcNow - user.DeletionRequestedAt.Value).TotalDays;
        if (daysSinceRequest > DeletionGraceDays)
            return ServiceResult.FailureResult("Deletion grace period has expired");

        user.DeletionRequestedAt = null;
        user.IsActive = true;
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult("Account deletion cancelled. Your account is active again.");
    }

    public async Task<ServiceResult> CancelDeletionByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ServiceResult.FailureResult("Invalid or expired cancellation link.");

        int userId;
        long requestedAtTicks;
        try
        {
            var payload = _deletionTokenProtector.Unprotect(token);
            var parts = payload.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out userId) || !long.TryParse(parts[1], out requestedAtTicks))
                return ServiceResult.FailureResult("Invalid or expired cancellation link.");
        }
        catch
        {
            // IDataProtector.Unprotect throws (CryptographicException, FormatException, ...) on any
            // forged, corrupted, or garbage token. Never let the exact exception surface to an
            // anonymous caller.
            return ServiceResult.FailureResult("Invalid or expired cancellation link.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        // The embedded DeletionRequestedAt must match the CURRENT value exactly: it no longer will once
        // the request is cancelled (null), superseded by a later request (different ticks), or already
        // purged (parent: user gone entirely; staff: DeletionRequestedAt cleared by the purge worker) —
        // which is what makes an old token stop working without needing a separate hard expiry.
        if (user == null || user.DeletionRequestedAt == null || user.DeletionRequestedAt.Value.Ticks != requestedAtTicks)
            return ServiceResult.FailureResult("Invalid or expired cancellation link.");

        // Hard expiry independent of the purge worker: the link is good for the grace period only, so a
        // delayed purge never leaves an old email able to reactivate an account months later.
        var requestedAt = new DateTime(requestedAtTicks, DateTimeKind.Utc);
        if (DateTime.UtcNow - requestedAt > TimeSpan.FromDays(DeletionGraceDays))
            return ServiceResult.FailureResult("Invalid or expired cancellation link.");

        user.DeletionRequestedAt = null;
        user.IsActive = true;
        user.SecurityStamp++; // any token minted while deactivated should not remain usable after reactivation
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult("Account deletion cancelled. Your account is active again. Please sign in.");
    }
}
