using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Api.DTOs.Admin;
using IepAssistant.Api.DTOs.Auth;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Domain.Data;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardStats>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        // Users
        var totalUsers = await _db.Users.AsNoTracking().CountAsync(ct);
        var activeUsers = await _db.Users.AsNoTracking().CountAsync(u => u.IsActive, ct);
        var adminUsers = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Admin", ct);
        var usersWithSubscription = await _db.Users.AsNoTracking().CountAsync(u => u.SubscriptionStatus == "active", ct);
        var usersOnboarded = await _db.Users.AsNoTracking().CountAsync(u => u.OnboardingCompletedAt != null, ct);
        var newUsersLast7Days = await _db.Users.AsNoTracking().CountAsync(u => u.CreatedAt >= sevenDaysAgo, ct);

        var usersBySubscriptionStatus = await _db.Users.AsNoTracking()
            .GroupBy(u => u.SubscriptionStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        // Children
        var totalChildren = await _db.ChildProfiles.AsNoTracking().CountAsync(c => c.IsActive, ct);

        var childrenByDisabilityRaw = await _db.ChildProfiles.AsNoTracking()
            .Where(c => c.IsActive && c.DisabilityCategory != null)
            .GroupBy(c => c.DisabilityCategory!)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, ct);

        // IEP Documents
        var totalDocuments = await _db.IepDocuments.AsNoTracking().CountAsync(d => d.IsActive, ct);
        var documentsParsed = await _db.IepDocuments.AsNoTracking().CountAsync(d => d.IsActive && d.Status == "parsed", ct);
        var documentsCreated = await _db.IepDocuments.AsNoTracking().CountAsync(d => d.IsActive && d.Status == "created", ct);
        var documentsError = await _db.IepDocuments.AsNoTracking().CountAsync(d => d.IsActive && d.Status == "error", ct);
        var newDocumentsLast7Days = await _db.IepDocuments.AsNoTracking().CountAsync(d => d.IsActive && d.CreatedAt >= sevenDaysAgo, ct);

        var documentsByMeetingTypeRaw = await _db.IepDocuments.AsNoTracking()
            .Where(d => d.IsActive && d.MeetingType != null)
            .GroupBy(d => d.MeetingType!)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

        // Analyses
        var totalAnalyses = await _db.IepAnalyses.AsNoTracking().CountAsync(ct);
        var analysesCompleted = await _db.IepAnalyses.AsNoTracking().CountAsync(a => a.Status == "completed", ct);
        var analysesError = await _db.IepAnalyses.AsNoTracking().CountAsync(a => a.Status == "error", ct);
        var analysesLast7Days = await _db.IepAnalyses.AsNoTracking().CountAsync(a => a.CreatedAt >= sevenDaysAgo, ct);

        // Advocacy Goals
        var totalGoals = await _db.ParentAdvocacyGoals.AsNoTracking().CountAsync(g => g.IsActive, ct);

        var goalsByCategoryRaw = await _db.ParentAdvocacyGoals.AsNoTracking()
            .Where(g => g.IsActive && g.Category != null)
            .GroupBy(g => g.Category!)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, ct);

        // Meeting Prep
        var totalChecklists = await _db.MeetingPrepChecklists.AsNoTracking().CountAsync(c => c.IsActive, ct);
        var checklistsCompleted = await _db.MeetingPrepChecklists.AsNoTracking().CountAsync(c => c.IsActive && c.Status == "completed", ct);

        // Usage
        var totalAnalysisUsage = await _db.UsageRecords.AsNoTracking().CountAsync(u => u.OperationType == "analysis", ct);
        var totalMeetingPrepUsage = await _db.UsageRecords.AsNoTracking().CountAsync(u => u.OperationType == "meeting_prep", ct);

        // Beta Codes
        var totalBetaCodes = await _db.BetaInviteCodes.AsNoTracking().CountAsync(ct);
        var redeemedBetaCodes = await _db.BetaInviteCodes.AsNoTracking().CountAsync(b => b.RedeemedByUserId != null, ct);

        // Sharing
        var totalSharedAccess = await _db.ChildAccesses.AsNoTracking().CountAsync(ca => ca.IsActive, ct);

        var stats = new AdminDashboardStats
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            AdminUsers = adminUsers,
            UsersWithSubscription = usersWithSubscription,
            UsersOnboarded = usersOnboarded,
            TotalChildren = totalChildren,
            TotalDocuments = totalDocuments,
            DocumentsParsed = documentsParsed,
            DocumentsCreated = documentsCreated,
            DocumentsError = documentsError,
            TotalAnalyses = totalAnalyses,
            AnalysesCompleted = analysesCompleted,
            AnalysesError = analysesError,
            TotalGoals = totalGoals,
            TotalChecklists = totalChecklists,
            ChecklistsCompleted = checklistsCompleted,
            TotalAnalysisUsage = totalAnalysisUsage,
            TotalMeetingPrepUsage = totalMeetingPrepUsage,
            TotalBetaCodes = totalBetaCodes,
            RedeemedBetaCodes = redeemedBetaCodes,
            TotalSharedAccess = totalSharedAccess,
            NewUsersLast7Days = newUsersLast7Days,
            NewDocumentsLast7Days = newDocumentsLast7Days,
            AnalysesLast7Days = analysesLast7Days,
            DocumentsByMeetingType = documentsByMeetingTypeRaw,
            GoalsByCategory = goalsByCategoryRaw,
            ChildrenByDisabilityCategory = childrenByDisabilityRaw,
            UsersBySubscriptionStatus = usersBySubscriptionStatus,
        };

        return Ok(ApiResponse<AdminDashboardStats>.SuccessResponse(stats));
    }

    [HttpGet("recent-users")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentUsers([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                State = u.State,
                Role = u.Role,
                IsActive = u.IsActive,
                OnboardingCompleted = u.OnboardingCompletedAt != null,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
    }
}
