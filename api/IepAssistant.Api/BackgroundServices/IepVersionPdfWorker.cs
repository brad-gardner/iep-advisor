using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Queue of IepVersion ids whose PDF needs rendering (P5b). The controller enqueues after
/// FinalizeAsync commits (and on retry); the single-consumer worker drains it.
/// </summary>
public class IepVersionPdfQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int versionId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(versionId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// Single-consumer background worker that renders one IepVersion PDF at a time. The single-consumer
/// loop naturally bounds render concurrency to 1, satisfying the "bound worker concurrency" refinement
/// so a burst of finalizes can't starve the thread pool. Per-item try/catch logs and continues; the
/// render service itself also swallows render failures into a retryable Error state. A startup sweep
/// re-enqueues any IepVersionPdf left Pending by a prior process crash mid-render.
/// </summary>
public class IepVersionPdfWorker : BackgroundService
{
    private readonly IepVersionPdfQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IepVersionPdfWorker> _logger;

    public IepVersionPdfWorker(
        IepVersionPdfQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<IepVersionPdfWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IepVersion PDF Worker started");

        await ReconcilePendingRendersAsync(stoppingToken);

        await foreach (var versionId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Rendering PDF for IepVersion {VersionId}", versionId);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IIepVersionPdfService>();
                await service.RenderAsync(versionId, stoppingToken);
            }
            catch (Exception ex)
            {
                // RenderAsync already isolates render failures; this guards against scope/resolution
                // failures so the loop never dies on a single bad item.
                _logger.LogError(ex, "Unhandled error rendering PDF for IepVersion {VersionId}", versionId);
            }
        }
    }

    /// <summary>
    /// Re-enqueue any IepVersionPdf still in Pending from a prior process (enqueued but never rendered,
    /// or crashed mid-render). Re-rendering is idempotent (overwrites the same blob path + row).
    /// </summary>
    private async Task ReconcilePendingRendersAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendingVersionIds = await context.IepVersionPdfs
                .Where(p => p.RenderStatus == PdfRenderStatus.Pending)
                .Select(p => p.IepVersionId)
                .ToListAsync(stoppingToken);

            if (pendingVersionIds.Count == 0)
                return;

            foreach (var versionId in pendingVersionIds)
                await _queue.EnqueueAsync(versionId, stoppingToken);

            _logger.LogWarning("Re-enqueued {Count} pending IepVersion PDF render(s) from a previous process", pendingVersionIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile pending IepVersion PDF renders at startup");
        }
    }
}
