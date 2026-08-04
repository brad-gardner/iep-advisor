using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Queue of AuthoredDocumentVersion ids whose PDF needs rendering (State Document Template Engine,
/// Phase 4). The controller enqueues after FinalizeAsync commits (and on retry); the single-consumer
/// worker drains it. A DISTINCT queue type from <see cref="IepVersionPdfQueue"/> (never reused) so the
/// two pipelines stay independent.
/// </summary>
public class AuthoredDocumentPdfQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int versionId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(versionId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// Single-consumer background worker that renders one AuthoredDocumentVersion PDF at a time. The
/// single-consumer loop naturally bounds render concurrency to 1 so a burst of finalizes can't starve the
/// thread pool. Per-item try/catch logs and continues; the render service itself also swallows render
/// failures into a retryable Error state. A startup sweep re-enqueues any AuthoredDocumentPdf left Pending
/// by a prior process crash mid-render. Mirrors <see cref="IepVersionPdfWorker"/>.
/// </summary>
public class AuthoredDocumentPdfWorker : BackgroundService
{
    private readonly AuthoredDocumentPdfQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthoredDocumentPdfWorker> _logger;

    public AuthoredDocumentPdfWorker(
        AuthoredDocumentPdfQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuthoredDocumentPdfWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuthoredDocument PDF Worker started");

        await ReconcilePendingRendersAsync(stoppingToken);

        await foreach (var versionId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Rendering PDF for AuthoredDocumentVersion {VersionId}", versionId);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuthoredDocumentPdfService>();
                await service.RenderAsync(versionId, stoppingToken);
            }
            catch (Exception ex)
            {
                // RenderAsync already isolates render failures; this guards against scope/resolution
                // failures so the loop never dies on a single bad item.
                _logger.LogError(ex, "Unhandled error rendering PDF for AuthoredDocumentVersion {VersionId}", versionId);
            }
        }
    }

    /// <summary>
    /// Re-enqueue any AuthoredDocumentPdf still in Pending from a prior process (enqueued but never
    /// rendered, or crashed mid-render). Re-rendering is idempotent (overwrites the same blob path + row).
    /// </summary>
    private async Task ReconcilePendingRendersAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendingVersionIds = await context.AuthoredDocumentPdfs
                .Where(p => p.RenderStatus == PdfRenderStatus.Pending)
                .Select(p => p.AuthoredDocumentVersionId)
                .ToListAsync(stoppingToken);

            if (pendingVersionIds.Count == 0)
                return;

            foreach (var versionId in pendingVersionIds)
                await _queue.EnqueueAsync(versionId, stoppingToken);

            _logger.LogWarning("Re-enqueued {Count} pending AuthoredDocument PDF render(s) from a previous process", pendingVersionIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile pending AuthoredDocument PDF renders at startup");
        }
    }
}
