using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Runs <see cref="IMeetingReminderService.RunOnceAsync"/> every 15 minutes (plan 4, decision 6). All
/// T-7d/T-1d/T-1h scheduling and idempotency logic lives in the service so it is unit-testable without
/// this timer; the worker owns only the schedule and DI scoping.
/// </summary>
public class MeetingReminderWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingReminderWorker> _logger;

    public MeetingReminderWorker(IServiceScopeFactory scopeFactory, ILogger<MeetingReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meeting Reminder Worker started");

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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMeetingReminderService>();
            await service.RunOnceAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Meeting reminder cycle failed; skipping.");
        }
    }
}
