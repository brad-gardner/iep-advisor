using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Api.DTOs.Admin;
using IepAssistant.Api.DTOs.Auth;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Api.DTOs.Notifications;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IAuditIntegrityService _auditIntegrityService;
    private readonly IEmailTransport _emailTransport;

    public AdminController(
        ApplicationDbContext db,
        INotificationService notificationService,
        IAuditIntegrityService auditIntegrityService,
        IEmailTransport emailTransport)
    {
        _db = db;
        _notificationService = notificationService;
        _auditIntegrityService = auditIntegrityService;
        _emailTransport = emailTransport;
    }

    // ----------------------------------------------------------------- Pilot-gates plan, phase 1: audit integrity

    /// <summary>Runs the hash-chain integrity walk synchronously and returns its outcome (in addition to
    /// the nightly <c>AuditIntegrityWorker</c> run).</summary>
    [HttpPost("audit/integrity-check")]
    [ProducesResponseType(typeof(ApiResponse<AuditIntegrityRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunAuditIntegrityCheck(CancellationToken ct)
    {
        var result = await _auditIntegrityService.RunCheckAsync(ct);
        return Ok(ApiResponse<AuditIntegrityRunDto>.SuccessResponse(MapIntegrityRun(result)));
    }

    /// <summary>The last 10 integrity runs, newest first.</summary>
    [HttpGet("audit/integrity")]
    [ProducesResponseType(typeof(ApiResponse<List<AuditIntegrityRunDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditIntegrityRuns(CancellationToken ct)
    {
        var runs = await _auditIntegrityService.GetRecentRunsAsync(10, ct);
        return Ok(ApiResponse<List<AuditIntegrityRunDto>>.SuccessResponse(runs.Select(MapIntegrityRun).ToList()));
    }

    private static AuditIntegrityRunDto MapIntegrityRun(Services.Models.AuditIntegrityRunModel model) => new()
    {
        Id = model.Id,
        StartedAt = model.StartedAt,
        CompletedAt = model.CompletedAt,
        RowsChecked = model.RowsChecked,
        FirstBrokenId = model.FirstBrokenId,
        Status = model.Status,
        Detail = model.Detail
    };

    // ----------------------------------------------------------------- Pilot-gates plan, phase 1: outbound email

    /// <summary>Outbound email rows, optionally filtered by status, newest first.</summary>
    [HttpGet("email")]
    [ProducesResponseType(typeof(ApiResponse<List<OutboundEmailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboundEmails([FromQuery] string status = "All", [FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);

        var query = _db.OutboundEmails.AsNoTracking();
        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<OutboundEmailStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(ApiResponse<object>.Error($"Invalid status: {status}"));
            query = query.Where(e => e.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new OutboundEmailDto
            {
                Id = e.Id,
                ToEmail = e.ToEmail,
                Subject = e.Subject,
                Kind = e.Kind,
                Status = e.Status.ToString(),
                Attempts = e.Attempts,
                LastError = e.LastError,
                NextAttemptAt = e.NextAttemptAt,
                SentAt = e.SentAt,
                CorrelationId = e.CorrelationId,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<OutboundEmailDto>>.SuccessResponse(rows));
    }

    /// <summary>Re-queues a Failed or Cancelled row for immediate retry.</summary>
    [HttpPost("email/{id}/resend")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendOutboundEmail(int id, CancellationToken ct)
    {
        var email = await _db.OutboundEmails.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (email == null)
            return NotFound(ApiResponse<object>.Error("Email not found"));

        if (email.Status is not (OutboundEmailStatus.Failed or OutboundEmailStatus.Cancelled))
            return BadRequest(ApiResponse<object>.Error($"Only a Failed or Cancelled email can be resent (current status: {email.Status})."));

        email.Status = OutboundEmailStatus.Queued;
        email.Attempts = 0;
        email.LastError = null;
        email.NextAttemptAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.SuccessResponse(null, "Email re-queued for delivery"));
    }

    /// <summary>Cancels a not-yet-sent email so it will never be attempted again.</summary>
    [HttpPost("email/{id}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOutboundEmail(int id, CancellationToken ct)
    {
        var email = await _db.OutboundEmails.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (email == null)
            return NotFound(ApiResponse<object>.Error("Email not found"));

        if (email.Status == OutboundEmailStatus.Sent)
            return BadRequest(ApiResponse<object>.Error("A sent email cannot be cancelled."));

        email.Status = OutboundEmailStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.SuccessResponse(null, "Email cancelled"));
    }

    /// <summary>Delivery-configuration + at-a-glance queue health for the admin banner.</summary>
    [HttpGet("email/status")]
    [ProducesResponseType(typeof(ApiResponse<OutboundEmailStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboundEmailStatus(CancellationToken ct)
    {
        var queued = await _db.OutboundEmails.AsNoTracking().CountAsync(e => e.Status == OutboundEmailStatus.Queued, ct);
        var failed = await _db.OutboundEmails.AsNoTracking().CountAsync(e => e.Status == OutboundEmailStatus.Failed, ct);
        var lastSentAt = await _db.OutboundEmails.AsNoTracking()
            .Where(e => e.SentAt != null)
            .OrderByDescending(e => e.SentAt)
            .Select(e => e.SentAt)
            .FirstOrDefaultAsync(ct);

        var dto = new OutboundEmailStatusDto
        {
            Configured = _emailTransport.IsConfigured,
            Queued = queued,
            Failed = failed,
            LastSentAt = lastSentAt
        };
        return Ok(ApiResponse<OutboundEmailStatusDto>.SuccessResponse(dto));
    }

    /// <summary>Plan 4: rows with a recorded email send failure, newest first — platform-admin visibility
    /// into <c>NotificationEmailWorker</c>/<c>DigestService</c> send errors (never silently dropped).</summary>
    [HttpGet("notifications/failures")]
    [ProducesResponseType(typeof(ApiResponse<List<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationFailures(CancellationToken ct)
    {
        var result = await _notificationService.GetFailuresAsync(200, ct);
        var items = (result.Data ?? new List<Services.Models.NotificationModel>()).Select(n => new NotificationDto
        {
            Id = n.Id,
            Kind = n.Kind,
            Title = n.Title,
            Body = n.Body,
            LinkPath = n.LinkPath,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt,
            EmailSentAt = n.EmailSentAt,
            EmailError = n.EmailError
        }).ToList();

        return Ok(ApiResponse<List<NotificationDto>>.SuccessResponse(items));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardStats>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        // Users
        var totalUsers = await _db.Users.AsNoTracking().CountAsync(ct);
        var activeUsers = await _db.Users.AsNoTracking().CountAsync(u => u.IsActive, ct);
        var adminUsers = await _db.Users.AsNoTracking().CountAsync(u => u.Role == UserRole.Admin, ct);
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
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                OnboardingCompleted = u.OnboardingCompletedAt != null,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
    }
}
