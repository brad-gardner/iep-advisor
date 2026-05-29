using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class AnalysisRunQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int runId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public class AnalysisRunWorker : BackgroundService
{
    private readonly AnalysisRunQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisRunWorker> _logger;

    public AnalysisRunWorker(
        AnalysisRunQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalysisRunWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analysis Run Worker started");

        // Reconcile any runs orphaned by a previous process crash/restart. A run left in Running
        // (or Pending that was enqueued but never executed) still holds a reserved quota unit, so
        // we fail+refund each one before resuming normal processing.
        await ReconcileOrphanedRunsAsync(stoppingToken);

        await foreach (var runId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Executing analysis run {RunId}", runId);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAnalysisRunService>();
                await service.ExecuteRunAsync(runId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing analysis run {RunId}", runId);

                // The run may be stuck in Running with its quota unit still reserved (e.g. crash,
                // cancellation, or a failure before ExecuteRunAsync could transition it). Open a
                // fresh scope and fail+refund idempotently so the unit is not leaked.
                try
                {
                    using var failScope = _scopeFactory.CreateScope();
                    var failService = failScope.ServiceProvider.GetRequiredService<IAnalysisRunService>();
                    await failService.FailRunAsync(runId, "Analysis was interrupted.", CancellationToken.None);
                }
                catch (Exception failEx)
                {
                    _logger.LogError(failEx, "Failed to reconcile interrupted analysis run {RunId}", runId);
                }
            }
        }
    }

    private async Task ReconcileOrphanedRunsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IAnalysisRunService>();

            var orphanedIds = await context.AnalysisRuns
                .Where(r => r.Status == AnalysisRunStatus.Running || r.Status == AnalysisRunStatus.Pending)
                .Select(r => r.Id)
                .ToListAsync(stoppingToken);

            if (orphanedIds.Count == 0)
                return;

            foreach (var runId in orphanedIds)
                await service.FailRunAsync(runId, "Interrupted by restart", stoppingToken);

            _logger.LogWarning("Swept {Count} orphaned analysis run(s) left from a previous process", orphanedIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile orphaned analysis runs at startup");
        }
    }
}
