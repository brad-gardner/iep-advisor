using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>See <see cref="IAuditIntegrityService"/>. Read-mostly: the only writes are the
/// <see cref="AuditIntegrityRun"/> row it creates for itself and, indirectly via
/// <see cref="INotificationService"/>, a notification on a broken chain. Never touches
/// <see cref="AccessAuditLog"/> rows — <c>AccessAuditLogWorker</c> is the sole writer of those.</summary>
public class AuditIntegrityService : IAuditIntegrityService
{
    private const int PageSize = 1000;
    private const int MaxDetailLength = 1000;

    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AuditIntegrityService> _logger;

    public AuditIntegrityService(ApplicationDbContext context, INotificationService notificationService, ILogger<AuditIntegrityService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AuditIntegrityRunModel> RunCheckAsync(CancellationToken ct = default)
    {
        var run = new AuditIntegrityRun
        {
            StartedAt = DateTime.UtcNow,
            Status = AuditIntegrityStatus.Ok
        };
        _context.AuditIntegrityRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        try
        {
            var (rowsChecked, firstBrokenId) = await WalkChainAsync(ct);

            run.RowsChecked = rowsChecked;
            run.FirstBrokenId = firstBrokenId;
            run.Status = firstBrokenId == null ? AuditIntegrityStatus.Ok : AuditIntegrityStatus.Broken;
            run.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            if (firstBrokenId != null)
            {
                _logger.LogError(
                    "Audit hash-chain integrity check found a broken chain starting at AccessAuditLog {FirstBrokenId} (run {RunId})",
                    firstBrokenId, run.Id);
                await NotifyPlatformAdminsAsync(firstBrokenId.Value, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit integrity check {RunId} failed", run.Id);
            run.Status = AuditIntegrityStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            run.Detail = Truncate(ex.Message);
            await _context.SaveChangesAsync(CancellationToken.None);
        }

        return MapToModel(run);
    }

    /// <summary>
    /// Walks hashed rows in Id order, recomputing each one's hash from its own fields plus the previous
    /// row's hash, and comparing to what is stored. Rows whose Hash is still null (not yet backfilled)
    /// are skipped rather than flagged — see <c>AccessAuditLogWorker.BackfillHashesAsync</c>; in steady
    /// state this filter is a no-op.
    /// </summary>
    private async Task<(int RowsChecked, int? FirstBrokenId)> WalkChainAsync(CancellationToken ct)
    {
        string? expectedPrev = null;
        var rowsChecked = 0;
        var lastId = 0;

        while (true)
        {
            var page = await _context.AccessAuditLogs.AsNoTracking()
                .Where(a => a.Hash != null && a.Id > lastId)
                .OrderBy(a => a.Id)
                .Take(PageSize)
                .ToListAsync(ct);

            if (page.Count == 0)
                return (rowsChecked, null);

            foreach (var row in page)
            {
                rowsChecked++;
                lastId = row.Id;

                if (row.PrevHash != expectedPrev)
                    return (rowsChecked, row.Id);

                var recomputed = AuditHashChain.ComputeHash(
                    row.Id, row.Action, row.ActorUserId, row.ResourceType, row.ResourceId, row.RecipientUserId, row.CreatedAt, row.PrevHash);
                if (recomputed != row.Hash)
                    return (rowsChecked, row.Id);

                expectedPrev = row.Hash;
            }
        }
    }

    private async Task NotifyPlatformAdminsAsync(int firstBrokenId, CancellationToken ct)
    {
        var adminIds = await _context.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (adminIds.Count == 0)
            return;

        await _notificationService.NotifyAsync(
            adminIds,
            NotificationKind.AuditIntegrityBroken,
            "Audit log integrity check failed",
            $"The audit hash chain is broken starting at AccessAuditLog #{firstBrokenId}. This may indicate the audit trail was altered outside the application.",
            "/admin/audit",
            $"audit-integrity-broken-{firstBrokenId}",
            emailImmediately: true,
            ct);
    }

    public async Task<List<AuditIntegrityRunModel>> GetRecentRunsAsync(int count, CancellationToken ct = default)
    {
        var runs = await _context.AuditIntegrityRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(count)
            .ToListAsync(ct);

        return runs.Select(MapToModel).ToList();
    }

    private static AuditIntegrityRunModel MapToModel(AuditIntegrityRun run) => new()
    {
        Id = run.Id,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        RowsChecked = run.RowsChecked,
        FirstBrokenId = run.FirstBrokenId,
        Status = run.Status.ToString(),
        Detail = run.Detail
    };

    private static string? Truncate(string? message) =>
        message == null ? null : (message.Length <= MaxDetailLength ? message : message[..MaxDetailLength]);
}
