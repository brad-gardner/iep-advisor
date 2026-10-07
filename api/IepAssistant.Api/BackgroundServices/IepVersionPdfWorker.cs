using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Queue of (IepVersion id, language) pairs whose PDF needs rendering (P5b; language added multilingual
/// plan phase 7). The controller enqueues after FinalizeAsync commits (and on retry, and on a first
/// request for a not-yet-rendered language); the single-consumer worker drains it.
/// </summary>
public class IepVersionPdfQueue
{
    private readonly Channel<(int VersionId, string Language)> _channel = Channel.CreateUnbounded<(int, string)>();

    /// <summary>English, matching every pre-phase-7 call site that enqueued a bare version id.</summary>
    public ValueTask EnqueueAsync(int versionId, CancellationToken cancellationToken = default)
        => EnqueueAsync(versionId, SupportedLanguages.English, cancellationToken);

    public async ValueTask EnqueueAsync(int versionId, string language, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync((versionId, SupportedLanguages.Normalize(language) ?? SupportedLanguages.English), cancellationToken);

    public IAsyncEnumerable<(int VersionId, string Language)> DequeueAllAsync(CancellationToken cancellationToken)
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

        await foreach (var (versionId, language) in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Rendering PDF for IepVersion {VersionId} language {Language}", versionId, language);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IIepVersionPdfService>();
                await service.RenderAsync(versionId, language, stoppingToken);
            }
            catch (Exception ex)
            {
                // RenderAsync already isolates render failures; this guards against scope/resolution
                // failures so the loop never dies on a single bad item.
                _logger.LogError(ex, "Unhandled error rendering PDF for IepVersion {VersionId} language {Language}", versionId, language);
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

            var pending = await context.IepVersionPdfs
                .Where(p => p.RenderStatus == PdfRenderStatus.Pending)
                .Select(p => new { p.IepVersionId, p.Language })
                .ToListAsync(stoppingToken);

            if (pending.Count == 0)
                return;

            foreach (var p in pending)
                await _queue.EnqueueAsync(p.IepVersionId, p.Language ?? SupportedLanguages.English, stoppingToken);

            _logger.LogWarning("Re-enqueued {Count} pending IepVersion PDF render(s) from a previous process", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile pending IepVersion PDF renders at startup");
        }
    }
}
