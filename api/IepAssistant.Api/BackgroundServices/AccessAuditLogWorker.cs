using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Drains the singleton <see cref="AuditLogger"/> channel and persists queued events as
/// <see cref="AccessAuditLog"/> rows, in batches of up to <see cref="MaxBatchSize"/> (pilot-gates plan,
/// phase 1).
///
/// <para><b>Chain tip lives in the database, not in memory.</b> Every batch resolves the current tip
/// inside its own SERIALIZABLE transaction (the latest row by Id), so two API instances writing at
/// once — a rolling deploy, an autoscale — serialize on the tip instead of forking the chain; the
/// loser's transaction fails (SQL Server: a deadlock/serialization error, caught by the retry loop) and is
/// retried against the new tip. Nothing about the chain depends on a single process. The unit suite drives
/// two workers on separate threads against a file-backed SQLite database (whose writer lock stands in for
/// SQL Server's range locks); SQL Server's own lock/deadlock behaviour is exercised only by a live environment.</para>
///
/// <para><b>Durability (decision 1):</b> a batch that still fails after <see cref="MaxAttempts"/> retries
/// (backoff 1s/5s/30s) is staged into <see cref="PendingAuditEvent"/> instead of being dropped; on host
/// shutdown, <see cref="StopAsync"/> stages anything still sitting unread in the channel the same way. On
/// the next boot, <see cref="ReplayPendingEventsAsync"/> turns every staged row back into a real, hashed
/// <see cref="AccessAuditLog"/> row (and deletes the staging row) before normal draining resumes — so a
/// crash or restart can delay an audit event, but never silently loses it.</para>
///
/// <para><b>Hash chain (decision 1):</b> each row's <see cref="AccessAuditLog.Hash"/>/<see cref="AccessAuditLog.PrevHash"/>
/// is computed via <see cref="AuditHashChain"/> once the row's Id is known — which requires an INSERT
/// followed by an UPDATE that sets only those two columns. The AccessAuditLogs immutability trigger
/// (added in the same migration as these columns) carries a narrow carve-out for exactly that
/// null-to-non-null transition, mirroring the AuthoredDocumentVersion/SharedDraftRevision carve-outs
/// already used elsewhere for the same reason: a legitimate, well-defined post-insert update. Both
/// SaveChanges calls run inside one DB transaction so a failure between them can never leave a duplicate,
/// unhashed row behind on retry.</para>
/// </summary>
public class AccessAuditLogWorker : BackgroundService
{
    private const int MaxBatchSize = 200;
    private const int HashBackfillPageSize = 500;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryBackoffs =
        { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) };

    private readonly AuditLogger _auditLogger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccessAuditLogWorker> _logger;
    private readonly IReadOnlyList<TimeSpan> _retryBackoffs;

    public AccessAuditLogWorker(
        AuditLogger auditLogger,
        IServiceScopeFactory scopeFactory,
        ILogger<AccessAuditLogWorker> logger)
        : this(auditLogger, scopeFactory, logger, RetryBackoffs)
    {
    }

    /// <summary>Test seam: the backoff schedule between persist attempts (production uses 1s/5s/30s).</summary>
    internal AccessAuditLogWorker(
        AuditLogger auditLogger,
        IServiceScopeFactory scopeFactory,
        ILogger<AccessAuditLogWorker> logger,
        IReadOnlyList<TimeSpan> retryBackoffs)
    {
        _auditLogger = auditLogger;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retryBackoffs = retryBackoffs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access Audit Log Worker started");

        await BackfillHashesAsync();
        await ReplayPendingEventsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            List<AuditEntry> batch;
            try
            {
                batch = await _auditLogger.ReadBatchAsync(MaxBatchSize, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (batch.Count == 0)
                break; // channel completed — never happens in production, but keeps the loop well-defined

            await PersistBatchWithRetryAsync(batch, stoppingToken);
        }
    }

    /// <summary>
    /// Host shutdown: <see cref="BackgroundService.StopAsync"/>'s default behavior cancels the token
    /// passed to <see cref="ExecuteAsync"/> and awaits it (bounded by the host's shutdown timeout). Once
    /// that loop has returned, anything still sitting unread in the channel — never picked up by
    /// <see cref="ReadBatchAsync"/> before the loop exited — is staged into <see cref="PendingAuditEvent"/>
    /// so the next boot's replay picks it up.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        var stragglers = _auditLogger.DrainImmediately();
        if (stragglers.Count == 0)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var entry in stragglers)
                context.PendingAuditEvents.Add(ToPendingEvent(entry));

            // Deliberately CancellationToken.None: cancellationToken is already signalled during
            // shutdown, and this is the last-resort durability write — it must not be abortable, or
            // the events it exists to save would be lost anyway.
            await context.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning("Staged {Count} audit event(s) still queued at shutdown for replay on next start", stragglers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stage {Count} audit event(s) for replay at shutdown — they may be lost", stragglers.Count);
        }
    }

    private async Task PersistBatchWithRetryAsync(List<AuditEntry> batch, CancellationToken stoppingToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, stoppingToken);
                await InsertRowsAsync(context, batch, stoppingToken);
                await transaction.CommitAsync(stoppingToken);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "Audit batch persist attempt {Attempt}/{MaxAttempts} failed ({Count} event(s))",
                    attempt, MaxAttempts, batch.Count);

                if (attempt < MaxAttempts)
                {
                    try
                    {
                        await Task.Delay(_retryBackoffs[attempt - 1], stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // shutdown mid-backoff — fall through to staging below
                    }
                }
            }
        }

        _logger.LogError(lastException,
            "Audit batch persist exhausted retries ({Count} event(s)) — staging for replay on next start",
            batch.Count);
        await StageBatchAsPendingAsync(batch);
    }

    private async Task StageBatchAsPendingAsync(List<AuditEntry> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var entry in batch)
                context.PendingAuditEvents.Add(ToPendingEvent(entry));

            // CancellationToken.None: this is the durability fallback of last resort; it must not be
            // abortable by the same shutdown signal that may have caused the batch to fail.
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stage {Count} audit event(s) as pending after batch persist failure — they will be lost", batch.Count);
        }
    }

    /// <summary>
    /// Turns rows staged in <see cref="PendingAuditEvent"/> (by a previous crash/shutdown) back into
    /// real, hashed <see cref="AccessAuditLog"/> rows, deleting each staged row in the SAME transaction
    /// as its insert — so a crash mid-replay leaves the staged rows untouched for the next attempt
    /// rather than risking a duplicate.
    /// </summary>
    private async Task ReplayPendingEventsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var replayed = 0;
            while (true)
            {
                var pending = await context.PendingAuditEvents
                    .OrderBy(p => p.Id)
                    .Take(MaxBatchSize)
                    .ToListAsync(stoppingToken);

                if (pending.Count == 0)
                    break;

                var entries = pending
                    .Select(p => new AuditEntry(p.ActionValue, p.ActorUserId, p.ResourceType, p.ResourceId, p.RecipientUserId, p.OccurredAt))
                    .ToList();

                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, stoppingToken);
                await InsertRowsAsync(context, entries, stoppingToken);
                context.PendingAuditEvents.RemoveRange(pending);
                await context.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);

                replayed += pending.Count;
            }

            if (replayed > 0)
                _logger.LogWarning("Replayed {Count} pending audit event(s) staged by a previous crash/shutdown", replayed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay pending audit events at startup; they remain staged for the next restart");
        }
    }

    /// <summary>
    /// Backfills <see cref="AccessAuditLog.Hash"/>/<see cref="AccessAuditLog.PrevHash"/> for historical
    /// rows left null by the migration that introduced them, in Id order, paging so a large table is
    /// never loaded at once. A partial failure is harmless: the next batch reads the real tip from the
    /// database, and the next restart resumes the backfill where it stopped.
    /// </summary>
    private async Task BackfillHashesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var tip = await context.AccessAuditLogs
                .Where(a => a.Hash != null)
                .OrderByDescending(a => a.Id)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync();

            var backfilled = 0;
            // Keyset paging: hashed rows are never re-scanned; with the filtered
            // IX_AccessAuditLogs_Unhashed index a fully-hashed table costs one probe per restart.
            var lastId = 0;
            while (true)
            {
                var page = await context.AccessAuditLogs
                    .Where(a => a.Hash == null && a.Id > lastId)
                    .OrderBy(a => a.Id)
                    .Take(HashBackfillPageSize)
                    .ToListAsync();

                if (page.Count == 0)
                    break;
                lastId = page[^1].Id;

                foreach (var row in page)
                {
                    var hash = AuditHashChain.ComputeHash(
                        row.Id, row.Action, row.ActorUserId, row.ResourceType, row.ResourceId, row.RecipientUserId, row.CreatedAt, tip);
                    row.PrevHash = tip;
                    row.Hash = hash;
                    tip = hash;
                }

                await context.SaveChangesAsync();
                backfilled += page.Count;
            }

            if (backfilled > 0)
                _logger.LogInformation("Backfilled hash chain for {Count} historical audit log row(s)", backfilled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit hash-chain backfill failed at startup; unhashed rows (if any) will be retried on the next restart");
        }


    }

    /// <summary>
    /// Inside the caller's SERIALIZABLE transaction: reads the current chain tip from the database
    /// (the latest row by Id), inserts <paramref name="entries"/> (assigning Ids), then computes and
    /// saves their chained Hash/PrevHash. Two <c>SaveChangesAsync</c> calls, one transaction — a
    /// failure between them can never leave an unhashed row behind, and a concurrent writer that
    /// read the same tip fails at commit and retries against the new tip.
    /// </summary>
    private static async Task InsertRowsAsync(ApplicationDbContext context, List<AuditEntry> entries, CancellationToken ct)
    {
        var tip = await context.AccessAuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(ct);

        var rows = entries.Select(entry => new AccessAuditLog
        {
            Action = entry.Action,
            ActorUserId = entry.ActorUserId,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            RecipientUserId = entry.RecipientUserId,
            CreatedAt = entry.CreatedAt
        }).ToList();

        context.AccessAuditLogs.AddRange(rows);
        await context.SaveChangesAsync(ct); // assigns Ids

        foreach (var row in rows)
        {
            var hash = AuditHashChain.ComputeHash(
                row.Id, row.Action, row.ActorUserId, row.ResourceType, row.ResourceId, row.RecipientUserId, row.CreatedAt, tip);
            row.PrevHash = tip;
            row.Hash = hash;
            tip = hash;
        }

        await context.SaveChangesAsync(ct); // null-to-non-null Hash/PrevHash update — permitted by the trigger carve-out
    }

    private static PendingAuditEvent ToPendingEvent(AuditEntry entry) => new()
    {
        ActionValue = entry.Action,
        ActorUserId = entry.ActorUserId,
        ResourceType = entry.ResourceType,
        ResourceId = entry.ResourceId,
        RecipientUserId = entry.RecipientUserId,
        OccurredAt = entry.CreatedAt
    };
}
