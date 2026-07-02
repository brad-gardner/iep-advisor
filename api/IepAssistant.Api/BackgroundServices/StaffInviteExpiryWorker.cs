using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Phase 3 pre-expiry reminder driver. Runs shortly after startup and then once daily, warning the inviting
/// admin (only) when a still-pending staff invite is within ~3 days (72h) of expiring. All correctness logic
/// lives in <see cref="IStaffInviteExpiryService"/> (candidate window, race guard, active-inviter/active-school
/// skips, best-effort stamping) so it is unit-testable without this timer. This worker owns only the schedule
/// and per-invite DI scoping: it scans for candidate ids in one scope, then processes each id in its OWN scope
/// with a fresh DbContext, wrapped in try/catch so a single failure is logged and skipped, never crashing the
/// loop (mirrors <see cref="AccessAuditLogWorker"/>).
///
/// SINGLE-INSTANCE ASSUMPTION: idempotency is a single timestamp (StaffInvite.ExpiryReminderSentAt) guarded by
/// a conditional UPDATE, which prevents double-sends within one instance but NOT across instances. A scaled-out
/// App Service (2+ instances) could double-send a reminder if both scan the same candidate before either
/// stamps. This is acceptable for the pilot (single instance); revisit with a distributed lock / leader election
/// if the API is scaled out.
/// </summary>
public class StaffInviteExpiryWorker : BackgroundService
{
    // Short settle delay after startup so the reminder pass doesn't contend with app boot/migrations.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaffInviteExpiryWorker> _logger;

    public StaffInviteExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<StaffInviteExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Staff Invite Expiry Worker started");

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunCycleAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        var utcNow = DateTime.UtcNow;

        // Scan for candidate ids in one scope (a snapshot; each id is re-verified in its own scope below).
        IReadOnlyList<int> candidateIds;
        try
        {
            using var scanScope = _scopeFactory.CreateScope();
            var service = scanScope.ServiceProvider.GetRequiredService<IStaffInviteExpiryService>();
            candidateIds = await service.FindExpiringInviteIdsAsync(utcNow, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Staff invite expiry scan failed; skipping this cycle.");
            return;
        }

        if (candidateIds.Count == 0)
            return;

        _logger.LogInformation("Staff invite expiry cycle: {Count} candidate invite(s) to evaluate.", candidateIds.Count);

        foreach (var inviteId in candidateIds)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IStaffInviteExpiryService>();
                await service.ProcessInviteAsync(inviteId, utcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed invite must never crash the worker or block the rest of the batch.
                _logger.LogError(ex, "Failed to process expiry reminder for staff invite {InviteId}.", inviteId);
            }
        }
    }
}
