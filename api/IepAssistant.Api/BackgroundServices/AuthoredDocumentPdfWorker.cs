using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Queue of (AuthoredDocumentVersion id, language) pairs whose PDF needs rendering (State Document
/// Template Engine, Phase 4; language added multilingual plan phase 7). The controller enqueues after
/// FinalizeAsync commits (and on retry, and on a first request for a not-yet-rendered language); the
/// single-consumer worker drains it. A DISTINCT queue type from <see cref="IepVersionPdfQueue"/> (never
/// reused) so the two pipelines stay independent.
/// </summary>
public class AuthoredDocumentPdfQueue
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

        await foreach (var (versionId, language) in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Rendering PDF for AuthoredDocumentVersion {VersionId} language {Language}", versionId, language);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuthoredDocumentPdfService>();
                await service.RenderAsync(versionId, language, stoppingToken);
            }
            catch (Exception ex)
            {
                // RenderAsync already isolates render failures; this guards against scope/resolution
                // failures so the loop never dies on a single bad item.
                _logger.LogError(ex, "Unhandled error rendering PDF for AuthoredDocumentVersion {VersionId} language {Language}", versionId, language);
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

            var pending = await context.AuthoredDocumentPdfs
                .Where(p => p.RenderStatus == PdfRenderStatus.Pending)
                .Select(p => new { p.AuthoredDocumentVersionId, p.Language })
                .ToListAsync(stoppingToken);

            if (pending.Count == 0)
                return;

            foreach (var p in pending)
                await _queue.EnqueueAsync(p.AuthoredDocumentVersionId, p.Language ?? SupportedLanguages.English, stoppingToken);

            _logger.LogWarning("Re-enqueued {Count} pending AuthoredDocument PDF render(s) from a previous process", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile pending AuthoredDocument PDF renders at startup");
        }
    }
}
