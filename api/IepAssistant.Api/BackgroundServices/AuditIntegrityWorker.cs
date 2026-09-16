using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Runs <see cref="IAuditIntegrityService.RunCheckAsync"/> once a day at 03:00 UTC (pilot-gates plan,
/// phase 1, decision 1). Mirrors <c>DigestWorker</c>'s "compute the delay until the next fixed run time"
/// shape so a late start, a missed tick, or a host restart never causes a double-run or drift.
/// </summary>
public class AuditIntegrityWorker : BackgroundService
{
    private static readonly TimeSpan RunAtUtc = TimeSpan.FromHours(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditIntegrityWorker> _logger;

    public AuditIntegrityWorker(IServiceScopeFactory scopeFactory, ILogger<AuditIntegrityWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Integrity Worker started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = ComputeDelayUntilNextRun(DateTime.UtcNow);
                await Task.Delay(delay, stoppingToken);
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
    }

    /// <summary>Delay until the next 03:00 UTC strictly after <paramref name="utcNow"/>.</summary>
    internal static TimeSpan ComputeDelayUntilNextRun(DateTime utcNow)
    {
        var todayRun = utcNow.Date + RunAtUtc;
        var next = utcNow < todayRun ? todayRun : todayRun.AddDays(1);
        var delay = next - utcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuditIntegrityService>();
            await service.RunCheckAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audit integrity cycle failed; skipping.");
        }
    }
}
