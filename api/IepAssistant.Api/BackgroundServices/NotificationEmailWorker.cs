using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Drains queued <see cref="IepAssistant.Domain.Entities.Notification"/> rows every 30 seconds (plan 4,
/// decision 3). Mirrors <see cref="StaffInviteExpiryWorker"/>'s shape: a scan for candidate ids in one
/// scope, then each id processed in its OWN scope with a fresh DbContext, wrapped in try/catch so one bad
/// row is logged and skipped rather than crashing the loop. All decision logic (which
/// <see cref="IEmailService"/> method to call, retry/backoff bookkeeping, error recording) lives in
/// <see cref="INotificationEmailService"/> so it is unit-testable without this timer.
///
/// SINGLE-INSTANCE ASSUMPTION: idempotency is <c>EmailAttempts</c>/<c>EmailSentAt</c> on the row itself,
/// which prevents double-sends within one instance but not across instances. Acceptable for a single-App-
/// Service-instance pilot; revisit with a distributed lock if the API is ever scaled out (same caveat as
/// <see cref="StaffInviteExpiryWorker"/>).
/// </summary>
public class NotificationEmailWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEmailWorker> _logger;

    public NotificationEmailWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Email Worker started");

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
        IReadOnlyList<int> candidateIds;
        try
        {
            using var scanScope = _scopeFactory.CreateScope();
            var service = scanScope.ServiceProvider.GetRequiredService<INotificationEmailService>();
            candidateIds = await service.FindQueuedIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification email scan failed; skipping this cycle.");
            return;
        }

        if (candidateIds.Count == 0)
            return;

        foreach (var id in candidateIds)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<INotificationEmailService>();
                await service.ProcessNotificationAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed notification must never crash the worker or block the rest of the batch.
                _logger.LogError(ex, "Failed to process notification email {NotificationId}.", id);
            }
        }
    }
}
