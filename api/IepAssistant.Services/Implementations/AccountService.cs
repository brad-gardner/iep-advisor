using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class AccountService : IAccountService
{
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly ITotpService _totpService;
    private readonly MfaSecretProtector _protector;

    public AccountService(
        IUserRepository userRepository,
        ApplicationDbContext context,
        ITotpService totpService,
        MfaSecretProtector protector)
    {
        _userRepository = userRepository;
        _context = context;
        _totpService = totpService;
        _protector = protector;
    }

    public async Task<object> ExportDataAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return new { error = "User not found" };

        var children = await _context.ChildProfiles
            .AsNoTracking()
            .Where(c => c.UserId == userId)
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
                user.Role,
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
                        a.SuggestedQuestions,
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
        if (daysSinceRequest > 30)
            return ServiceResult.FailureResult("Deletion grace period has expired");

        user.DeletionRequestedAt = null;
        user.IsActive = true;
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult("Account deletion cancelled. Your account is active again.");
    }
}
