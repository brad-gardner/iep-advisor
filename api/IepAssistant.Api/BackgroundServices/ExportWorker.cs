using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Queue of <see cref="ExportJob"/> ids to build (plan 7, decision 8). The controller enqueues after
/// <see cref="IExportService"/>'s enqueue methods commit. A DISTINCT queue type from
/// <see cref="AuthoredDocumentPdfQueue"/> (never reused) so the two pipelines stay independent.
/// </summary>
public class ExportQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int jobId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(jobId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// Single-consumer background worker that builds one export ZIP at a time (mirrors
/// <see cref="AuthoredDocumentPdfWorker"/>). A startup sweep re-enqueues any job left Queued or Running
/// by a prior process crash (a stuck Running job is reset to Queued first, since a mid-build crash left
/// no valid partial state to resume from).
/// </summary>
public class ExportWorker : BackgroundService
{
    private readonly ExportQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExportWorker> _logger;

    public ExportWorker(ExportQueue queue, IServiceScopeFactory scopeFactory, ILogger<ExportWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Export Worker started");

        await ReconcilePendingJobsAsync(stoppingToken);

        await foreach (var jobId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Building export {JobId}", jobId);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IExportService>();
                await service.RunAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                // RunAsync already isolates build failures into a Failed job status; this guards against
                // scope/resolution failures so the loop never dies on a single bad item.
                _logger.LogError(ex, "Unhandled error building export {JobId}", jobId);
            }
        }
    }

    private async Task ReconcilePendingJobsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuck = await context.ExportJobs
                .Where(j => j.Status == ExportJobStatus.Queued || j.Status == ExportJobStatus.Running)
                .ToListAsync(stoppingToken);

            if (stuck.Count == 0)
                return;

            foreach (var job in stuck)
            {
                if (job.Status == ExportJobStatus.Running)
                    job.Status = ExportJobStatus.Queued;
            }
            await context.SaveChangesAsync(stoppingToken);

            foreach (var job in stuck)
                await _queue.EnqueueAsync(job.Id, stoppingToken);

            _logger.LogWarning("Re-enqueued {Count} pending export(s) from a previous process", stuck.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile pending exports at startup");
        }
    }
}
