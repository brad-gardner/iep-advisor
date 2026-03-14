using System.Threading.Channels;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class IepAnalysisQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int documentId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(documentId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public class IepAnalysisWorker : BackgroundService
{
    private readonly IepAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IepAnalysisWorker> _logger;

    public IepAnalysisWorker(
        IepAnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<IepAnalysisWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IEP Analysis Worker started");

        await foreach (var documentId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Analyzing document {DocumentId}", documentId);

                using var scope = _scopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<IIepAnalysisService>();
                await analysisService.AnalyzeDocumentAsync(documentId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing document {DocumentId}", documentId);
            }
        }
    }
}
