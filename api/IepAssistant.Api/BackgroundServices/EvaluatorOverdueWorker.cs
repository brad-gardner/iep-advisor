using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Runs <see cref="IEvaluationCaseService.RunOverdueNotificationsAsync"/> once per day (plan 7, decision 1
/// — mirrors <see cref="DigestWorker"/>'s once-daily schedule pattern). All overdue-detection and dedup
/// logic lives in the service so it is unit-testable without this timer; the worker owns only the
/// schedule and DI scoping (mirrors <see cref="MeetingReminderWorker"/>'s scope-per-cycle pattern).
/// </summary>
public class EvaluatorOverdueWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EvaluatorOverdueWorker> _logger;

    public EvaluatorOverdueWorker(IServiceScopeFactory scopeFactory, ILogger<EvaluatorOverdueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Evaluator Overdue Worker started");

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
            var service = scope.ServiceProvider.GetRequiredService<IEvaluationCaseService>();
            await service.RunOverdueNotificationsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Evaluator overdue cycle failed; skipping.");
        }
    }
}
